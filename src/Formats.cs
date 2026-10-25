using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

internal static class Formats
{
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
    public sealed class Worksheet { public string Name; public string[,] Cells; }
    public static List<Worksheet> ReadXlsx(string path)
    {
        XNamespace s="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        using(var zip=ZipFile.OpenRead(path))
        {
            var shared=zip.GetEntry("xl/sharedStrings.xml")==null?new string[0]:Xml(zip,"xl/sharedStrings.xml").Descendants(s+"si").Select(e=>string.Concat(e.Descendants(s+"t").Select(t=>t.Value))).ToArray();
            var links=Xml(zip,"xl/_rels/workbook.xml.rels").Root.Elements().Where(e=>(string)e.Attribute("TargetMode")!="External").ToDictionary(e=>(string)e.Attribute("Id"),e=>(string)e.Attribute("Target"));
            var sheets=new List<Worksheet>();
            foreach(var sheet in Xml(zip,"xl/workbook.xml").Descendants(s+"sheet"))
            {
                string target=links[(string)sheet.Attribute(rel+"id")];
                string entry=target.StartsWith("/")?target.TrimStart('/'):("xl/"+target);
                var doc=Xml(zip,entry);int maxRow=100,maxCol=26;
                foreach(var cell in doc.Descendants(s+"c")) { int r,c; Coordinates((string)cell.Attribute("r"),out r,out c);maxRow=Math.Max(maxRow,r+1);maxCol=Math.Max(maxCol,c+1); }
                var values=new string[maxRow,maxCol];
                foreach(var cell in doc.Descendants(s+"c"))
                {
                    int r,c;Coordinates((string)cell.Attribute("r"),out r,out c);
                    string type=(string)cell.Attribute("t"); string value=(string)cell.Element(s+"v")??"";
                    if(type=="s") { int index;if(!int.TryParse(value,out index)||index<0||index>=shared.Length)throw new FormatException("Ungültiger Textverweis in XLSX.");value=shared[index]; }
                    else if(type=="inlineStr")value=string.Concat(cell.Descendants(s+"t").Select(t=>t.Value));
                    else if(type=="b")value=value=="1"?"WAHR":"FALSCH";
                    // Cached results only. Do not pretend to recalculate arbitrary Excel formulas.
                    else if(cell.Element(s+"f")!=null && value=="")value="#KEIN_CACHE!";
                    values[r,c]=value;
                }
                sheets.Add(new Worksheet { Name=(string)sheet.Attribute("name"),Cells=values });
            }
            return sheets;
        }
    }
    static void Coordinates(string address,out int row,out int col)
    {
        if(string.IsNullOrEmpty(address))throw new FormatException("Zelladresse fehlt.");
        col=0;int i=0;while(i<address.Length && address[i]>='A' && address[i]<='Z')col=checked(col*26+address[i++]-'A'+1);
        if(col==0 || !int.TryParse(address.Substring(i),out row) || row<1 || row>10000 || col>512)throw new FormatException("Unterstützter Bereich: A1 bis SR10000 (512 Spalten).");
        row--;col--;
    }
}
