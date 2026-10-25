using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Text.RegularExpressions;

internal static class Formats
{
    public static string ReadText(string path,bool html=false)
    {
        if(new FileInfo(path).Length>30000000)throw new FormatException("Diese Textdatei ist für die aktuelle Version zu groß (maximal 30 MB).");
        var bytes=File.ReadAllBytes(path);Encoding encoding=null;int skip=0;
        if(bytes.Length>=4 && bytes[0]==0xff && bytes[1]==0xfe && bytes[2]==0 && bytes[3]==0){encoding=Encoding.UTF32;skip=4;}
        else if(bytes.Length>=4 && bytes[0]==0 && bytes[1]==0 && bytes[2]==0xfe && bytes[3]==0xff){encoding=new UTF32Encoding(true,true);skip=4;}
        else if(bytes.Length>=3 && bytes[0]==0xef && bytes[1]==0xbb && bytes[2]==0xbf){encoding=Encoding.UTF8;skip=3;}
        else if(bytes.Length>=2 && bytes[0]==0xff && bytes[1]==0xfe){encoding=Encoding.Unicode;skip=2;}
        else if(bytes.Length>=2 && bytes[0]==0xfe && bytes[1]==0xff){encoding=Encoding.BigEndianUnicode;skip=2;}
        if(encoding==null && html)
        {
            string head=Encoding.ASCII.GetString(bytes,0,Math.Min(bytes.Length,8192));var charset=Regex.Match(head,"charset\\s*=\\s*['\"\\s]*([a-zA-Z0-9_-]+)",RegexOptions.IgnoreCase);
            if(charset.Success)try{encoding=Encoding.GetEncoding(charset.Groups[1].Value);}catch(ArgumentException){}
        }
        if(encoding==null && bytes.Length>=8)
        {
            int sample=Math.Min(bytes.Length,2048),odd=0,even=0;for(int i=0;i<sample;i++)if(bytes[i]==0){if(i%2==0)even++;else odd++;}
            if(odd>sample/4 && even<sample/20)encoding=Encoding.Unicode;else if(even>sample/4 && odd<sample/20)encoding=Encoding.BigEndianUnicode;
        }
        string text;
        if(encoding!=null)text=encoding.GetString(bytes,skip,bytes.Length-skip);
        else try{text=new UTF8Encoding(false,true).GetString(bytes);}catch(DecoderFallbackException){text=Encoding.GetEncoding(1252).GetString(bytes);}
        if(text.Any(c=>c=='\0') || text.Count(c=>char.IsControl(c) && c!='\r' && c!='\n' && c!='\t' && c!='\f')>Math.Max(2,text.Length/50))throw new FormatException("Die Datei enthält binäre Daten und kann nicht als Text geöffnet werden.");
        return text;
    }
    static XDocument Xml(ZipArchive zip, string path)
    {
        var entry=zip.GetEntry(path);if(entry==null)throw new FormatException("Dateibestandteil fehlt: "+path);
        if(entry.Length>30000000)throw new FormatException("Dateibestandteil ist für diese Version zu groß.");
        using(var stream=entry.Open())using(var reader=System.Xml.XmlReader.Create(stream,new System.Xml.XmlReaderSettings { DtdProcessing=System.Xml.DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=30000000 }))return XDocument.Load(reader);
    }
    public static string ReadDocx(string path)
    {
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        using(var zip=ZipFile.OpenRead(path))
            return string.Join(Environment.NewLine,Xml(zip,"word/document.xml").Descendants(w+"p").Select(p=>string.Concat(p.Descendants().Where(e=>e.Name==w+"t" || e.Name==w+"tab" || e.Name==w+"br").Select(e=>e.Name==w+"tab"?"\t":e.Name==w+"br"?Environment.NewLine:e.Value))));
    }
    public sealed class Worksheet { public string Name; public string[,] Cells; public string ExportPath; }
    public static List<Worksheet> ReadXlsx(string path)
    {
        XNamespace s="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        using(var zip=ZipFile.OpenRead(path))
        {
            var shared=zip.GetEntry("xl/sharedStrings.xml")==null?new string[0]:Xml(zip,"xl/sharedStrings.xml").Descendants(s+"si").Select(e=>string.Concat(e.Descendants(s+"t").Select(t=>t.Value))).ToArray();
            var links=Xml(zip,"xl/_rels/workbook.xml.rels").Root.Elements().Where(e=>(string)e.Attribute("TargetMode")!="External").ToDictionary(e=>(string)e.Attribute("Id"),e=>(string)e.Attribute("Target"));
            var sheets=new List<Worksheet>();var book=Xml(zip,"xl/workbook.xml");
            bool date1904=book.Descendants(s+"workbookPr").Any(x=>(string)x.Attribute("date1904")=="1" || (string)x.Attribute("date1904")=="true");
            var numberFormats=new Dictionary<int,string>();int[] styleFormats=new int[0];
            if(zip.GetEntry("xl/styles.xml")!=null)
            {
                var styles=Xml(zip,"xl/styles.xml");
                foreach(var format in styles.Descendants(s+"numFmt")){int id;if(int.TryParse((string)format.Attribute("numFmtId"),out id))numberFormats[id]=(string)format.Attribute("formatCode")??"";}
                var xfs=styles.Root.Element(s+"cellXfs");if(xfs!=null)styleFormats=xfs.Elements(s+"xf").Select(x=>{int id;return int.TryParse((string)x.Attribute("numFmtId"),out id)?id:0;}).ToArray();
            }
            foreach(var sheet in book.Descendants(s+"sheet"))
            {
                if(sheets.Count>=64)throw new FormatException("Diese Version unterstützt bis zu 64 Tabellenblätter.");
                string target;if(!links.TryGetValue((string)sheet.Attribute(rel+"id")??"",out target))throw new FormatException("Tabellenblattverweis fehlt oder verweist auf eine externe Datei.");
                string entry=new Uri(new Uri("https://local.invalid/xl/workbook.xml"),target).AbsolutePath.TrimStart('/');
                var doc=Xml(zip,Uri.UnescapeDataString(entry));int maxRow=1,maxCol=1;
                var populated=doc.Descendants(s+"c").Where(x=>x.Element(s+"f")!=null || x.Element(s+"v")!=null || x.Element(s+"is")!=null).ToArray();
                foreach(var cell in populated) { int r,c; Coordinates((string)cell.Attribute("r"),out r,out c);maxRow=Math.Max(maxRow,r+1);maxCol=Math.Max(maxCol,c+1); }
                var values=new string[maxRow,maxCol];
                foreach(var cell in populated)
                {
                    int r,c;Coordinates((string)cell.Attribute("r"),out r,out c);
                    string type=(string)cell.Attribute("t"); string value=(string)cell.Element(s+"v")??"";
                    if(type=="s") { int index;if(!int.TryParse(value,out index)||index<0||index>=shared.Length)throw new FormatException("Ungültiger Textverweis in XLSX.");value=shared[index]; }
                    else if(type=="inlineStr")value=string.Concat(cell.Descendants(s+"t").Select(t=>t.Value));
                    else if(type=="b")value=value=="1"?"WAHR":"FALSCH";
                    // Cached results only. Do not pretend to recalculate arbitrary Excel formulas.
                    else if(cell.Element(s+"f")!=null && value=="")value="#KEIN_CACHE!";
                    if(type==null || type=="n")
                    {
                        int index;if(int.TryParse((string)cell.Attribute("s"),out index) && index>=0 && index<styleFormats.Length)
                        {
                            string code;numberFormats.TryGetValue(styleFormats[index],out code);
                            value=DisplayDate(value,styleFormats[index],code,date1904);
                        }
                    }
                    values[r,c]=value;
                }
                sheets.Add(new Worksheet { Name=(string)sheet.Attribute("name"),Cells=values });
            }
            return sheets;
        }
    }
    static string DisplayDate(string value,int id,string format,bool date1904)
    {
        bool date=id>=14 && id<=17 || id==22 || id>=27 && id<=36 || id>=50 && id<=58;
        bool time=id>=18 && id<=22 || id>=45 && id<=47;
        string code=Regex.Replace(format??"",@"""[^""]*""|\\.|_.|\*.", "");
        bool elapsed=Regex.IsMatch(code,@"\[[hms]+\]",RegexOptions.IgnoreCase);
        code=Regex.Replace(code,@"\[[^\]]*\]","");
        if(format!=null){date=Regex.IsMatch(code,@"[yd]",RegexOptions.IgnoreCase) || Regex.IsMatch(code,@"m",RegexOptions.IgnoreCase) && !Regex.IsMatch(code,@"[hs]",RegexOptions.IgnoreCase);time=Regex.IsMatch(code,@"[hs]",RegexOptions.IgnoreCase);}
        double serial;if((!date && !time && !elapsed) || !double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out serial))return value;
        try
        {
            if(elapsed){var span=TimeSpan.FromDays(serial);return ((long)span.TotalHours).ToString(CultureInfo.InvariantCulture)+":"+span.Minutes.ToString("00")+":"+span.Seconds.ToString("00");}
            DateTime converted=date1904?new DateTime(1904,1,1).AddDays(serial):DateTime.FromOADate(serial+(serial>=0 && serial<60?1:0));
            return converted.ToString(date?(time?"dd.MM.yyyy HH:mm:ss":"dd.MM.yyyy"):"HH:mm:ss",CultureInfo.GetCultureInfo("de-DE"));
        }
        catch(ArgumentException){return value;}catch(OverflowException){return value;}
    }
    static void Coordinates(string address,out int row,out int col)
    {
        if(string.IsNullOrEmpty(address))throw new FormatException("Zelladresse fehlt.");
        col=0;int i=0;while(i<address.Length && address[i]>='A' && address[i]<='Z')col=checked(col*26+address[i++]-'A'+1);
        if(col==0 || !int.TryParse(address.Substring(i),out row) || row<1 || row>10000 || col>512)throw new FormatException("Unterstützter Bereich: A1 bis SR10000 (512 Spalten).");
        row--;col--;
    }
}
