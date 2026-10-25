using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

internal static class PlotChecks
{
    static int count;
    static void Assert(bool value,string message){if(!value)throw new Exception(message);count++;}
    static void Near(double expected,double actual,string message){Assert(Math.Abs(expected-actual)<1e-8,message+" ("+actual+")");}
    static PlotSource Input(string[,] cells){return new PlotSource {Identity=new object(),Name="Test",Cells=cells,Headings=true};}
    static T Field<T>(Tabelle window,string name){return (T)typeof(Tabelle).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);}
    static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
    {if(root is T)yield return (T)root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    [STAThread]public static int Main()
    {
        try{Run();Console.WriteLine(count+" Plotprüfungen bestanden.");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static void Run()
    {
        string folder=Path.Combine(Path.GetTempPath(),"byby-plot-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        double value;
        Assert(Plots.Number("1.234,5 €","Auto",out value) && value==1234.5,"Deutsche Zahl mit Währung");
        Assert(Plots.Number("1,234.5","Auto",out value) && value==1234.5,"Englische Zahl");
        Assert(Plots.Number("12,5%","Auto",out value) && value==12.5,"Prozentbeschriftete Werte bleiben in Prozentpunkten");
        Assert(Plots.Number("(2,5)","Auto",out value) && value==-2.5,"Negative Zahl in Klammern");
        Assert(Plots.Number("1,234","English",out value) && value==1234,"Explizite englische Gruppierung");
        Assert(Plots.Number("1.234","Deutsch",out value) && value==1234,"Explizite deutsche Gruppierung");
        Assert(!Plots.Number("NaN","Auto",out value) && !Plots.Number("Infinity","Auto",out value) && !Plots.Number("","Auto",out value),"Fehlende und nicht endliche Zahlen abgewiesen");
        Assert(Plots.Date("25.10.2026",out value) && DateTime.FromOADate(value).Year==2026,"Deutsches Datum");
        Assert(Plots.Date("2026-10-25",out value) && !Plots.Date("1.5",out value),"ISO-Datum, Dezimalzahl ist kein Datum");
        Assert(Plots.Short(1e-15)!="0","Sehr kleine Zahlen behalten wissenschaftliche Notation");
        var input=Input(new[,]{{"Gruppe","Wert","Zweite","Datum"},{"A","2","4","03.01.2024"},{"B","3","6","01.01.2024"},{"A","4","","02.01.2024"},{"C","","8","04.01.2024"}});
        var suggestions=Plots.Suggest(input);Assert(suggestions.Any(x=>x.Options.Kind==PlotKind.Line) && suggestions.Any(x=>x.Options.Kind==PlotKind.Scatter) && suggestions.Any(x=>x.Options.Kind==PlotKind.Columns),"Datengestützte Diagrammvorschläge");
        var options=new PlotOptions {Kind=PlotKind.Columns,X=0,Y=1,Aggregate=PlotAggregate.Sum};
        var data=Plots.Build(input,options);Assert(data.Categories.SequenceEqual(new[]{"A","B"}),"Leere Y-Werte werden nicht zu null");
        Near(6,data.Series[0].Points[0].Y,"Summe pro Kategorie");
        options.Aggregate=PlotAggregate.Mean;Near(3,Plots.Build(input,options).Series[0].Points[0].Y,"Mittelwert pro Kategorie");
        options.Aggregate=PlotAggregate.Count;data=Plots.Build(input,options);Assert(data.Categories.Contains("C"),"Kategorienzählung berücksichtigt Zeilen ohne Y-Zahl");Near(2,data.Series[0].Points[0].Y,"Anzahl pro Kategorie");
        options.Y2=2;Assert(Plots.Build(input,options).Series.Count==1,"Kategorienzählung erzeugt keine irreführende zweite Reihe");options.Y2=-1;
        options.Aggregate=PlotAggregate.Rows;Assert(Plots.Build(input,options).Categories.Count==3,"Jede gültige Zeile einzeln");
        options.Aggregate=PlotAggregate.Sum;options.Y2=2;data=Plots.Build(input,options);Assert(data.Series.Count==2 && data.Series[1].Points.Count==3,"Zwei Reihen mit unabhängigen fehlenden Werten");
        Assert(data.YLabel.Contains("Wert") && data.YLabel.Contains("Zweite"),"Gemeinsame Y-Beschriftung nennt beide Reihen");
        input.SelectedRows=new[]{2};options.SelectedOnly=true;data=Plots.Build(input,options);Assert(data.Categories.Count==1 && data.Categories[0]=="B","Nur ausgewählte Zeilen");
        input.SelectedRows=new int[0];bool rejected=false;try{Plots.Build(input,options);}catch(FormatException){rejected=true;}Assert(rejected,"Leere Zeilenauswahl erklärt");
        options.SelectedOnly=false;options.Y2=-1;options.SortValue=true;options.Limit=1;data=Plots.Build(input,options);Assert(data.Categories[0]=="A" && data.Notice.Contains("1 von 2"),"Sortierung und sichtbare Kategoriebegrenzung");
        data=Plots.Build(input,new PlotOptions {Kind=PlotKind.Line,X=3,Y=1});Assert(data.DateX && data.Series[0].Points[0].Label=="01.01.2024","Zeitverlauf chronologisch und mit Datumsskala");
        data=Plots.Build(input,new PlotOptions {Kind=PlotKind.Scatter,X=1,Y=2});Assert(data.Series[0].Points.Count==2 && data.NumericX,"Punktdiagramm nur mit gültigen Wertepaaren");
        var distribution=Input(new[,]{{"Kategorie","Zahl"},{"A","0"},{"A","1"},{"A","2"},{"A","3"},{"A","4"}});
        data=Plots.Build(distribution,new PlotOptions {Kind=PlotKind.Histogram,Y=1,Bins=2});Assert(data.Series[0].Points.Count==2 && data.Series[0].Points.Sum(x=>x.Y)==5,"Histogramm zählt auch den rechten Rand");
        Near(2,data.Series[0].Points[0].Y,"Linke Histogrammklasse");
        var same=Input(new[,]{{"Zahl"},{"5"},{"5"},{"5"}});data=Plots.Build(same,new PlotOptions {Kind=PlotKind.Histogram,Y=0});Assert(data.Series[0].Points.Count==1 && data.BinWidth>0,"Konstante Werte als Histogramm");
        var boxes=Input(new[,]{{"Gruppe","Zahl"},{"A","1"},{"A","2"},{"A","3"},{"A","4"},{"A","100"}});
        data=Plots.Build(boxes,new PlotOptions {Kind=PlotKind.Box,X=0,Y=1});Near(3,data.Boxes[0].Median,"Boxplot-Median");Assert(data.Boxes[0].Low==1 && data.Boxes[0].High==4 && data.Boxes[0].Outliers.SequenceEqual(new[]{100.0}),"Tukey-Whisker und Ausreißer");
        var categories=Input(new[,]{{"Name"},{"Alpha"},{"Beta"},{"Alpha"}});Assert(Plots.Suggest(categories)[0].Options.Aggregate==PlotAggregate.Count,"Kategorien ohne Zahlen automatisch zählen");
        rejected=false;try{Plots.Build(new PlotSource {Cells=new string[100,26]},new PlotOptions());}catch(FormatException){rejected=true;}Assert(rejected,"Eine leere Tabelle erzeugt keine erfundenen Zählwerte");
        var formula=Input(new[,]{{"X","Y"},{"1","=2*3"},{"2","=1/0"}});data=Plots.Build(formula,new PlotOptions {Kind=PlotKind.Scatter,X=0,Y=1});Assert(data.Series[0].Points.Count==1 && data.Series[0].Points[0].Y==6,"Unterstützte Formeln, Fehler werden ausgelassen");
        var large=new string[10001,2];large[0,0]="X";large[0,1]="Y";for(int i=1;i<10001;i++){large[i,0]=i.ToString();large[i,1]=(i*2).ToString();}
        data=Plots.Build(Input(large),new PlotOptions {Kind=PlotKind.Scatter,X=0,Y=1});Assert(data.Series[0].Points.Count==5000 && data.Series[0].Points.Last().X==10000 && data.Notice.Contains("5000"),"Große Punktwolke transparent begrenzt, letzter Punkt erhalten");
        var negative=Input(new[,]{{"Name","Wert"},{"Links","-5"},{"Rechts","10"}});
        var largeMean=Input(new[,]{{"Gruppe","Wert"},{"A","1e308"},{"A","1e308"}});
        Assert(Plots.Build(largeMean,new PlotOptions {X=0,Y=1,Aggregate=PlotAggregate.Mean}).Series[0].Points[0].Y==1e308,"Mittelwert ohne Überlauf bei großen endlichen Werten");
        var extreme=Plots.Build(Input(new[,]{{"Gruppe","Wert"},{"A","-1e308"},{"B","1e308"}}),new PlotOptions {X=0,Y=1});rejected=false;try{PlotDrawing.Scene(extreme,1200,800);}catch(FormatException){rejected=true;}Assert(rejected,"Nicht darstellbarer Wertebereich wird erklärt");
        foreach(PlotKind kind in Enum.GetValues(typeof(PlotKind)))
        {
            var selected=kind==PlotKind.Scatter?new PlotOptions {Kind=kind,X=1,Y=1}:new PlotOptions {Kind=kind,X=0,Y=1};
            data=Plots.Build(negative,selected);var scene=PlotDrawing.Scene(data,1200,800);
            Assert(scene.Shapes.Count>10 && scene.Shapes.All(s=>!double.IsNaN(s.X) && !double.IsInfinity(s.Y) && s.W>=0 && s.H>=0),"Endliche Zeichengeometrie für "+kind);
            PlotDrawing.Export(data,Path.Combine(folder,kind+".png"),1200,800);
        }
        data=Plots.Build(input,new PlotOptions {Kind=PlotKind.Columns,X=0,Y=1,Y2=2,Title="Umsatz <Plan> & Realität",YLabel="Wert (€)"});
        Assert(PlotDrawing.Scene(data,380,150).Shapes.Count>10,"Kleine Vorschau passt ihre Abstände an");
        var vector=PlotDrawing.Svg(PlotDrawing.Scene(data,1200,800),data.Notice);XNamespace ns="http://www.w3.org/2000/svg";
        Assert(vector.Root.Name==ns+"svg" && vector.Root.Descendants(ns+"text").Any(t=>t.Value.Contains("<Plan> & Realität")),"SVG kodiert Titel als Text");
        Assert(!vector.Root.Descendants(ns+"image").Any() && !vector.Root.Descendants(ns+"script").Any(),"SVG bleibt Vektor ohne Skripte");
        string png=Path.Combine(folder,"export.png"),jpg=Path.Combine(folder,"export.jpg"),svg=Path.Combine(folder,"export.svg");
        PlotDrawing.Export(data,png,1200,800);PlotDrawing.Export(data,jpg,1200,800);PlotDrawing.Export(data,svg,1200,800);
        foreach(string path in new[]{png,jpg}){using(var stream=File.OpenRead(path)){var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);Assert(decoder.Frames[0].PixelWidth==1200 && decoder.Frames[0].PixelHeight==800,"Exportabmessungen "+Path.GetExtension(path));}}
        Assert(XDocument.Load(svg).Root.Attribute("viewBox").Value=="0 0 1200 800","SVG-Export skalierbar");
        byte[] original=File.ReadAllBytes(png);rejected=false;try{PlotDrawing.Export(new PlotData(),png,1200,800);}catch(FormatException){rejected=true;}Assert(rejected && original.SequenceEqual(File.ReadAllBytes(png)),"Fehlgeschlagener Export erhält vorhandene Datei");
        Groups(folder);Ui(folder);
        Console.WriteLine("Plot-Testdateien und Vorschauen: "+folder);
    }
    static void Groups(string folder)
    {
        var source=Input(new[,]{{"Datum","Standort","Messung A","Messung B"},{"01.10.2026","Nord","2","12"},{"01.10.2026","Süd","20","120"},{"02.10.2026","Nord","4","14"},{"02.10.2026","Süd","40","140"},{"03.10.2026","West","8","18"},{"03.10.2026","","99","999"}});
        var options=new PlotOptions {Kind=PlotKind.Line,X=0,Y=2,Y2=3,Group=1};
        var data=Plots.Build(source,options);
        Assert(data.Groups.Count==3 && data.Series.Count==6 && data.Measures.SequenceEqual(new[]{"Messung A","Messung B"}),"Drei Farbgruppen und zwei Messreihen");
        Assert(data.Groups.Select(g=>g.Name).SequenceEqual(new[]{"Nord","Süd","West"}) && data.UsedRows==5,"Gruppenreihenfolge und verwendete Zeilen");
        Assert(data.Notice.Contains("ohne Gruppenangabe") && data.Notice.Contains("Standort"),"Fehlende Gruppen werden erklärt");
        Assert(data.Series.Where(s=>s.Group=="Nord").All(s=>s.Points.All(p=>p.Y<20)) && data.Series.Where(s=>s.Group=="Süd").All(s=>s.Points.All(p=>p.Y>=20)),"Kurven verbinden nur Werte derselben Gruppe");
        Assert(data.Series.Where(s=>s.Group=="Nord").Select(s=>s.ColorIndex).Distinct().Count()==1 && data.Series.Where(s=>s.MeasureIndex==0).Select(s=>s.ColorIndex).Distinct().Count()==3,"Farbe kodiert Gruppe, nicht Messreihe");
        Assert(data.Series.Where(s=>s.Group=="Nord").Select(s=>s.MeasureIndex).SequenceEqual(new[]{0,1}),"Messreihe erhält eigene Linienart");
        var scene=PlotDrawing.Scene(data,1200,800);
        var dateLabels=scene.Shapes.Where(s=>s.Type=="text" && System.Text.RegularExpressions.Regex.IsMatch(s.Text??"",@"^\d{2}\.\d{2}\.\d{2}$")).Select(s=>s.Text).ToArray();
        Assert(dateLabels.Length>1 && dateLabels.Distinct().Count()==dateLabels.Length,"Datumsachse zeigt keine doppelten Tagesbeschriftungen");
        Assert(scene.Shapes.Where(s=>s.Type=="polyline").Any(s=>s.Dash!=null) && scene.Shapes.Where(s=>s.Type=="polyline").Any(s=>s.Dash==null),"Durchgezogene und gestrichelte Gruppenlinien");
        Assert(scene.Shapes.Where(s=>s.Tip!=null).Any(s=>s.Type=="circle") && scene.Shapes.Where(s=>s.Tip!=null).Any(s=>s.Type=="rect"),"Kreise und Quadrate unterscheiden Messreihen");
        Assert(scene.Shapes.Where(s=>s.Type=="text").Any(s=>s.Text.Contains("Farbe · Standort")) && scene.Shapes.Where(s=>s.Type=="text").Any(s=>s.Text=="Messreihe:"),"Getrennte Legenden für Gruppe und Messreihe");
        XNamespace ns="http://www.w3.org/2000/svg";var svg=PlotDrawing.Svg(scene,data.Notice);
        Assert(svg.Root.Element(ns+"title").Value==data.Title && svg.Root.Descendants().Any(e=>e.Attribute("stroke-dasharray")!=null),"SVG enthält korrekten Titel und Linienarten");
        var selected=source.SelectedRows;source.SelectedRows=new[]{1,3};var partial=options.Copy();partial.SelectedOnly=true;
        var subset=Plots.Build(source,partial);Assert(subset.Groups.Count==1 && subset.Groups[0].ColorIndex==data.Groups[0].ColorIndex,"Farben bleiben bei Zeilenauswahl stabil");source.SelectedRows=selected;
        var reversed=Input(new[,]{{"Datum","Standort","Messung A","Messung B"},{"03.10.2026","West","8","18"},{"02.10.2026","Süd","40","140"},{"01.10.2026","Nord","2","12"}});
        Assert(Plots.Build(reversed,options).Groups.All(g=>g.ColorIndex==data.Groups.First(original=>original.Name==g.Name).ColorIndex),"Farben bleiben nach Umsortieren der Zeilen stabil");
        var limited=options.Copy();limited.GroupLimit=1;var one=Plots.Build(source,limited);Assert(one.Groups.Count==1 && one.Notice.Contains("Erste 1 Gruppen"),"Gruppenlimit sichtbar und nicht still");
        var categories=Input(new[,]{{"X","Gruppe","Wert"},{"A","Nord","2"},{"B","Süd","10"},{"A","Nord","3"},{"A","Süd","20"},{"B","Nord","4"}});
        var bars=Plots.Build(categories,new PlotOptions {Kind=PlotKind.Columns,X=0,Y=2,Group=1,Aggregate=PlotAggregate.Sum});
        Assert(bars.Categories.SequenceEqual(new[]{"A","B"}) && bars.Series.First(s=>s.Group=="Nord").Points.First(p=>p.Label=="A").Y==5,"Zusammenfassung innerhalb von X und Gruppe");
        Assert(bars.Series.First(s=>s.Group=="Süd").Points.First(p=>p.Label=="A").X==0,"Gleiche X-Kategorien sind über Gruppen ausgerichtet");
        var raw=Plots.Build(categories,new PlotOptions {Kind=PlotKind.Columns,X=0,Y=2,Group=1,Aggregate=PlotAggregate.Rows});
        Assert(raw.Categories.Count==5 && raw.Series.SelectMany(s=>s.Points).Select(p=>p.X).Distinct().Count()==5,"Einzelzeilen werden bei gleichen X-Werten nicht überlagert");
        var hist=Plots.Build(categories,new PlotOptions {Kind=PlotKind.Histogram,Y=2,Group=1,Bins=3});
        Assert(hist.Series.SelectMany(s=>s.Points).Sum(p=>p.Y)==5 && hist.Series[0].Points.Select(p=>p.X).SequenceEqual(hist.Series[1].Points.Select(p=>p.X)),"Histogramme verwenden gemeinsame Klassen und zählen alle Gruppenwerte");
        var boxes=Plots.Build(categories,new PlotOptions {Kind=PlotKind.Box,X=0,Y=2,Group=1});
        Assert(boxes.Boxes.Count==4 && boxes.Boxes.Where(b=>b.Label=="A").Select(b=>b.CategoryIndex).Distinct().Count()==1 && boxes.Boxes.Where(b=>b.Label=="A").Select(b=>b.GroupSlot).Distinct().Count()==2,"Boxplots liegen je X-Kategorie gruppiert nebeneinander");
        Assert(Enumerable.Range(0,24).Select(PlotPalette.Color).Distinct().Count()==24,"Palette liefert 24 verschiedene Farben");
        foreach(PlotKind kind in Enum.GetValues(typeof(PlotKind)))
        {
            var choice=options.Copy();choice.Kind=kind;if(kind==PlotKind.Scatter)choice.X=2;
            if(kind==PlotKind.Box || kind==PlotKind.Columns || kind==PlotKind.Bars)choice.X=1;
            var plot=Plots.Build(source,choice);PlotDrawing.Export(plot,Path.Combine(folder,"Gruppen-"+kind+".png"),1200,800);
            Assert(PlotDrawing.Scene(plot,1200,800).Shapes.Count>10,"Gruppierter Export "+kind);
        }
        PlotDrawing.Export(data,Path.Combine(folder,"Gruppen-Linie.svg"),1200,800);
    }
    static void Ui(string folder)
    {
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};var window=new Tabelle();
        string csv=Path.Combine(folder,"Daten.csv");File.WriteAllText(csv,"Gruppe;Umsatz;Kosten;Datum\nAlpha;12;5;01.01.2026\nBeta;18;8;02.01.2026\nAlpha;9;3;03.01.2026\nGamma;15;7;04.01.2026");
        window.OpenPath(csv);var root=(FrameworkElement)window.Content;window.Content=null;root.Resources.MergedDictionaries.Add(app.Resources.MergedDictionaries[0]);root.SetResourceReference(Panel.BackgroundProperty,"App.Background");root.SetResourceReference(TextBlock.ForegroundProperty,"App.Text");root.SetValue(TextBlock.FontSizeProperty,window.FontSize);
        using(var host=new HwndSource(new HwndSourceParameters("Plots-invisible-test"){Width=1440,Height=860,PositionX=-32000,PositionY=-32000,WindowStyle=unchecked((int)0x80000000)}))
        {
            host.RootVisual=root;Layout(root);
            var panel=Field<PlotPanel>(window,"plots");Assert(panel.Visibility==Visibility.Collapsed,"Plotbereich anfangs ausgeblendet");
            Children<Button>(root).First(b=>Convert.ToString(b.Content)=="Plot erstellen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Layout(root);
            Assert(panel.Visibility==Visibility.Visible && panel.Current!=null && panel.Current.Kind==PlotKind.Line,"Button öffnet passenden Plotvorschlag");
            var grid=Field<DataGrid>(window,"grid");var header=Children<DataGridColumnHeader>(grid).First(h=>h.Column!=null && h.Column.DisplayIndex==0);
            ((MenuItem)header.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert(panel.Current.XLabel=="Gruppe","Rechtsklick setzt X-Achse");
            Assert(panel.Current.Kind==PlotKind.Columns,"Rechtsklick auf Textspalte wählt einen passenden Diagrammtyp");
            header=Children<DataGridColumnHeader>(grid).First(h=>h.Column!=null && h.Column.DisplayIndex==2);
            ((MenuItem)header.ContextMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert(panel.Current.YLabel.Contains("Kosten"),"Rechtsklick setzt Y-Achse");
            panel.Use(new PlotOptions {Kind=PlotKind.Histogram,Y=1});
            ((MenuItem)header.ContextMenu.Items[2]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert(panel.Current.Kind==PlotKind.Columns && panel.Current.Series.Count==2,"Zweite Y-Reihe schaltet auf passenden Diagrammtyp um");
            panel.Use(new PlotOptions {Kind=PlotKind.Columns,X=0,Y=1,Y2=2,Title="Umsatz und Kosten",Aggregate=PlotAggregate.Sum});
            Assert(panel.Current.Series.Count==2 && panel.Current.Series[0].Points[0].Y==21,"Bearbeitbarer Vergleich mit zweiter Reihe");
            panel.ExportTo(Path.Combine(folder,"UI-Export.svg"),1200,800);Assert(File.Exists(Path.Combine(folder,"UI-Export.svg")),"Export aus dem Plotbereich");
            window.SelectCell(2,1);panel.Use(new PlotOptions {Kind=PlotKind.Columns,X=0,Y=1,SelectedOnly=true});Assert(panel.Current.Categories.Count==1 && panel.Current.Categories[0]=="Beta","Markierung in der Tabelle wird für Plot übernommen");
            panel.Use(new PlotOptions {Kind=PlotKind.Columns,X=0,Y=1,Y2=2,Title="Umsatz und Kosten",Aggregate=PlotAggregate.Sum});
            Layout(root);
            header=Children<DataGridColumnHeader>(grid).First(h=>h.Column!=null && h.Column.DisplayIndex==0);
            header.ContextMenu.PlacementTarget=header;header.ContextMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Assert(((SolidColorBrush)header.Background).Color==((SolidColorBrush)Theme.Color("Plot.Pick")).Color,"Rechtsklick markiert den aktiven Spaltenkopf");
            header.ContextMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));
            ((MenuItem)header.ContextMenu.Items[3]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Layout(root);
            Assert(panel.Current.Groups.Count==3 && grid.Columns[0].Header.ToString().Contains("Gruppe"),"Rechtsklick setzt Gruppierung und Headerrolle");
            header=Children<DataGridColumnHeader>(grid).First(h=>h.Column!=null && h.Column.DisplayIndex==0);
            Assert(((SolidColorBrush)header.Background).Color==((SolidColorBrush)Theme.Color("Plot.RoleGroup")).Color,"Gruppenspalte behält ihre Rollenfarbe");
            panel.Use(new PlotOptions {Kind=PlotKind.Line,X=3,Y=1,Y2=2,Group=0,Title="Verlauf nach Gruppe"});
            Assert(grid.Columns[3].Header.ToString().Contains("X") && grid.Columns[1].Header.ToString().Contains("Y1") && grid.Columns[2].Header.ToString().Contains("Y2"),"Alle Achsenrollen sind sichtbar");
            var graphMenu=Children<PlotView>(root).First().ContextMenu;graphMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));((MenuItem)graphMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Layout(root);
            Assert(panel.Current.Groups.Count==0 && !grid.Columns[0].Header.ToString().Contains("A · Gruppe"),"Rechtsklick im Plot kann Gruppierung und Rollenmarkierung entfernen");
            ((MenuItem)graphMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert(panel.Current.Groups.Count==3,"Rechtsklick im Plot kann Farbgruppierung setzen");
            string mode=Theme.Current.Mode;bool paper=Theme.Current.DarkDocument;
            foreach(string appearance in new[]{"Light","Dark"})
            {
                Theme.Set(appearance,paper,false);Layout(root);var bitmap=new RenderTargetBitmap(1440,860,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(folder,"Tabelle-Plot-"+appearance+".png")))encoder.Save(file);
                Assert(Children<PlotView>(root).First().ActualWidth>=300 && Children<PlotView>(root).First().ActualHeight>=200,"Vorschau passt in "+appearance+" in die rechte untere Fläche");
                header=Children<DataGridColumnHeader>(grid).First(h=>h.Column!=null && h.Column.DisplayIndex==0);
                Assert(((SolidColorBrush)header.Background).Color==((SolidColorBrush)Theme.Color("Plot.RoleGroup")).Color,"Rollenfarbe folgt "+appearance);
            }
            Theme.Set(mode,paper,false);
            string second=Path.Combine(folder,"Andere.csv");File.WriteAllText(second,"X;Y\n1;3\n2;6");window.OpenPath(second);panel.Open();
            Assert(panel.Current.Kind==PlotKind.Scatter && !panel.Current.Title.Contains("Umsatz"),"Neue Datei erhält eigene Vorschläge ohne alte Plotdaten");
            Children<Button>(root).First(b=>Convert.ToString(b.Content)=="×").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(panel.Visibility==Visibility.Collapsed,"Plotbereich lässt sich schließen");
        }window.Close();app.Shutdown();
    }
    static void Layout(FrameworkElement root){root.Measure(new Size(1440,860));root.Arrange(new Rect(0,0,1440,860));root.UpdateLayout();root.Dispatcher.Invoke(new Action(delegate{}),DispatcherPriority.ApplicationIdle);}
}
