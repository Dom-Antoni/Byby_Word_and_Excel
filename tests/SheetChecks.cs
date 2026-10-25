using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class SheetChecks
{
    static int count;
    static void Assert(bool value,string message){if(!value)throw new Exception(message);count++;}
    static void Entry(ZipArchive zip,string name,string text){using(var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false)))writer.Write(text);}
    static T Field<T>(Tabelle window,string name){return (T)typeof(Tabelle).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window);}
    static MenuItem Menu(ItemsControl parent,string name)
    {
        foreach(object item in parent.Items){var menu=item as MenuItem;if(menu==null)continue;if(Convert.ToString(menu.Header)==name)return menu;var child=Menu(menu,name);if(child!=null)return child;}return null;
    }
    static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T)yield return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children<T>(VisualTreeHelper.GetChild(root,i)))yield return child;
    }
    static void Click(MenuItem menu,bool value){menu.IsChecked=value;menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));}
    [STAThread]public static int Main()
    {
        try{Run();Console.WriteLine(count+" Tabellenimport- und Oberflächenprüfungen bestanden.");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"byby-sheet-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        Assert(SheetFiles.Detect("Name;Wert\nA;1,25\nB;2,50")==';',"Dezimal-Komma nicht als Trennzeichen");
        Assert(SheetFiles.Detect("Name,Wert\nA,12\nB,13")==',',"Komma erkannt");
        Assert(SheetFiles.Detect("Name\tWert\nA\t12\nB\t13")=='\t',"Tabulator erkannt");
        Assert(SheetFiles.Detect("\"Text, mit Komma\";Wert\n\"Zeile\nmit, Komma\";2")==';',"Zitate und mehrzeilige Felder");
        Assert(SheetFiles.Detect("Einzelne Spalte\nNoch eine")==';',"Einspaltige Datei");
        var imported=SheetFiles.FromText("sep=,\r\nName,Wert\r\n\"A,B\",12\r\n",null,"Test");
        Assert(imported.Separator==',' && imported.Sheets[0].Cells[1,0]=="A,B","Excel-sep-Direktive");
        Assert(imported.Sheets[0].Cells.GetLength(0)==2 && imported.Sheets[0].Cells.GetLength(1)==2,"Keine künstlichen Leerzeilen oder Leerspalten");
        Assert(SheetFiles.FromText("A,B\n1,2",';',"Test").Sheets[0].Cells.GetLength(1)==1,"Manuelle Trennzeichenwahl");
        Assert(SheetFiles.FromText("\ufeffName\tWert\nA\t1",null,"Test").Separator=='\t',"BOM vor Spaltennamen");
        string csv=Path.Combine(folder,"sample.csv");File.WriteAllBytes(csv,Encoding.GetEncoding(1252).GetBytes("Stadt;Wert\nKöln;1,5\nMünchen;=Text"));
        Assert(SheetFiles.Read(csv).Sheets[0].Cells[1,0]=="Köln","Ältere CSV-Zeichenkodierung");
        string tab=Path.Combine(folder,"sample.txt");File.WriteAllText(tab,"Name\tWert\nA\t42",Encoding.Unicode);Assert(SheetFiles.Read(tab).Separator=='\t',"UTF-16 mit automatischer Erkennung");
        string xlsx=Path.Combine(folder,"date.xlsx");
        using(var zip=ZipFile.Open(xlsx,ZipArchiveMode.Create))
        {
            Entry(zip,"xl/workbook.xml","<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Daten' r:id='r1'/><sheet name='Zweites Blatt' r:id='r2'/></sheets></workbook>");
            Entry(zip,"xl/_rels/workbook.xml.rels","<Relationships><Relationship Id='r1' Target='../xl/worksheets/sheet1.xml'/><Relationship Id='r2' Target='/xl/worksheets/sheet2.xml'/></Relationships>");
            Entry(zip,"xl/styles.xml","<styleSheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><numFmts><numFmt numFmtId='164' formatCode='dd.mm.yyyy'/></numFmts><cellXfs><xf numFmtId='0'/><xf numFmtId='14'/><xf numFmtId='20'/><xf numFmtId='164'/></cellXfs></styleSheet>");
            Entry(zip,"xl/worksheets/sheet1.xml","<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>Datum</t></is></c><c r='B1' t='inlineStr'><is><t>Wert</t></is></c></row><row r='2'><c r='A2' s='1'><v>45292</v></c><c r='B2'><f>2+2</f><v>4</v></c><c r='C2' s='2'><v>0.5</v></c><c r='D2' s='3'><v>45293</v></c></row></sheetData></worksheet>");
            Entry(zip,"xl/worksheets/sheet2.xml","<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>Blatt zwei</t></is></c></row></sheetData></worksheet>");
        }
        var book=SheetFiles.Read(xlsx);
        Assert(book.Sheets.Count==2 && book.Sheets[1].Name=="Zweites Blatt","Mehrere XLSX-Blätter und normalisierte Verweise");
        Assert(book.Sheets[0].Cells[1,0]=="01.01.2024","Excel-Datum angezeigt");
        Assert(book.Sheets[0].Cells[1,2]=="12:00:00" && book.Sheets[0].Cells[1,3]=="02.01.2024","Uhrzeit und benutzerdefiniertes Datumsformat");
        Assert(book.Sheets[0].Cells[1,1]=="4","Gespeichertes Formelergebnis");
        Assert(book.Sheets[0].Cells.GetLength(0)==2 && book.Sheets[0].Cells.GetLength(1)==4,"Tatsächliche Excel-Dimensionen");
        string ods=Path.Combine(folder,"sample.ods");
        string odf="<office:document-content xmlns:office='urn:oasis:names:tc:opendocument:xmlns:office:1.0' xmlns:table='urn:oasis:names:tc:opendocument:xmlns:table:1.0' xmlns:text='urn:oasis:names:tc:opendocument:xmlns:text:1.0'><office:body><office:spreadsheet><table:table table:name='Daten'><table:table-row><table:table-cell office:value-type='string'><text:p>Name</text:p></table:table-cell><table:table-cell><text:p>Wert</text:p></table:table-cell></table:table-row><table:table-row table:number-rows-repeated='2'><table:table-cell><text:p>Ein<text:s text:c='2'/>Text</text:p></table:table-cell><table:table-cell office:value-type='float' office:value='3.5' table:number-columns-repeated='2'/></table:table-row><table:table-row table:number-rows-repeated='1048573'><table:table-cell table:number-columns-repeated='16384'/></table:table-row></table:table><table:table table:name='Weiteres Blatt'><table:table-row><table:table-cell office:value-type='boolean' office:boolean-value='true'/><table:table-cell table:formula='of:=1+1'/></table:table-row></table:table></office:spreadsheet></office:body></office:document-content>";
        using(var zip=ZipFile.Open(ods,ZipArchiveMode.Create))Entry(zip,"content.xml",odf);
        book=SheetFiles.Read(ods);Assert(book.Sheets.Count==2,"Mehrere ODS-Blätter");
        Assert(book.Sheets[0].Cells.GetLength(0)==3 && book.Sheets[0].Cells.GetLength(1)==3,"ODS-Wiederholungen ohne riesige leere Restbereiche");
        Assert(book.Sheets[0].Cells[2,0]=="Ein  Text" && book.Sheets[0].Cells[2,2]=="3.5","ODS-Text und wiederholte Werte");
        Assert(book.Sheets[1].Cells[0,0]=="WAHR" && book.Sheets[1].Cells[0,1]=="#KEIN_CACHE!","ODS-Boolesche Werte und fehlender Formelcache");
        string fods=Path.Combine(folder,"sample.fods");File.WriteAllText(fods,odf);Assert(SheetFiles.Read(fods).Sheets.Count==2,"Flaches OpenDocument");
        string xml=Path.Combine(folder,"sample.xls");File.WriteAllText(xml,"<Workbook xmlns='urn:schemas-microsoft-com:office:spreadsheet' xmlns:ss='urn:schemas-microsoft-com:office:spreadsheet'><Worksheet ss:Name='XML'><Table><Row><Cell><Data ss:Type='String'>Name</Data></Cell></Row><Row ss:Index='3'><Cell ss:Index='2'><Data ss:Type='Number'>42</Data></Cell></Row></Table></Worksheet></Workbook>");
        book=SheetFiles.Read(xml);Assert(book.Sheets[0].Cells[2,1]=="42" && book.Sheets[0].Cells[1,0]==null,"Excel-XML mit ausgelassenen Zeilen und Spalten");
        File.WriteAllBytes(xml,new byte[]{0xd0,0xcf,0x11,0xe0});bool rejected=false;try{SheetFiles.Read(xml);}catch(FormatException ex){rejected=ex.Message.Contains("XLSX");}Assert(rejected,"Binäres XLS mit verständlichem Hinweis");
        string html=Path.Combine(folder,"tables.html");File.WriteAllText(html,"<html><script><table><tr><td>Nicht zeigen</td></tr></table></script><table><caption>Messwerte</caption><tr><th>Name</th><th colspan='2'>Werte</th></tr><tr><td rowspan='2'>A &amp; B</td><td>3</td><td>4</td></tr><tr><td>5</td><td>6</td></tr></table><table><tr><td>Zweites</td></tr></table></html>");
        book=SheetFiles.Read(html);Assert(book.Sheets.Count==2 && book.Sheets[0].Name=="Messwerte","HTML-Tabellen ohne Skriptinhalt");
        Assert(book.Sheets[0].Cells[1,0]=="A & B" && book.Sheets[0].Cells[2,1]=="5","HTML-Entitäten und verbundene Zellen");
        rejected=false;try{SheetFiles.Bounds(10001,2);}catch(FormatException){rejected=true;}Assert(rejected,"Zeilenlimit verständlich begrenzt");
        string bad=Path.Combine(folder,"bad.xml");File.WriteAllText(bad,"<!DOCTYPE Workbook [<!ENTITY test SYSTEM 'file:///C:/Windows/win.ini'>]><Workbook>&test;</Workbook>");
        rejected=false;try{SheetFiles.Read(bad);}catch(System.Xml.XmlException){rejected=true;}Assert(rejected,"XML-DTD wird nicht ausgewertet");
        UiChecks(folder,csv,xlsx);
        Console.WriteLine("Testdateien und Ansichtsproben: "+folder);
    }
    static void UiChecks(string folder,string csv,string xlsx)
    {
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};var window=new Tabelle();
        window.OpenPath(csv);var grid=Field<DataGrid>(window,"grid");var sheets=Field<TabControl>(window,"worksheets");var formula=Field<TextBox>(window,"formula");
        Assert(grid.IsReadOnly && formula.IsReadOnly,"Datei öffnet in geschützter Leseansicht");
        Assert(grid.Columns[0].Header.ToString().Contains("Stadt") && grid.Columns[0].Header.ToString().StartsWith("A\n"),"Spaltenbuchstabe und Datenname im Kopf");
        Assert(grid.Items.Count==2 && ((DataRowView)grid.Items[0])[0].ToString()=="Köln","Kopfzeile fest, Daten unverändert");
        Assert(((DataRowView)grid.Items[1])[1].ToString()=="=Text","Importierte Textwerte werden nicht automatisch als Formeln ausgeführt");
        Assert(grid.MinRowHeight>=38 && grid.Columns[0].ActualWidth>=0 && grid.Columns[0].Width.Value>=140,"Mehr Platz für Zeilen und Spalten");
        var root=(FrameworkElement)window.Content;window.Content=null;
        // A detached offscreen root is not in Application.Windows. Register resources
        // locally so live resource changes invalidate it just as they do a real Window.
        root.Resources.MergedDictionaries.Add(app.Resources.MergedDictionaries[0]);
        root.SetResourceReference(TextBlock.ForegroundProperty,"App.Text");root.SetResourceReference(Panel.BackgroundProperty,"App.Background");root.SetValue(TextBlock.FontSizeProperty,window.FontSize);root.SetValue(TextBlock.FontFamilyProperty,window.FontFamily);
        using(var host=new HwndSource(new HwndSourceParameters("Tabelle-invisible-test"){Width=1220,Height=780,PositionX=-32000,PositionY=-32000,WindowStyle=unchecked((int)0x80000000)}))
        {
            host.RootVisual=root;Layout(root);var menu=Children<Menu>(root).First();
            Assert(menu.Items.Count==4,"Funktionsmenüs Datei, Bearbeiten, Ansicht und Daten");
            var row=grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;Assert(row!=null && row.Header.ToString()=="2","Zeilennummern entsprechen der Originaldatei");
            Click(Menu(menu,"Erste Spalte beim Scrollen festhalten"),true);Assert(grid.FrozenColumnCount==1,"Menü fixiert erste Spalte");
            Click(Menu(menu,"Erste Zeile als Spaltennamen"),false);Assert(grid.Items.Count==3,"Kopfzeile lässt sich als normale Datenzeile anzeigen");
            Click(Menu(menu,"Bearbeitung aktivieren"),true);Assert(!grid.IsReadOnly && !formula.IsReadOnly,"Bearbeitungsmenü funktioniert");
            window.SelectCell(1,1);formula.Text="=2*3";Children<Button>(root).First(x=>Convert.ToString(x.Content)=="Übernehmen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Click(Menu(menu,"Ergebnisse statt Formeln anzeigen"),true);Assert(((DataRowView)grid.Items[1])[1].ToString()=="6","Einfache Berechnung über Formelzeile");
            window.OpenPath(xlsx);sheets.SelectedIndex=1;Assert(grid.Columns.Count==1 && grid.Columns[0].Header.ToString().Contains("Blatt zwei"),"Blattreiter wechseln Inhalt");
            sheets.SelectedIndex=0;Click(Menu(menu,"Bearbeitung aktivieren"),true);window.SelectCell(1,1);formula.Text="123";Children<Button>(root).First(x=>Convert.ToString(x.Content)=="Übernehmen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            sheets.SelectedIndex=1;sheets.SelectedIndex=0;Assert(((DataRowView)grid.Items[0])[1].ToString()=="123","Änderungen bleiben beim Blattwechsel erhalten");
            string large=Path.Combine(folder,"large.tsv");
            using(var writer=new StreamWriter(large,false,new UTF8Encoding(true))){writer.WriteLine("ID\tName\tA\tB\tC\tD\tE\tF");for(int i=1;i<10000;i++)writer.WriteLine(i+"\tZeile "+i+"\t1\t2\t3\t4\t5\t6");}
            window.OpenPath(large);Layout(root);window.SelectCell(9999,5);Layout(root);
            var last=grid.ItemContainerGenerator.ContainerFromIndex(9998) as DataGridRow;
            Assert(last!=null && last.Header.ToString()=="10000","Bis zur letzten von 10.000 Zeilen scrollen");
            Assert(Children<DataGridRow>(grid).Count()<200,"Große Tabelle erzeugt nur sichtbare Zeilen als UI-Elemente");
            // Fresh test data for the full UI preview; never open a visible desktop window.
            string preview=Path.Combine(folder,"preview.csv");var lines=new List<string>{"Projekt;Status;Verantwortlich;Beginn;Aufwand (h);Notiz"};
            for(int i=0;i<35;i++)lines.Add("Projekt "+(i+1)+";"+(i%3==0?"Abgeschlossen":"In Arbeit")+";"+(i%2==0?"Müller":"Smith")+";25.10.2026;"+(i+2)+";"+(i%4==0?"Ein längerer Hinweis mit gut lesbarem Abstand in den Zellen.":"Kurzer Hinweis"));
            File.WriteAllLines(preview,lines,new UTF8Encoding(true));window.OpenPath(preview);
            Layout(root);
            var firstHeader=Children<DataGridColumnHeader>(grid).First(x=>Convert.ToString(x.Content).StartsWith("A\n"));
            double headerTop=firstHeader.TransformToAncestor(root).Transform(new Point(0,0)).Y;
            double headerLeft=firstHeader.TransformToAncestor(root).Transform(new Point(0,0)).X;
            window.SelectCell(30,5);Layout(root);
            var scrolledRow=grid.ItemContainerGenerator.ContainerFromIndex(29) as DataGridRow;
            Assert(scrolledRow!=null && scrolledRow.Header.ToString()=="31","Originale Zeilennummern nach vertikalem Scrollen");
            Assert(Math.Abs(firstHeader.TransformToAncestor(root).Transform(new Point(0,0)).Y-headerTop)<1,"Spaltennamen bleiben beim Scrollen oben sichtbar");
            Assert(Math.Abs(firstHeader.TransformToAncestor(root).Transform(new Point(0,0)).X-headerLeft)<1,"Fixierte erste Spalte bleibt beim horizontalen Scrollen stehen");
            window.SelectCell(1,0);Layout(root);
            string mode=Theme.Current.Mode;bool paper=Theme.Current.DarkDocument;
            foreach(string appearance in new[]{"Light","Dark"})
            {
                Theme.Set(appearance,paper,false);Layout(root);var bitmap=new RenderTargetBitmap(1220,780,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(folder,"Tabelle-"+appearance+".png")))encoder.Save(output);
                Assert(Children<DataGridColumnHeader>(grid).Any(x=>x.Content!=null && x.Content.ToString().Contains("Projekt")),"Spaltenkopf in "+appearance+" gerendert");
                byte[] pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(2,200,1,1),pixel,4,0);Assert(appearance=="Dark"?pixel[2]<50:pixel[2]>200,"Gerenderter Hintergrund folgt "+appearance);
            }
            Theme.Set(mode,paper,false);
        }
        window.Close();app.Shutdown();
    }
    static void Layout(FrameworkElement root){root.Measure(new Size(1220,780));root.Arrange(new Rect(0,0,1220,780));root.UpdateLayout();root.Dispatcher.Invoke(new Action(delegate{}),DispatcherPriority.ApplicationIdle);}
}
