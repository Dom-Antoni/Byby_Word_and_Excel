using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

internal sealed class SheetImport
{
    public List<Formats.Worksheet> Sheets;
    public string Note, Text;
    public char Separator=';';
}

internal static class SheetFiles
{
    public const int MaxRows=10000,MaxColumns=512;
    public const string Filter="Alle unterstützten Tabellen|*.csv;*.tsv;*.tab;*.txt;*.xlsx;*.xlsm;*.xltx;*.xltm;*.ods;*.fods;*.xml;*.html;*.htm;*.xls|Excel (*.xlsx;*.xlsm;*.xltx;*.xltm)|*.xlsx;*.xlsm;*.xltx;*.xltm|CSV / Text|*.csv;*.tsv;*.tab;*.txt|OpenDocument (*.ods;*.fods)|*.ods;*.fods|HTML / Excel-XML|*.html;*.htm;*.xml;*.xls|Alle Dateien|*.*";
    public static void Bounds(int rows,int columns)
    {
        if(rows<1 || columns<1 || rows>MaxRows || columns>MaxColumns)throw new FormatException("Unterstützt werden bis zu 10.000 Zeilen und 512 Spalten je Blatt.");
    }
    public static string SeparatorName(char separator){return separator=='\t'?"Tabulator":separator==','?"Komma":"Semikolon";}
    public static char Detect(string text)
    {
        // Count separators outside quotes in the first 60 logical records.
        var records=new List<int[]>();var counts=new int[3];bool quoted=false,content=false;
        for(int i=0;i<text.Length && records.Count<60;i++)
        {
            char c=text[i];
            if(c=='"'){if(quoted && i+1<text.Length && text[i+1]=='"'){i++;continue;}quoted=!quoted;content=true;}
            else if(!quoted && (c=='\r' || c=='\n'))
            {
                if(content)records.Add(counts);counts=new int[3];content=false;if(c=='\r' && i+1<text.Length && text[i+1]=='\n')i++;
            }
            else {if(!char.IsWhiteSpace(c))content=true;if(!quoted){if(c=='\t')counts[0]++;if(c==';')counts[1]++;if(c==',')counts[2]++;}}
        }
        if(content)records.Add(counts);
        double best=0;int chosen=1;
        for(int candidate=0;candidate<3;candidate++)
        {
            var positive=records.Select(x=>x[candidate]).Where(x=>x>0).ToArray();if(positive.Length==0)continue;
            int consistent=positive.GroupBy(x=>x).Max(x=>x.Count());
            double score=(double)consistent/records.Count+0.1*positive.Length/records.Count;
            // Ties prefer tab, then semicolon, avoiding decimal commas in European data.
            if(score>best+0.00001){best=score;chosen=candidate;}
        }
        return new[]{'\t',';',','}[chosen];
    }
    public static SheetImport FromText(string text,char? separator,string name)
    {
        string body=text.TrimStart('\ufeff');char? directive=null;
        var hint=Regex.Match(body,@"^sep=([;,\t])(?:\r\n|\r|\n|$)",RegexOptions.IgnoreCase);
        if(hint.Success){directive=hint.Groups[1].Value[0];body=body.Substring(hint.Length);}
        char delimiter=separator??directive??Detect(body);var rows=Sheet.Read(body,delimiter);
        while(rows.Count>0 && rows[rows.Count-1].All(string.IsNullOrEmpty))rows.RemoveAt(rows.Count-1);
        int columns=rows.Count==0?1:Math.Max(1,rows.Max(x=>x.Length));Bounds(Math.Max(1,rows.Count),columns);
        var cells=new string[Math.Max(1,rows.Count),columns];
        for(int r=0;r<rows.Count;r++)for(int c=0;c<rows[r].Length;c++)cells[r,c]=rows[r][c];
        return new SheetImport {Sheets=new List<Formats.Worksheet>{new Formats.Worksheet {Name=name,Cells=cells}},Separator=delimiter,Text=text,Note=SeparatorName(delimiter)+(separator.HasValue?" · manuell gewählt":directive.HasValue?" · sep-Angabe übernommen":" · automatisch erkannt")};
    }
    public static SheetImport Read(string path,char? separator=null)
    {
        string extension=Path.GetExtension(path).ToLowerInvariant();
        if(new[]{".xlsx",".xlsm",".xltx",".xltm"}.Contains(extension))
            return Book(Formats.ReadXlsx(path),"Excel-Werteimport · gespeicherte Formelergebnisse; grundlegende Datumsanzeige · Export als CSV/TSV");
        if(extension==".ods")
        {
            using(var zip=ZipFile.OpenRead(path))return Book(Ods(ReadXml(zip,"content.xml")),"OpenDocument-Werteimport · gespeicherte Ergebnisse · Export als CSV/TSV");
        }
        string text;
        if(extension==".xls")
        {
            // Several exporters use .xls for HTML or SpreadsheetML; actual BIFF is binary.
            using(var stream=File.OpenRead(path)){if(stream.ReadByte()==0xd0)throw new FormatException("Das alte binäre XLS-Format wird nicht direkt unterstützt. Bitte die Datei als XLSX, ODS oder CSV bereitstellen. Die Originaldatei bleibt unverändert.");}
        }
        text=Formats.ReadText(path,extension==".html" || extension==".htm" || extension==".xls");
        if(extension==".fods")return Book(Ods(ReadXml(text)),"OpenDocument-Werteimport · Export als CSV/TSV");
        if(extension==".xml" || Regex.IsMatch(text,@"<(?:(?:\w+):)?Workbook\b",RegexOptions.IgnoreCase))
        {
            var xml=ReadXml(text);
            if(xml.Root!=null && xml.Root.Name.NamespaceName=="urn:schemas-microsoft-com:office:spreadsheet")
                return Book(SpreadsheetXml(xml),"Excel-XML-Werteimport · Export als CSV/TSV");
            throw new FormatException("Diese XML-Datei ist keine unterstützte Excel-XML-Arbeitsmappe.");
        }
        if(new[]{".html",".htm",".xls"}.Contains(extension) || Regex.IsMatch(text,@"^\s*(?:<!doctype html|<html\b)",RegexOptions.IgnoreCase))
            return Book(Html(text),"HTML-Tabellen · vereinfachte Zellwerte, ohne Skripte oder externe Inhalte");
        return FromText(text,separator,Path.GetFileNameWithoutExtension(path));
    }
    static SheetImport Book(List<Formats.Worksheet> sheets,string note)
    {
        if(sheets.Count==0)throw new FormatException("Keine Tabellenblätter gefunden.");if(sheets.Count>64)throw new FormatException("Diese Version unterstützt bis zu 64 Tabellenblätter.");
        return new SheetImport {Sheets=sheets,Note=note};
    }
    static XDocument ReadXml(string text)
    {
        using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=30000000}))return XDocument.Load(reader);
    }
    static XDocument ReadXml(ZipArchive zip,string path)
    {
        var entry=zip.GetEntry(path);if(entry==null)throw new FormatException("Dateibestandteil fehlt: "+path);
        if(entry.Length>30000000)throw new FormatException("Dateibestandteil ist zu groß.");
        using(var stream=entry.Open())using(var reader=XmlReader.Create(stream,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=30000000}))return XDocument.Load(reader);
    }
    sealed class Sparse
    {
        readonly Dictionary<long,string> values=new Dictionary<long,string>();int rows=1,columns=1;
        public void Put(long row,long column,string value)
        {
            if(string.IsNullOrEmpty(value))return;
            if(row<0 || column<0 || row>=MaxRows || column>=MaxColumns)throw new FormatException("Unterstützt werden bis zu 10.000 Zeilen und 512 Spalten je Blatt.");
            values[row*MaxColumns+column]=value;rows=Math.Max(rows,(int)row+1);columns=Math.Max(columns,(int)column+1);
        }
        public bool Has(int row,int column){return values.ContainsKey((long)row*MaxColumns+column);}
        public string[,] Cells(){var result=new string[rows,columns];foreach(var item in values)result[item.Key/MaxColumns,item.Key%MaxColumns]=item.Value;return result;}
    }
    static int Repeat(XElement element,XName attribute)
    {
        string raw=(string)element.Attribute(attribute);int value;if(raw==null)return 1;
        if(!int.TryParse(raw,out value) || value<1 || value>2000000)throw new FormatException("Ungültige Wiederholungsanzahl in OpenDocument.");return value;
    }
    static List<Formats.Worksheet> Ods(XDocument doc)
    {
        XNamespace t="urn:oasis:names:tc:opendocument:xmlns:table:1.0",o="urn:oasis:names:tc:opendocument:xmlns:office:1.0",p="urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        var result=new List<Formats.Worksheet>();
        foreach(var table in doc.Descendants(o+"spreadsheet").Elements(t+"table"))
        {
            if(result.Count>=64)throw new FormatException("Diese Version unterstützt bis zu 64 Tabellenblätter.");
            var values=new Sparse();long row=0;
            foreach(var source in table.Descendants(t+"table-row").Where(x=>x.Ancestors(t+"table").First()==table))
            {
                int rowRepeat=Repeat(source,t+"number-rows-repeated");long col=0;
                foreach(var cell in source.Elements().Where(x=>x.Name==t+"table-cell" || x.Name==t+"covered-table-cell"))
                {
                    int colRepeat=Repeat(cell,t+"number-columns-repeated");string type=(string)cell.Attribute(o+"value-type"),value="";
                    if(cell.Name==t+"table-cell")
                    {
                        value=string.Join("\n",cell.Elements(p+"p").Select(x=>OdsText(x,p)));
                        if(type=="float" || type=="percentage" || type=="currency")value=(string)cell.Attribute(o+"value")??value;
                        else if(type=="date")value=(string)cell.Attribute(o+"date-value")??value;
                        else if(type=="boolean")value=(string)cell.Attribute(o+"boolean-value")=="true"?"WAHR":"FALSCH";
                        else if(type=="time")value=value!=""?value:(string)cell.Attribute(o+"time-value")??"";
                        else if(value=="")value=(string)cell.Attribute(o+"string-value")??"";
                        if(value=="" && cell.Attribute(t+"formula")!=null)value="#KEIN_CACHE!";
                    }
                    if(value!="")
                    {
                        if(row+rowRepeat>MaxRows || col+colRepeat>MaxColumns)throw new FormatException("Unterstützt werden bis zu 10.000 Zeilen und 512 Spalten je Blatt.");
                        for(int r=0;r<rowRepeat;r++)for(int c=0;c<colRepeat;c++)values.Put(row+r,col+c,value);
                    }
                    col+=colRepeat;
                }
                row+=rowRepeat;
            }
            result.Add(new Formats.Worksheet {Name=(string)table.Attribute(t+"name")??"Tabelle",Cells=values.Cells()});
        }
        return result;
    }
    static string OdsText(XElement element,XNamespace p)
    {
        var text=new StringBuilder();foreach(var node in element.Nodes())
        {
            var literal=node as XText;if(literal!=null){text.Append(literal.Value);continue;}var child=node as XElement;if(child==null)continue;
            if(child.Name==p+"s"){int count;int.TryParse((string)child.Attribute(p+"c")??"1",out count);text.Append(' ',Math.Max(1,Math.Min(1000,count)));}
            else if(child.Name==p+"tab")text.Append('\t');else if(child.Name==p+"line-break")text.Append('\n');else text.Append(OdsText(child,p));
        }return text.ToString();
    }
    static List<Formats.Worksheet> SpreadsheetXml(XDocument doc)
    {
        XNamespace s="urn:schemas-microsoft-com:office:spreadsheet";var result=new List<Formats.Worksheet>();
        foreach(var sheet in doc.Root.Elements(s+"Worksheet"))
        {
            if(result.Count>=64)throw new FormatException("Diese Version unterstützt bis zu 64 Tabellenblätter.");
            var values=new Sparse();int row=0;
            foreach(var source in sheet.Elements(s+"Table").Elements(s+"Row"))
            {
                row=XmlIndex(source,s+"Index",row);int col=0;
                foreach(var cell in source.Elements(s+"Cell"))
                {
                    col=XmlIndex(cell,s+"Index",col);var data=cell.Element(s+"Data");string value=data==null?"":data.Value;
                    if(data!=null && (string)data.Attribute(s+"Type")=="Boolean")value=value=="1"?"WAHR":"FALSCH";
                    if(data==null && cell.Attribute(s+"Formula")!=null)value="#KEIN_CACHE!";
                    values.Put(row,col,value);int merged;int.TryParse((string)cell.Attribute(s+"MergeAcross"),out merged);if(merged<0 || merged>MaxColumns)throw new FormatException("Ungültiger Zellbereich.");col+=1+merged;
                }row++;
            }
            result.Add(new Formats.Worksheet {Name=(string)sheet.Attribute(s+"Name")??"Tabelle",Cells=values.Cells()});
        }return result;
    }
    static int XmlIndex(XElement element,XName name,int fallback){string text=(string)element.Attribute(name);int value;if(text==null)return fallback;if(!int.TryParse(text,out value) || value<1)throw new FormatException("Ungültiger Zellindex.");return value-1;}
    static List<Formats.Worksheet> Html(string text)
    {
        text=Regex.Replace(text,@"<!--[\s\S]*?-->|<(script|style)\b[^>]*>[\s\S]*?</\1\s*>","",RegexOptions.IgnoreCase);
        var result=new List<Formats.Worksheet>();
        foreach(Match table in Regex.Matches(text,@"<table\b[^>]*>([\s\S]*?)</table\s*>",RegexOptions.IgnoreCase))
        {
            if(result.Count>=64)throw new FormatException("Diese Version unterstützt bis zu 64 Tabellenblätter.");
            var values=new Sparse();int row=0;
            foreach(Match source in Regex.Matches(table.Groups[1].Value,@"<tr\b[^>]*>([\s\S]*?)</tr\s*>",RegexOptions.IgnoreCase))
            {
                int col=0;foreach(Match cell in Regex.Matches(source.Groups[1].Value,@"<(td|th)\b(?<attrs>[^>]*)>(?<value>[\s\S]*?)</\1\s*>",RegexOptions.IgnoreCase))
                {
                    while(values.Has(row,col))col++;int across=Span(cell.Groups["attrs"].Value,"colspan"),down=Span(cell.Groups["attrs"].Value,"rowspan");
                    string value=cell.Groups["value"].Value;value=Regex.Replace(value,@"<br\s*/?>","\n",RegexOptions.IgnoreCase);
                    value=WebUtility.HtmlDecode(Regex.Replace(value,@"<[^>]*>","")).Trim();
                    if(row+down>MaxRows || col+across>MaxColumns)throw new FormatException("HTML-Tabelle überschreitet den unterstützten Zellbereich.");
                    values.Put(row,col,value);
                    // Occupied merged slots use empty placeholders; retain layout without duplicating text.
                    for(int r=0;r<down;r++)for(int c=0;c<across;c++)if(r!=0 || c!=0)values.Put(row+r,col+c,"\u200b");
                    col+=across;
                }row++;
            }
            var cells=values.Cells();for(int r=0;r<cells.GetLength(0);r++)for(int c=0;c<cells.GetLength(1);c++)if(cells[r,c]=="\u200b")cells[r,c]="";
            var caption=Regex.Match(table.Groups[1].Value,@"<caption\b[^>]*>([\s\S]*?)</caption>",RegexOptions.IgnoreCase);
            result.Add(new Formats.Worksheet {Name=caption.Success?WebUtility.HtmlDecode(Regex.Replace(caption.Groups[1].Value,@"<[^>]*>","")).Trim():"Tabelle "+(result.Count+1),Cells=cells});
        }return result;
    }
    static int Span(string attributes,string name){var match=Regex.Match(attributes,@"\b"+name+@"\s*=\s*['""]?(\d+)",RegexOptions.IgnoreCase);int count;if(!match.Success || !int.TryParse(match.Groups[1].Value,out count))return 1;return Math.Max(1,Math.Min(MaxRows+1,count));}
}
