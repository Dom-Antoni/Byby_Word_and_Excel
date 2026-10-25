using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

internal static class Checks
{
    static int count;
    static void Equal(object expected,object actual) { if(!object.Equals(expected,actual))throw new Exception("Erwartet: "+expected+"; erhalten: "+actual);count++; }
    static void Error(Action action,string expected) { try { action(); }catch(FormatException e){Equal(expected,e.Message);return;}throw new Exception("Erwarteter Fehler fehlt: "+expected); }
    static void Entry(ZipArchive zip,string path,string text) { using(var w=new StreamWriter(zip.CreateEntry(path).Open()))w.Write(text); }
    public static void Main()
    {
        var rows=Sheet.Read("Name;Notiz\r\n\"Müller\";\"Zeile 1\nZeile 2; \"\"Zitat\"\"\"\r\n",';');
        Equal(2,rows.Count);Equal("Müller",rows[1][0]);Equal("Zeile 1\nZeile 2; \"Zitat\"",rows[1][1]);
        Equal("",Sheet.Read("a;b;",';')[0][2]);Equal("\"a\"\"b\"",Sheet.Quote("a\"b"));
        Error(delegate { Sheet.Read("\"offen",';'); },"Nicht geschlossenes Anführungszeichen in CSV.");
        Equal("A",Sheet.Column(0));Equal("Z",Sheet.Column(25));Equal("AA",Sheet.Column(26));
        var cells=new string[10,30];cells[0,0]="1,5";cells[1,0]="2";cells[2,0]="Text";
        cells[0,1]="=SUMME(A1:A3)*2";Equal(7.0,Sheet.Value(cells,0,1,new HashSet<string>()));
        cells[1,1]="=AVERAGE(A1:A3)";Equal(1.75,Sheet.Value(cells,1,1,new HashSet<string>()));
        cells[2,1]="=-(2+3)*4+10/2";Equal(-15.0,Sheet.Value(cells,2,1,new HashSet<string>()));
        cells[3,1]="=1/0";Error(delegate { Sheet.Value(cells,3,1,new HashSet<string>()); },"#DIV/0!");
        cells[4,1]="=B6";cells[5,1]="=B5";Error(delegate { Sheet.Value(cells,4,1,new HashSet<string>()); },"#ZIRKEL!");
        cells[6,1]="=A99";Error(delegate { Sheet.Value(cells,6,1,new HashSet<string>()); },"#BEZUG!");
        cells[7,1]="=SUMME(A1:A3)+MIN(A1:A2)+MAX(A1:A2)";Equal(7.0,Sheet.Value(cells,7,1,new HashSet<string>()));
        cells[8,1]="=2xyz";Error(delegate { Sheet.Value(cells,8,1,new HashSet<string>()); },"#FORMEL!");
        string directory=Path.Combine(Path.GetTempPath(),"byby-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        string textPath=Path.Combine(directory,"save.txt");
        Ui.AtomicWrite(textPath,delegate(string temp){File.WriteAllText(temp,"alt");});
        Ui.AtomicWrite(textPath,delegate(string temp){File.WriteAllText(temp,"neu");});Equal("neu",File.ReadAllText(textPath));
        try { Ui.AtomicWrite(textPath,delegate(string temp){File.WriteAllText(temp,"unvollständig");throw new IOException("Test");}); }catch(IOException){}
        Equal("neu",File.ReadAllText(textPath));
        string docx=Path.Combine(directory,"sample.docx");
        using(var zip=ZipFile.Open(docx,ZipArchiveMode.Create))Entry(zip,"word/document.xml","<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>Hallo</w:t></w:r><w:r><w:t> Welt</w:t></w:r></w:p><w:p><w:r><w:t>Absatz</w:t></w:r></w:p></w:body></w:document>");
        Equal("Hallo Welt"+Environment.NewLine+"Absatz",Formats.ReadDocx(docx));
        string xlsx=Path.Combine(directory,"sample.xlsx");
        using(var zip=ZipFile.Open(xlsx,ZipArchiveMode.Create))
        {
            Entry(zip,"xl/workbook.xml","<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Test' r:id='r1'/></sheets></workbook>");
            Entry(zip,"xl/_rels/workbook.xml.rels","<Relationships><Relationship Id='r1' Target='worksheets/sheet1.xml'/></Relationships>");
            Entry(zip,"xl/sharedStrings.xml","<sst xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><si><t>Grüße</t></si></sst>");
            Entry(zip,"xl/worksheets/sheet1.xml","<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='1'><c r='A1' t='s'><v>0</v></c><c r='B1'><f>2+2</f><v>4</v></c><c r='C1'><f>NOW()</f></c><c r='D1' t='inlineStr'><is><t>Inline</t></is></c></row></sheetData></worksheet>");
        }
        var book=Formats.ReadXlsx(xlsx);Equal("Test",book[0].Name);Equal("Grüße",book[0].Cells[0,0]);Equal("4",book[0].Cells[0,1]);Equal("#KEIN_CACHE!",book[0].Cells[0,2]);Equal("Inline",book[0].Cells[0,3]);
        Console.WriteLine(count+" Prüfungen bestanden. Testdateien: "+directory);
    }
}
