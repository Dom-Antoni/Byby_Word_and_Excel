using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class EditorReviewChecks
{
    static int count;
    static void Assert(bool value,string name){if(!value)throw new Exception(name);count++;}
    static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {foreach(object child in LogicalTreeHelper.GetChildren(root)){var value=child as T;if(value!=null)yield return value;var dependency=child as DependencyObject;if(dependency!=null)foreach(var nested in Descendants<T>(dependency))yield return nested;}}
    static void Pump(DocumentEditor editor){editor.UpdateLayout();editor.Dispatcher.Invoke(new Action(delegate{}),DispatcherPriority.ApplicationIdle);}
    static void Type(DocumentEditor editor,string value)
    {
        var composition=new TextComposition(InputManager.Current,editor,value);
        editor.RaiseEvent(new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputEvent});
        editor.RaiseEvent(new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice,composition){RoutedEvent=TextCompositionManager.TextInputEvent});Pump(editor);
    }
    [STAThread]public static int Main()
    {
        try{Run();Console.WriteLine(count+" Editor- und Hinweisprüfungen bestanden.");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static void Run()
    {
        var editor=new DocumentEditor {Width=600,Height=300};var root=new AdornerDecorator {Child=editor};
        using(var host=new HwndSource(new HwndSourceParameters("Schreib-review-invisible"){Width=600,Height=300,PositionX=-32000,PositionY=-32000,WindowStyle=unchecked((int)0x80000000)}))
        {
            host.RootVisual=root;root.Measure(new Size(600,300));root.Arrange(new Rect(0,0,600,300));Pump(editor);
            var source=new Reference {Id="test-source",Title="Test",Year="2026"};
            editor.Text="Vor Nach";editor.Select(4,0);editor.ClearUndo();editor.InsertCitation(source,"(Test, 2026)");Pump(editor);
            var citation=editor.Citations().Single();Assert(editor.SelectionStart==citation.Start+citation.Length && editor.SelectionLength==0,"Cursor nach Einfügen hinter Quelle");
            Type(editor," weiter");Assert(editor.Text=="Vor (Test, 2026) weiterNach","Weiterschreiben nach Quelle");Assert(editor.Citations().Single().Length==citation.Length,"Neuer Text gehört nicht zur Quelle");
            editor.Select(citation.Start+3,0);Assert(editor.SelectionStart==citation.Start+citation.Length,"Cursor im Verweis springt ans Ende");
            EditingCommands.MoveLeftByCharacter.Execute(null,editor);Assert(editor.SelectionStart==citation.Start,"Pfeil links überspringt Quelle");
            EditingCommands.MoveRightByCharacter.Execute(null,editor);Assert(editor.SelectionStart==citation.Start+citation.Length,"Pfeil rechts überspringt Quelle");
            editor.ClearUndo();EditingCommands.Backspace.Execute(null,editor);Assert(editor.Citations().Count==0 && editor.Text=="Vor  weiterNach","Rücktaste entfernt vollständige Quelle");
            editor.Undo();Assert(editor.Citations().Count==1 && editor.Text=="Vor (Test, 2026) weiterNach","Entfernen einer Quelle mit einem Rückgängig rückgängig");
            editor.Select(citation.Start,0);EditingCommands.Delete.Execute(null,editor);Assert(editor.Citations().Count==0 && editor.Text=="Vor  weiterNach","Entf vor Quelle entfernt ganzen Verweis");
            editor.Undo();editor.Select(citation.Start+2,2);Assert(editor.SelectionStart==citation.Start && editor.SelectionLength==citation.Length,"Teilauswahl umfasst ganze Quelle");
            editor.SelectedText="ersetzt";Assert(editor.Text=="Vor ersetzt weiterNach" && editor.Citations().Count==0,"Ersetzen einer Teilauswahl hinterlässt keinen Quellenrest");
            editor.Text="Vor (Quelle) danach";editor.Select(4,8);editor.InsertCitation(source,"(Quelle)");editor.Select(13,0);EditingCommands.DeletePreviousWord.Execute(null,editor);Assert(editor.Citations().Count==0 && !editor.Text.Contains("Quelle"),"Strg+Rücktaste über Leerraum entfernt Quelle vollständig ("+editor.Text+")");
            editor.Text="Vor Nach";editor.Select(4,0);editor.InsertCitation(source,"(Quelle)");editor.Select(6,2);Type(editor,"neu");Assert(editor.Text=="Vor neuNach" && editor.Citations().Count==0,"Tippen über Teilauswahl ersetzt ganze Quelle");
            editor.Text="Text ";editor.Select(5,0);editor.InsertCitation(source,"[1]");editor.ClearUndo();Type(editor," danach");editor.ApplyAppearance(true);editor.Undo();Assert(editor.Text=="Text [1]","Darstellungswechsel mit Quelle erzeugt keine Undo-Einträge");
            // Rendering and exporting must not turn proofing into persistent formatting.
            editor.Text="Tippfehler Grammatik Stilhinweis\r\nZweite Zeile";editor.ApplyAppearance(false);editor.ClearUndo();string rtf=editor.SaveRtf();int changes=0;editor.TextChanged+=delegate{changes++;};
            var marks=new[]{new ProofMark {Start=0,Length=10,Kind="spelling"},new ProofMark {Start=11,Length=9,Kind="grammar"},new ProofMark {Start=21,Length=11,Kind="style"}};
            editor.SetProofMarks(marks);Pump(editor);Assert(changes==0 && !editor.CanUndo,"Hinweisfarben ändern weder Text noch Rückgängig");Assert(editor.SaveRtf()==rtf,"Hinweisfarben landen nicht im RTF");
            string folder=Path.Combine(Path.GetTempPath(),"byby-review-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            foreach(bool dark in new[]{false,true})
            {
                editor.ApplyAppearance(dark);Pump(editor);var bitmap=new RenderTargetBitmap(600,300,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(folder,dark?"dark.png":"light.png")))encoder.Save(stream);
                byte[] pixels=new byte[600*300*4];bitmap.CopyPixels(pixels,600*4,0);int red=0,blue=0,yellow=0;
                for(int i=0;i<pixels.Length;i+=4){int b=pixels[i],g=pixels[i+1],r=pixels[i+2];if(r>140 && r>g*1.5 && r>b*1.5)red++;if(b>140 && b>r*1.4 && b>g*1.1)blue++;if(r>130 && g>80 && b<100 && r>g)yellow++;}
                Assert(red>10 && blue>10 && yellow>10,"Alle drei Hinweisfarben sichtbar auf "+(dark?"dunklem":"hellem")+" Papier ("+red+", "+blue+", "+yellow+")");
            }
            Console.WriteLine("Hinweisvorschau: "+folder);
            editor.Text="Start "+string.Join(" ",Enumerable.Repeat("langesWort",100));editor.ApplyAppearance(false);editor.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
            editor.SetProofMarks(new[]{new ProofMark {Start=6,Length=editor.Text.Length-6,Kind="style"}});Pump(editor);
            var wrapped=new RenderTargetBitmap(600,300,96,96,PixelFormats.Pbgra32);wrapped.Render(root);byte[] wrappedPixels=new byte[600*300*4];wrapped.CopyPixels(wrappedPixels,600*4,0);int markedRows=0;
            for(int y=0;y<300;y++){bool marked=false;for(int x=0;x<600;x++){int i=(y*600+x)*4;if(wrappedPixels[i+2]>130 && wrappedPixels[i+1]>80 && wrappedPixels[i]<100){marked=true;break;}}if(marked)markedRows++;}
            Assert(markedRows>5,"Langer Hinweis wird auf mehreren umgebrochenen Zeilen markiert");
            editor.ScrollToEnd();Pump(editor);var scrolled=new RenderTargetBitmap(600,300,96,96,PixelFormats.Pbgra32);scrolled.Render(root);var scrolledEncoder=new PngBitmapEncoder();scrolledEncoder.Frames.Add(BitmapFrame.Create(scrolled));using(var stream=File.Create(Path.Combine(folder,"scrolled.png")))scrolledEncoder.Save(stream);
            editor.ScrollToHome();
            editor.Text="Das ist eigentlich klar.";
            using(var assistant=new WritingPanel(editor,new WritingSettings()))
            {
                editor.TextChanged+=delegate{if(!editor.PresentationChanging)assistant.Invalidate();};
                assistant.Check();var list=Descendants<ListBox>(assistant).Single(x=>x.Items.Cast<object>().OfType<ListBoxItem>().Any(i=>i.Tag is WritingIssue));
                list.SelectedItem=list.Items.Cast<ListBoxItem>().Single(x=>((WritingIssue)x.Tag).Rule=="filler");
                Descendants<Button>(assistant).Single(x=>Convert.ToString(x.Content)=="Ignorieren").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert(assistant.DismissedHints.Count==1 && !list.Items.Cast<ListBoxItem>().Any(x=>((WritingIssue)x.Tag).Rule=="filler"),"Ignorieren in der echten Seitenleiste blendet Hinweis aus");
                assistant.Check();Assert(!list.Items.Cast<ListBoxItem>().Any(x=>((WritingIssue)x.Tag).Rule=="filler"),"Manuelles Nachprüfen zeigt ignorierten Hinweis nicht erneut");
                editor.Select(0,0);editor.SelectedText="Heute: ";assistant.Check();Assert(assistant.DismissedHints.Single().Start==editor.Text.IndexOf("eigentlich",StringComparison.Ordinal) && !list.Items.Cast<ListBoxItem>().Any(x=>((WritingIssue)x.Tag).Rule=="filler"),"Automatische Invalidierung behält Ignorieren nach anderer Textänderung");
                string json=new JavaScriptSerializer().Serialize(new WritingDocument {Rtf=editor.SaveRtf(),IgnoredHints=assistant.DismissedHints});
                var saved=new JavaScriptSerializer().Deserialize<WritingDocument>(json);assistant.ResetDocument(saved.IgnoredHints);assistant.Check();Assert(!list.Items.Cast<ListBoxItem>().Any(x=>((WritingIssue)x.Tag).Rule=="filler"),"Dokument-Rundlauf erhält ignorierte Hinweise");
                assistant.ResetDocument();assistant.Check();Assert(list.Items.Cast<ListBoxItem>().Any(x=>((WritingIssue)x.Tag).Rule=="filler"),"Neues Dokument erhält eigene Hinweisliste");
                editor.Select(editor.Text.IndexOf("eigentlich",StringComparison.Ordinal),10);list.SelectedItem=list.Items.Cast<ListBoxItem>().Single(x=>((WritingIssue)x.Tag).Rule=="filler");
                Descendants<Button>(assistant).Single(x=>Convert.ToString(x.Content)=="Übernehmen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));assistant.Check();Assert(!editor.Text.Contains("eigentlich") && !list.Items.Cast<ListBoxItem>().Any(x=>((WritingIssue)x.Tag).Rule=="filler"),"Korrigierter Hinweis bleibt nach erneuter Prüfung weg");
            }
        }
    }
}
