using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

internal static class LayoutInteractionChecks
{
    static int count;static string folder;
    static void Assert(bool value,string label){if(!value)throw new Exception(label);count++;}
    static void Pump(){Dispatcher.CurrentDispatcher.Invoke(new Action(delegate{}),DispatcherPriority.ApplicationIdle);}
    static void Wait(Task task){var until=DateTime.UtcNow.AddSeconds(10);while(!task.IsCompleted && DateTime.UtcNow<until){Pump();Thread.Sleep(1);}Assert(task.IsCompleted,"Hintergrundarbeit endet");task.GetAwaiter().GetResult();}
    static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);}
    static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject {foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()){if(child is T)yield return (T)child;foreach(var nested in Children<T>(child))yield return nested;}}
    static HwndSource Host(FrameworkElement root,int width=1000,int height=700)
    {var host=new HwndSource(new HwndSourceParameters("Schreib-layout-offscreen"){Width=width,Height=height,PositionX=-32000,PositionY=-32000,WindowStyle=unchecked((int)0x80000000)});host.RootVisual=root;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();Pump();return host;}
    static void Image(FrameworkElement root,string name,int width=1000,int height=700)
    {var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(folder,name+".png")))encoder.Save(file);}
    [STAThread]public static int Main()
    {try{new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));folder=Path.Combine(Path.GetTempPath(),"byby-layout-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);Margins();Context();SingleFlight();Performance();DocumentRoundTrip();Console.WriteLine(count+" Layout-, Kontext- und Reaktionsprüfungen bestanden. Vorschau: "+folder);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
    static void Margins()
    {
        double mm;Assert(DocumentMargins.TryLength("2,5 cm",false,out mm) && mm==25,"Dezimalkomma und cm");Assert(DocumentMargins.TryLength("12 mm",true,out mm) && mm==12,"Explizite Einheit übersteuert Auswahl");
        Assert(!DocumentMargins.TryLength("NaN",true,out mm) && !DocumentMargins.TryLength("-1",true,out mm) && !DocumentMargins.TryLength("6",true,out mm),"Ungültige Randwerte abweisen");
        var editor=new DocumentEditor();editor.Text="Ein Text mit sichtbaren Rändern.\r\nZweiter Absatz bleibt erhalten.";var surface=new PageSurface(editor);
        using(var host=Host(surface))
        {
            int committed=0;editor.LayoutCommitted+=delegate{committed++;};string text=editor.Text;editor.ClearUndo();
            editor.SetMargins(new DocumentMargins {Left=10,Right=20,Top=15,Bottom=12},false);Pump();Assert(Math.Abs(editor.Padding.Left-DocumentMargins.Pixels(10))<.01,"Rand verändert das echte RichTextBox-Padding");Assert(editor.Text==text,"Ränder verändern keinen Text");
            Assert(committed==0,"Vorschau ist noch keine gespeicherte Layoutänderung");surface.SetMouseMargins(true);surface.DragMargin(0,DocumentMargins.Pixels(5));Assert(Math.Abs(editor.PageMargins.Left-15)<.01,"Linken Rand mit Maus verschieben");surface.DragMargin(1,DocumentMargins.Pixels(5));Assert(Math.Abs(editor.PageMargins.Right-15)<.01,"Rechten Rand mit Maus verschieben");surface.DragMargin(2,DocumentMargins.Pixels(5));surface.DragMargin(3,DocumentMargins.Pixels(2));Assert(editor.PageMargins.Top==20 && editor.PageMargins.Bottom==10,"Oben und unten per Maus verschieben");Assert(committed==4,"Mausänderungen markieren Dokument als geändert");
            var original=editor.PageMargins;var dialog=new PageMarginsWindow(editor);var fields=Children<TextBox>(dialog).ToArray();fields[0].Text="1,2";Assert(editor.PageMargins.Left==12,"Zahlenfeld zeigt live Vorschau");dialog.Close();Assert(editor.PageMargins.Same(original),"Abbrechen stellt vorherige Ränder wieder her");Assert(committed==4,"Abgebrochene Vorschau erzeugt keine zusätzliche Dokumentänderung");
            Pump();var firstGlyph=editor.Pointer(0).GetCharacterRect(LogicalDirection.Forward);Assert(Math.Abs(firstGlyph.Left-editor.Padding.Left)<6,"Text und Lineal verwenden denselben Rand, ohne doppeltes Padding ("+firstGlyph.Left+" / "+editor.Padding.Left+")");
            editor.Select(0,3);Assert(editor.SelectedText=="Ein","Positionen nach Randänderung korrekt");Assert(editor.SlowPointerCalls==0,"Normale Textpositionen nutzen den schnellen Index");int indexes=editor.PointerIndexBuilds,reads=editor.TextSnapshotBuilds;for(int i=0;i<100;i++){editor.Pointer(i%editor.Text.Length);var cached=editor.Text;}Assert(editor.PointerIndexBuilds==indexes && editor.TextSnapshotBuilds==reads,"Unveränderter Text verwendet Snapshot und Positionsindex wieder");
            editor.SetProofMarks(new[]{new ProofMark {Start=0,Length=3,Kind="spelling"},new ProofMark {Start=4,Length=4,Kind="grammar"},new ProofMark {Start=13,Length=9,Kind="style"}});Pump();Image(surface,"Seitenraender-hell");
            editor.ApplyAppearance(true);surface.RefreshRulers();Pump();Image(surface,"Seitenraender-dunkel");editor.ApplyAppearance(false);
            editor.Text=string.Join("\r\n",Enumerable.Repeat("Langer Absatz mit mehreren Wörtern, damit sich die Schreibfläche erweitert.",100));Pump();Assert(surface.ExtentHeight>surface.ViewportHeight,"Langer Text bleibt auf fortlaufendem Blatt scrollbar");editor.Select(editor.Text.Length,0);surface.RevealCaret();Pump();Assert(surface.VerticalOffset>0,"Textende lässt sich auf dem äußeren Blatt erreichen");
        }
    }
    static void Context()
    {
        var editor=new DocumentEditor {Width=600,Height=300};var root=new AdornerDecorator {Child=editor};
        using(var host=Host(root,600,300))using(var panel=new WritingPanel(editor,new WritingSettings(),delegate(string text,WritingSettings settings,CancellationToken cancel){var result=new GrammarResult {Status="Test"};int at=text.IndexOf("Feler",StringComparison.Ordinal);if(at>=0)result.Issues.Add(new WritingIssue {Start=at,Length=5,Original="Feler",Rule="lt:TEST_SPELL",Category="Rechtschreibung",Message="Ein Testfehler.",Replacements=new List<string>{"Fehler"}});return Task.FromResult(result);}))
        {
            editor.TextChanged+=delegate{panel.Invalidate();};editor.Text="Ein Feler ist eigentlich klar.";Wait(panel.CheckAsync());var menu=new ContextMenu();menu.Items.Add(new MenuItem {Header="Referenz hinzufügen…"});panel.AttachContextMenu(menu);int at=editor.Text.IndexOf("Feler",StringComparison.Ordinal);panel.PopulateContext(at+2,menu);
            Assert(menu.Items.OfType<MenuItem>().Any(x=>Convert.ToString(x.Header)=="Vorschlag: Fehler"),"Rechtsklick zeigt konkrete Rechtschreibkorrektur");panel.PopulateContext(at+2,menu);Assert(menu.Items.OfType<MenuItem>().Count(x=>Convert.ToString(x.Header)=="Vorschlag: Fehler")==1,"Wiederholtes Öffnen verdoppelt Vorschläge nicht");Assert(menu.Items.OfType<MenuItem>().Count(x=>Convert.ToString(x.Header)=="Referenz hinzufügen…")==1,"Quellenmenü bleibt erhalten");
            menu.Items.OfType<MenuItem>().Single(x=>Convert.ToString(x.Header)=="Vorschlag: Fehler").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert(editor.Text=="Ein Fehler ist eigentlich klar.","Kontextvorschlag ersetzt nur die richtige Fundstelle");Wait(panel.CheckAsync());
            panel.PopulateContext(editor.Text.IndexOf("eigentlich",StringComparison.Ordinal)+2,menu);menu.Items.OfType<MenuItem>().Single(x=>Convert.ToString(x.Header)=="Diesen Hinweis ignorieren").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Wait(panel.CheckAsync());Assert(panel.DismissedHints.Count==1,"Ignorieren im Kontextmenü bleibt bei Folgeprüfung erhalten");
            editor.Text="Ein Feler.";Wait(panel.CheckAsync());editor.IsReadOnly=true;panel.PopulateContext(6,menu);Assert(!menu.Items.OfType<MenuItem>().Single(x=>Convert.ToString(x.Header)=="Vorschlag: Fehler").IsEnabled,"Lesemodus deaktiviert Korrektur im Kontextmenü");editor.IsReadOnly=false;
        }
    }
    static void SingleFlight()
    {
        var editor=new DocumentEditor();var delayed=new TaskCompletionSource<GrammarResult>();int requests=0;
        using(var panel=new WritingPanel(editor,new WritingSettings(),delegate(string text,WritingSettings settings,CancellationToken cancel){requests++;return requests==1?delayed.Task:Task.FromResult(new GrammarResult {Status="Fertig"});}))
        {
            editor.TextChanged+=delegate{panel.Invalidate();};editor.Text="Alter Text.";var first=panel.CheckAsync();var until=DateTime.UtcNow.AddSeconds(5);while(requests==0 && DateTime.UtcNow<until){Pump();Thread.Sleep(1);}Assert(requests==1,"Grammatik läuft im Hintergrund an");
            editor.Text="Neuer Text.";var second=panel.CheckAsync(true);var third=panel.CheckAsync(true);Assert(object.ReferenceEquals(first,second) && object.ReferenceEquals(first,third) && requests==1,"Schnelle Folgeprüfungen erzeugen keine parallelen Anfragen");
            delayed.SetResult(new GrammarResult {Status="Veraltet",Issues=new List<WritingIssue>{new WritingIssue {Start=0,Length=5,Original="Alter",Rule="test",Category="Grammatik",Message="Veraltet"}}});Wait(first);Assert(panel.ChecksCompleted==0,"Veraltete Ergebnisse werden nicht übernommen");Wait(panel.CheckAsync());Assert(panel.ChecksCompleted==1 && requests==2,"Aktueller Stand wird anschließend geprüft");
            int starts=panel.ChecksStarted;Wait(panel.CheckAsync(true));Assert(panel.ChecksStarted==starts,"Unveränderter Text wird automatisch nicht erneut geprüft");
        }
    }
    static void Performance()
    {
        var words=Enumerable.Range(0,100000).Select(x=>"Wort"+x.ToString("D6")).Concat(new[]{"wissenschaftlich","wissenschaftliche","wissenschaftlichen"});var index=new CompletionIndex(words);var lookup=Stopwatch.StartNew();int matches=0;for(int i=0;i<1000;i++)matches+=index.Find("wissenschaft",3).Count();lookup.Stop();Assert(matches==3000,"Präfixsuche findet drei Vorschläge");Assert(lookup.ElapsedMilliseconds<2000,"Großes Wörterbuch wird über Index durchsucht");
        var editor=new DocumentEditor {Width=700,Height=500};var root=new AdornerDecorator {Child=editor};
        using(var host=Host(root,700,500))using(var completion=new Completion(editor))using(var panel=new WritingPanel(editor,new WritingSettings(),delegate{return Task.FromResult(new GrammarResult {Status="Test"});}))
        {
            editor.TextChanged+=delegate{panel.Invalidate();};editor.Text=string.Join(" ",Enumerable.Repeat("Forschung braucht klare Ziele und regelmäßiges Feedback.",60));Wait(panel.CheckAsync());editor.Select(editor.Text.Length,0);var times=new List<double>();int started=panel.ChecksStarted;
            for(int i=0;i<80;i++){var watch=Stopwatch.StartNew();editor.SelectedText=i%8==0?" ":"a";Pump();watch.Stop();times.Add(watch.Elapsed.TotalMilliseconds);}
            times.Sort();double p95=times[(int)(times.Count*.95)];Console.WriteLine("Eingabeprobe ("+editor.Text.Length+" Zeichen): Median "+times[times.Count/2].ToString("0.0")+" ms, P95 "+p95.ToString("0.0")+" ms; Präfixsuche 1000× "+lookup.ElapsedMilliseconds+" ms.");Assert(p95<150,"Tippen eines längeren Posts bleibt ohne lange UI-Blockaden");Assert(panel.ChecksStarted==started,"Während der Eingabefolge wird keine Vollprüfung ausgelöst");
        }
    }
    static void DocumentRoundTrip()
    {
        var window=new Schreib();var editor=Field<DocumentEditor>(window,"editor");string file=Path.Combine(folder,"Raender.schreib");editor.Text="Testdokument.";editor.SetMargins(new DocumentMargins {Left=12,Right=23,Top=14,Bottom=19});typeof(Schreib).GetField("path",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,file);Assert((bool)typeof(Schreib).GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,new object[]{false}),"Schreib-Dokument mit Rändern speichern");
        editor.SetMargins(new DocumentMargins(),false);typeof(Schreib).GetMethod("OpenPath",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,new object[]{file});Assert(editor.PageMargins.Left==12 && editor.PageMargins.Right==23 && editor.PageMargins.Top==14 && editor.PageMargins.Bottom==19,"Alle vier Ränder bleiben beim Speichern und Öffnen erhalten");
        string bad=Path.Combine(folder,"kaputt.schreib");File.WriteAllText(bad,"{}");bool rejected=false;try{typeof(Schreib).GetMethod("OpenPath",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,new object[]{bad});}catch(TargetInvocationException){rejected=true;}Assert(rejected && editor.PageMargins.Left==12 && editor.Text=="Testdokument.","Fehlerhaftes Dokument verändert weder Text noch Ränder");
        var root=(FrameworkElement)window.Content;window.Content=null;using(var host=Host(root,1400,900)){Image(root,"Schreib-mit-Lineal",1400,900);}typeof(Schreib).GetField("dirty",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,false);window.Close();
    }
}
