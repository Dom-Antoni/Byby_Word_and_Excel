using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

internal sealed class PlotPanel : DockPanel,IDisposable
{
    sealed class Choice {public int Index;public string Label;public override string ToString(){return Label;}}
    readonly Func<PlotSource> source;
    readonly ComboBox kind=new ComboBox(),x=new ComboBox(),y=new ComboBox(),y2=new ComboBox(),group=new ComboBox(),aggregate=new ComboBox(),numbers=new ComboBox(),resolution=new ComboBox();
    readonly TextBox title=new TextBox(),xLabel=new TextBox(),yLabel=new TextBox(),bins=new TextBox(),limit=new TextBox(),groupLimit=new TextBox();
    readonly CheckBox selected=new CheckBox {Content="Nur markierte Zeilen"},sort=new CheckBox {Content="Kategorien nach Betrag sortieren"},zero=new CheckBox {Content="Y-Achse ab null"},dark=new CheckBox {Content="Dunkler Plot-Hintergrund"};
    readonly TextBlock hint=new TextBlock(),notice=new TextBlock();
    readonly Button export;
    readonly DispatcherTimer timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(350)};
    readonly Dictionary<object,PlotOptions> saved=new Dictionary<object,PlotOptions>();
    readonly PlotView view=new PlotView();
    PlotView largeView;
    Window largeWindow;
    PlotOptions options=new PlotOptions();
    object identity;
    bool updating,refreshColumns;
    public event Action CloseRequested;
    public event Action RolesChanged;
    public PlotData Current {get{return view.Data;}}
    public PlotOptions RolesFor(object sourceIdentity){return object.ReferenceEquals(identity,sourceIdentity)?options.Copy():null;}
    void PublishRoles(){if(RolesChanged!=null)RolesChanged();}
    public PlotPanel(Func<PlotSource> getSource)
    {
        source=getSource;Margin=new Thickness(12,0,0,0);Theme.Bind(this,Panel.BackgroundProperty,"App.Panel");
        var heading=new DockPanel {Margin=new Thickness(10,7,8,4)};DockPanel.SetDock(heading,Dock.Top);Children.Add(heading);
        var close=Ui.Button(heading,"×",delegate{if(CloseRequested!=null)CloseRequested();});DockPanel.SetDock(close,Dock.Right);close.Padding=new Thickness(9,2,9,2);close.ToolTip="Plotbereich schließen";
        var enlarge=Ui.Button(heading,"↗",Enlarge);DockPanel.SetDock(enlarge,Dock.Right);enlarge.Padding=new Thickness(9,2,9,2);enlarge.ToolTip="Plot in eigenem Fenster vergrößern";
        heading.Children.Add(new TextBlock {Text="Plots · lokal aus deinen Daten",FontSize=16,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
        var actions=new DockPanel {Margin=new Thickness(10,0,10,6)};DockPanel.SetDock(actions,Dock.Top);Children.Add(actions);
        var suggest=Ui.Button(actions,"Vorschläge",Suggestions);DockPanel.SetDock(suggest,Dock.Right);suggest.Padding=new Thickness(9,5,9,5);
        export=Ui.Button(actions,"Export…",Export);DockPanel.SetDock(export,Dock.Right);export.Padding=new Thickness(9,5,9,5);
        var rActions=new WrapPanel {Margin=new Thickness(10,0,10,4)};DockPanel.SetDock(rActions,Dock.Top);Children.Add(rActions);
        Ui.Button(rActions,"ggplot / R exportieren…",delegate{ExportR(false);});Ui.Button(rActions,"In RStudio öffnen…",delegate{ExportR(true);});
        kind.ItemsSource=Plots.KindNames;kind.Padding=new Thickness(6);actions.Children.Add(kind);
        var axes=new Grid {Margin=new Thickness(10,0,10,5)};axes.ColumnDefinitions.Add(new ColumnDefinition());axes.ColumnDefinitions.Add(new ColumnDefinition());DockPanel.SetDock(axes,Dock.Top);Children.Add(axes);
        var left=new StackPanel {Margin=new Thickness(0,0,5,0)};var right=new StackPanel {Margin=new Thickness(5,0,0,0)};axes.Children.Add(left);Grid.SetColumn(right,1);axes.Children.Add(right);
        Field(left,"X-Achse",x);Field(right,"Y-Achse",y);
        var grouping=new StackPanel {Margin=new Thickness(10,0,10,2)};Field(grouping,"Gruppieren / Farbe nach (Faktor)",group);DockPanel.SetDock(grouping,Dock.Top);Children.Add(grouping);
        var extra=new StackPanel {Margin=new Thickness(10,4,10,8)};
        Field(extra,"Zweite Y-Reihe (gemeinsame Skala)",y2);
        Field(extra,"Gleiche Kategorien zusammenfassen",aggregate);
        Field(extra,"Titel (leer = automatisch)",title);Field(extra,"X-Beschriftung (leer = automatisch)",xLabel);Field(extra,"Y-Beschriftung (leer = automatisch)",yLabel);
        Field(extra,"Zahlenformat · Auto liest einzelne Kommas/Punkte als Dezimalzeichen",numbers);
        Field(extra,"Histogrammklassen (0 = automatisch)",bins);Field(extra,"Maximale X-Kategorien (1–100)",limit);Field(extra,"Maximale Farbgruppen (1–24)",groupLimit);
        foreach(var check in new[]{selected,sort,zero,dark}){check.Margin=new Thickness(0,5,0,2);extra.Children.Add(check);check.Click+=Changed;}
        resolution.ItemsSource=new[]{"1200 × 800","1800 × 1200","2400 × 1600"};resolution.SelectedIndex=1;Field(extra,"Exportgröße",resolution);
        var scroll=new ScrollViewer {Content=extra,MaxHeight=250,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var expander=new Expander {Header="Beschriftung und Optionen",Content=scroll,Margin=new Thickness(10,0,10,4)};DockPanel.SetDock(expander,Dock.Top);Children.Add(expander);
        hint.FontSize=11;hint.TextWrapping=TextWrapping.Wrap;hint.MaxHeight=34;hint.Margin=new Thickness(10,0,10,5);Theme.Bind(hint,TextBlock.ForegroundProperty,"App.Muted");DockPanel.SetDock(hint,Dock.Top);Children.Add(hint);
        notice.FontSize=11;notice.TextWrapping=TextWrapping.Wrap;notice.Margin=new Thickness(10,5,10,8);var footer=new ScrollViewer {Content=notice,MaxHeight=72,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};DockPanel.SetDock(footer,Dock.Bottom);Children.Add(footer);
        var frame=new Border {Margin=new Thickness(10,0,10,0),BorderThickness=new Thickness(1),Child=view};Theme.Bind(frame,Border.BorderBrushProperty,"App.Border");Children.Add(frame);
        aggregate.ItemsSource=new[]{"Jede Zeile einzeln","Summe pro X","Mittelwert pro X","Zeilen pro X"};numbers.ItemsSource=new[]{"Auto","Deutsch","English"};
        foreach(var combo in new[]{kind,x,y,y2,group,aggregate,numbers})combo.SelectionChanged+=Changed;
        foreach(var box in new[]{title,xLabel,yLabel,bins,limit,groupLimit}){box.MaxLength=200;box.TextChanged+=Changed;}
        var groupMenu=new ContextMenu();view.ContextMenu=groupMenu;
        groupMenu.Opened+=delegate
        {
            groupMenu.Items.Clear();var input=source();
            foreach(var choice in Choices(input,"Keine Gruppierung"))
            {
                int column=choice.Index;var item=new MenuItem {Header=choice.Label,IsCheckable=true,IsChecked=options.Group==column};
                item.Click+=delegate{try{Assign(column,3);}catch(Exception ex){ClearPlot();notice.Text=ex.Message;}};groupMenu.Items.Add(item);
            }
        };
        timer.Tick+=delegate{timer.Stop();Refresh(false);};
    }
    static void Field(Panel panel,string label,Control input)
    {
        panel.Children.Add(new TextBlock {Text=label,FontSize=11,Margin=new Thickness(0,2,0,3),TextWrapping=TextWrapping.Wrap});input.Margin=new Thickness(0,0,0,4);
        var text=input as TextBox;if(text!=null)text.Padding=new Thickness(6);panel.Children.Add(input);
    }
    void Changed(object sender,RoutedEventArgs e){if(updating)return;timer.Stop();timer.Start();}
    public void SourceChanged(){if(Visibility!=Visibility.Visible)return;refreshColumns=true;timer.Stop();timer.Start();}
    public void SelectionChanged(){if(options.SelectedOnly)SourceChanged();}
    public void Reset(){timer.Stop();saved.Clear();identity=null;ClearPlot();PublishRoles();}
    void ClearPlot()
    {
        view.Data=null;view.InvalidateVisual();export.IsEnabled=false;
        if(largeView!=null){largeView.Data=null;largeView.InvalidateVisual();largeWindow.Title="Keine gültigen Plotdaten · Plot";}
    }
    public void Open(){Refresh(true);}
    public void Hide(){timer.Stop();if(largeWindow!=null)largeWindow.Close();}
    void Remember(){if(identity!=null)saved[identity]=options.Copy();}
    List<Choice> Choices(PlotSource input,string empty){var result=new List<Choice>{new Choice {Index=-1,Label=empty}};for(int col=0;col<input.Cells.GetLength(1);col++)result.Add(new Choice {Index=col,Label=Sheet.Column(col)+" · "+input.Column(col)});return result;}
    static int Pick(ComboBox combo){var item=combo.SelectedItem as Choice;return item==null?-1:item.Index;}
    static void Select(ComboBox combo,int index){combo.SelectedItem=combo.Items.Cast<Choice>().FirstOrDefault(c=>c.Index==index)??combo.Items[0];}
    void Populate(PlotSource input)
    {
        updating=true;
        try
        {
            x.ItemsSource=Choices(input,"(Zeilennummer / alle Werte)");y.ItemsSource=Choices(input,"(Anzahl Zeilen)");y2.ItemsSource=Choices(input,"(keine zweite Reihe)");group.ItemsSource=Choices(input,"(keine Gruppierung)");
            Select(x,options.X);Select(y,options.Y);Select(y2,options.Y2);kind.SelectedIndex=(int)options.Kind;aggregate.SelectedIndex=(int)options.Aggregate;numbers.SelectedItem=options.Numbers;
            Select(group,options.Group);groupLimit.Text=options.GroupLimit.ToString();
            title.Text=options.Title;xLabel.Text=options.XLabel;yLabel.Text=options.YLabel;bins.Text=options.Bins.ToString();limit.Text=options.Limit.ToString();selected.IsChecked=options.SelectedOnly;sort.IsChecked=options.SortValue;zero.IsChecked=options.Zero;dark.IsChecked=options.Dark;
        }finally{updating=false;}
    }
    void ReadOptions()
    {
        options.Kind=(PlotKind)Math.Max(0,kind.SelectedIndex);options.X=Pick(x);options.Y=Pick(y);options.Y2=Pick(y2);options.Group=Pick(group);options.Aggregate=(PlotAggregate)Math.Max(0,aggregate.SelectedIndex);
        options.Title=title.Text;options.XLabel=xLabel.Text;options.YLabel=yLabel.Text;options.Numbers=Convert.ToString(numbers.SelectedItem)??"Auto";
        int count;if(!int.TryParse(bins.Text,out count) || count<0 || count>100){if(options.Kind==PlotKind.Histogram)throw new FormatException("Histogrammklassen: bitte 0 (automatisch) bis 100 eingeben.");count=0;}options.Bins=count;
        if(!int.TryParse(limit.Text,out count) || count<1 || count>100){if(options.Kind==PlotKind.Columns || options.Kind==PlotKind.Bars || options.Kind==PlotKind.Box)throw new FormatException("Maximale Kategorien: bitte 1 bis 100 eingeben.");count=20;}options.Limit=count;
        if(!int.TryParse(groupLimit.Text,out count) || count<1 || count>24){if(options.Group>=0)throw new FormatException("Farbgruppen: bitte 1 bis 24 eingeben.");count=12;}options.GroupLimit=count;
        options.SelectedOnly=selected.IsChecked==true;options.SortValue=sort.IsChecked==true;options.Zero=zero.IsChecked==true;options.Dark=dark.IsChecked==true;
    }
    void Refresh(bool opening)
    {
        timer.Stop();try
        {
            var input=source();bool changed=!object.ReferenceEquals(identity,input.Identity);
            if(changed)
            {
                Remember();identity=input.Identity;
                if(!saved.TryGetValue(identity,out options)){var suggestions=Plots.Suggest(input);options=suggestions.Count==0?new PlotOptions():suggestions[0].Options.Copy();}
                Populate(input);
            }
            else if(!opening){ReadOptions();if(refreshColumns)Populate(input);}
            else{ReadOptions();Populate(input);}
            refreshColumns=false;
            Render(input);
        }
        catch(Exception ex){ClearPlot();notice.Text=ex.Message;PublishRoles();}
    }
    void Render(PlotSource input)
    {
        PublishRoles();
        hint.Text=options.Kind==PlotKind.Histogram?"Häufigkeitsverteilung; alle gültigen Werte gehen in die Klassen ein.":options.Kind==PlotKind.Box?"Median, Quartile und Tukey-Whisker (1,5 × IQR); Ausreißer als Punkte.":options.Kind==PlotKind.Line?"Verfügbare Werte werden verbunden; numerische X-Werte und Datumswerte sortiert.":options.Kind==PlotKind.Scatter?"Jede gültige Wertepaar-Zeile ist ein Punkt.":"Gleiche Kategorien: "+(options.Aggregate==PlotAggregate.Rows?"jede Zeile einzeln":options.Aggregate==PlotAggregate.Sum?"Summe":options.Aggregate==PlotAggregate.Mean?"Mittelwert":"Anzahl Zeilen")+".";
        bool grouped=options.Kind==PlotKind.Columns || options.Kind==PlotKind.Bars;
        aggregate.IsEnabled=grouped;sort.IsEnabled=grouped;limit.IsEnabled=grouped || options.Kind==PlotKind.Box;x.IsEnabled=options.Kind!=PlotKind.Histogram;bins.IsEnabled=options.Kind==PlotKind.Histogram;y2.IsEnabled=options.Kind!=PlotKind.Histogram && options.Kind!=PlotKind.Box && !(grouped && options.Aggregate==PlotAggregate.Count);
        view.Data=Plots.Build(input,options);view.InvalidateVisual();notice.Text=view.Data.Notice;export.IsEnabled=true;Remember();
        if(largeView!=null){largeView.Data=view.Data;largeView.InvalidateVisual();largeWindow.Title=view.Data.Title+" · Plot";}
    }
    void Suggestions()
    {
        try
        {
            var input=source();var suggestions=Plots.Suggest(input,Convert.ToString(numbers.SelectedItem)??"Auto");var menu=new ContextMenu();
            foreach(var suggestion in suggestions)
            {
                var chosen=suggestion;var item=new MenuItem {Header=chosen.Label,ToolTip=chosen.Reason};item.Click+=delegate{try{options=chosen.Options.Copy();Populate(input);Render(input);}catch(Exception ex){ClearPlot();notice.Text=ex.Message;}};menu.Items.Add(item);
            }
            if(suggestions.Count==0)menu.Items.Add(new MenuItem {Header="Noch keine passenden Daten erkannt",IsEnabled=false});menu.IsOpen=true;
        }
        catch(Exception ex){notice.Text=ex.Message;}
    }
    public void Assign(int column,int axis)
    {
        Refresh(false);var input=source();
        if(axis==0)options.X=column;else if(axis==1)options.Y=column;else if(axis==2)options.Y2=column;else options.Group=column;
        if(options.Kind==PlotKind.Histogram && axis==0)options.Kind=PlotKind.Columns;
        if(options.Y<0 && axis==0){var numeric=Plots.Profile(input,options.Numbers).FirstOrDefault(c=>c.IsNumber && c.Index!=column);options.Y=numeric==null?-1:numeric.Index;}
        if(axis==2){if(options.Kind==PlotKind.Histogram || options.Kind==PlotKind.Box)options.Kind=PlotKind.Columns;if(options.Aggregate==PlotAggregate.Count)options.Aggregate=PlotAggregate.Sum;}
        if(axis==0)
        {
            var profile=Plots.Profile(input,options.Numbers)[column];
            if(profile.IsDate)options.Kind=PlotKind.Line;
            else if(profile.IsNumber && options.Kind!=PlotKind.Box)options.Kind=PlotKind.Scatter;
            else if(options.Kind==PlotKind.Line || options.Kind==PlotKind.Scatter)options.Kind=PlotKind.Columns;
        }
        Populate(input);try{Render(input);}catch(Exception ex){ClearPlot();notice.Text=ex.Message;}
    }
    internal void Use(PlotOptions value){options=value.Copy();var input=source();identity=input.Identity;Populate(input);Render(input);}
    internal void ExportTo(string path,int width=1600,int height=1000){if(view.Data==null)throw new FormatException("Bitte zuerst einen gültigen Plot erstellen.");PlotDrawing.Export(view.Data,path,width,height);}
    void Export()
    {
        Refresh(false);if(view.Data==null)return;
        string filename=System.Text.RegularExpressions.Regex.Replace(view.Data.Title,@"[\\/:*?""<>|]","_");if(filename.Length>100)filename=filename.Substring(0,100);
        var dialog=new SaveFileDialog {Filter="PNG (*.png)|*.png|JPG (*.jpg)|*.jpg|SVG (*.svg)|*.svg",FileName=filename,DefaultExt=".png",AddExtension=true};
        if(dialog.ShowDialog()!=true)return;
        try{int width=resolution.SelectedIndex==0?1200:resolution.SelectedIndex==2?2400:1800;ExportTo(dialog.FileName,width,width*2/3);notice.Text="Exportiert: "+Path.GetFileName(dialog.FileName)+" · "+width+" × "+(width*2/3)+" · "+view.Data.Notice;}
        catch(Exception ex){MessageBox.Show(ex.Message,"Plot konnte nicht exportiert werden");}
    }
    async void ExportR(bool open)
    {
        try
        {
            Refresh(false);if(view.Data==null)throw new FormatException("Bitte zuerst einen gültigen Plot erstellen.");
            var dialog=new SaveFileDialog {Filter="Neues R-Projekt (*.Rproj)|*.Rproj",FileName="Plot-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".Rproj",Title="R-Projekt exportieren · eigener Unterordner wird angelegt"};if(dialog.ShowDialog()!=true)return;
            int width=resolution.SelectedIndex==0?1200:resolution.SelectedIndex==2?2400:1800;
            var result=RExport.Create(Path.Combine(Path.GetDirectoryName(dialog.FileName),Path.GetFileNameWithoutExtension(dialog.FileName)),source(),view.Data,options.Numbers,width,width*2/3);
            notice.Text="R-Projekt gespeichert. Lokales R erzeugt die Objekte im Hintergrund…";await RExport.PrepareObjects(result);notice.Text=result.Message;
            if(open)RExport.Open(result);else MessageBox.Show(result.Message+"\n\n"+result.Directory,"R-Export");
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"R-Export fehlgeschlagen");}
    }
    void Enlarge()
    {
        Refresh(false);if(view.Data==null)return;
        if(largeWindow!=null){largeWindow.Activate();return;}
        largeView=new PlotView {Data=view.Data};largeWindow=new Window();Ui.Window(largeWindow,view.Data.Title+" · Plot");largeWindow.Width=1000;largeWindow.Height=700;largeWindow.MinWidth=480;largeWindow.MinHeight=320;largeWindow.Owner=Window.GetWindow(this);largeWindow.Content=largeView;largeWindow.Closed+=delegate{largeWindow=null;largeView=null;};largeWindow.Show();
    }
    public void Dispose(){Hide();}
}
