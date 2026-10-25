using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;

internal static class ViewFilesChecks
{
    static int count;
    static void Assert(bool condition,string name){if(!condition)throw new Exception(name);count++;}
    static string Text(FlowDocument document){return new TextRange(document.ContentStart,document.ContentEnd).Text;}
    static void Entry(ZipArchive zip,string name,string content){using(var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false)))writer.Write(content);}
    [STAThread]public static int Main()
    {
        try{Run();Console.WriteLine(count+" Ansichts- und Importprüfungen bestanden.");return 0;}catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
    }
    static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"byby-view-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string textPath=Path.Combine(folder,"legacy.txt");File.WriteAllBytes(textPath,Encoding.GetEncoding(1252).GetBytes("Grüße – schön"));Assert(DocumentFiles.ReadText(textPath)=="Grüße – schön","Windows-1252 lesbar");
        File.WriteAllText(textPath,"Unicode 😀",new UTF8Encoding(true));Assert(DocumentFiles.ReadText(textPath)=="Unicode 😀","UTF-8 mit BOM");
        File.WriteAllText(textPath,"Grüße aus Köln",Encoding.Unicode);Assert(DocumentFiles.ReadText(textPath)=="Grüße aus Köln","UTF-16 mit BOM");
        File.WriteAllBytes(textPath,Encoding.Unicode.GetBytes("Plain UTF16 without BOM"));Assert(DocumentFiles.ReadText(textPath)=="Plain UTF16 without BOM","UTF-16 ohne BOM");
        File.WriteAllBytes(textPath,new byte[]{0,1,2,0,5});bool rejected=false;try{DocumentFiles.ReadText(textPath);}catch(FormatException){rejected=true;}Assert(rejected,"Binärdatei abgewiesen");
        var html=DocumentFiles.Html("<!doctype html><html><head><title>Hidden</title><style>secret css</style></head><body><h1>Überschrift</h1><p>Ein <strong>fetter</strong> und <em>kursiver</em> Text &amp; mehr.<br>Zweite Zeile.</p><script>secret js</script><ul><li>Eintrag</li></ul><img src='https://not-loaded.invalid/test.png' alt='Beschreibung'></body></html>");
        string content=Text(html);Assert(content.Contains("Überschrift") && content.Contains("Text & mehr.") && content.Contains("Zweite Zeile."),"HTML-Text, Entitäten und Umbrüche");Assert(!content.Contains("secret") && !content.Contains("doctype") && !content.Contains("Hidden"),"Keine Skripte, Styles oder Kopfzeile als Text");
        Assert(content.Contains("• Eintrag") && content.Contains("[Bild: Beschreibung]"),"HTML-Liste und Bildbeschreibung");Assert(((Paragraph)html.Blocks.FirstBlock).FontWeight==FontWeights.Bold,"HTML-Überschrift formatiert");
        string htmlPath=Path.Combine(folder,"page.html");File.WriteAllBytes(htmlPath,Encoding.GetEncoding(1252).GetBytes("<meta charset='windows-1252'><p>Grüße</p>"));Assert(Text(DocumentFiles.Read(htmlPath).Document).Contains("Grüße"),"HTML-Zeichensatz berücksichtigt");
        string docx=Path.Combine(folder,"sample.docx");using(var zip=ZipFile.Open(docx,ZipArchiveMode.Create))
        {
            Entry(zip,"word/document.xml","<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:pPr><w:pStyle w:val='Heading1'/></w:pPr><w:r><w:t>Titel</w:t></w:r></w:p><w:p><w:r><w:rPr><w:b/><w:i/></w:rPr><w:t>Fetter Text 😀</w:t></w:r></w:p><w:tbl><w:tr><w:tc><w:p><w:r><w:t>Zelle A</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Zelle B</w:t></w:r></w:p></w:tc></w:tr></w:tbl></w:body></w:document>");
            Entry(zip,"word/footnotes.xml","<w:footnotes xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:footnote w:id='1'><w:p><w:r><w:t>Fußnotentext</w:t></w:r></w:p></w:footnote></w:footnotes>");
        }
        var word=DocumentFiles.Read(docx);Assert(word.Converted && Text(word.Document).Contains("Fetter Text 😀"),"DOCX-Text und Importstatus");Assert(((Paragraph)word.Document.Blocks.FirstBlock).FontWeight==FontWeights.Bold,"Word-Überschrift");var styled=((Paragraph)word.Document.Blocks.ElementAt(1)).Inlines.FirstInline as Span;Assert(styled!=null && styled.FontWeight==FontWeights.Bold && styled.FontStyle==FontStyles.Italic,"Word-Fett/Kursiv erhalten");Assert(Text(word.Document).Contains("Zelle A   |   Zelle B") && Text(word.Document).Contains("Fußnotentext"),"Word-Tabelle und Fußnoten lesbar");
        string odt=Path.Combine(folder,"sample.odt");using(var zip=ZipFile.Open(odt,ZipArchiveMode.Create))Entry(zip,"content.xml","<office:document-content xmlns:office='urn:oasis:names:tc:opendocument:xmlns:office:1.0' xmlns:text='urn:oasis:names:tc:opendocument:xmlns:text:1.0'><office:body><office:text><text:h>ODT-Titel</text:h><text:p>Ein<text:s text:c='2'/>Text<text:line-break/>Zeile</text:p></office:text></office:body></office:document-content>");Assert(Text(DocumentFiles.Read(odt).Document).Contains("Ein  Text"),"ODT-Text und Leerzeichen");
        rejected=false;try{DocumentFiles.Read(Path.Combine(folder,"old.doc"));}catch(FormatException ex){rejected=ex.Message.Contains("DOCX");}Assert(rejected,"Altes DOC mit verständlichem Hinweis");
        var window=new Window();Ui.Window(window,"Theme-Test");string mode=Theme.Current.Mode;bool paper=Theme.Current.DarkDocument;
        Theme.Set("Dark",true,false);Assert(Theme.Dark && ((SolidColorBrush)Theme.Color("App.Text")).Color.R>200,"Dunkle Palette");
        var baseline=new DocumentEditor();var baselineHost=Host(baseline);baseline.Text="Unveränderter Text";baseline.ClearUndo();baseline.BeginChange();baseline.Select(baseline.Text.Length,0);baseline.SelectedText=" ergänzt";baseline.EndChange();baseline.Undo();Console.WriteLine("Rückgängig ohne Darstellungswechsel: ["+baseline.Text+"]");
        var editor=new DocumentEditor();var editorHost=Host(editor);editor.Text="Unveränderter Text";editor.ClearUndo();editor.BeginChange();editor.Select(editor.Text.Length,0);editor.SelectedText=" ergänzt";editor.EndChange();
        string before=editor.Text,light=editor.SaveRtf();editor.ApplyAppearance(true);Assert(editor.Text==before,"Dunkle Dokumentfläche ändert keinen Text");Assert(editor.SaveRtf()==light,"Darstellung verändert keine gespeicherte Formatierung");editor.Undo();Assert(editor.Text=="Unveränderter Text","Darstellungswechsel erhält Rückgängig (erhalten: "+editor.Text+")");
        Theme.Set("Light",false,false);Assert(!Theme.Dark && ((SolidColorBrush)Theme.Color("App.Text")).Color.R<100,"Helle Palette");Theme.Set(mode,paper,false);
        // Render representative real controls offscreen; no desktop window or user document is touched.
        foreach(string appearance in new[]{"Light","Dark"})
        {
            Theme.Set(appearance,true,false);var panel=new StackPanel {Width=640,Height=260,Margin=new Thickness(20)};Theme.Bind(panel,Panel.BackgroundProperty,"App.Panel");window.Content=panel;panel.Children.Add(new TextBlock {Text="Schreib · Datei  Bearbeiten  Format  Referenzen  Prüfen  Ansicht",FontSize=16,Foreground=Theme.Color("App.Text"),Margin=new Thickness(10)});var row=new StackPanel {Orientation=Orientation.Horizontal};panel.Children.Add(row);Ui.Button(row,"Speichern",delegate{});Ui.Button(row,"Rückgängig",delegate{});panel.Children.Add(new TextBox {Text="Grüße – Dokumentansicht",Margin=new Thickness(10),Padding=new Thickness(10)});var box=new ComboBox {ItemsSource=new[]{"Hell","Dunkel"},SelectedIndex=appearance=="Dark"?1:0,Margin=new Thickness(10)};panel.Children.Add(box);panel.Measure(new Size(680,300));panel.Arrange(new Rect(0,0,680,300));panel.UpdateLayout();var bitmap=new RenderTargetBitmap(680,300,96,96,PixelFormats.Pbgra32);bitmap.Render(panel);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(folder,"theme-"+appearance+".png")))encoder.Save(file);
        }
        Theme.Set(mode,paper,false);editorHost.Dispose();baselineHost.Dispose();Console.WriteLine("Darstellungsproben: "+folder);
    }
    static HwndSource Host(DocumentEditor editor){var source=new HwndSource(new HwndSourceParameters("Schreib-invisible-test"){Width=680,Height=300,PositionX=-32000,PositionY=-32000,WindowStyle=unchecked((int)0x80000000)});source.RootVisual=editor;editor.Measure(new Size(680,300));editor.Arrange(new Rect(0,0,680,300));editor.UpdateLayout();editor.Dispatcher.Invoke(new Action(delegate{}),DispatcherPriority.ApplicationIdle);return source;}
}
