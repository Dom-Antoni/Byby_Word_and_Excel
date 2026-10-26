using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

internal static class Sheet
{
    public static string Column(int n) { string s = ""; do { s = (char)('A' + n % 26) + s; n = n / 26 - 1; } while (n >= 0); return s; }
    public static List<string[]> Read(string text, char separator)
    {
        var rows = new List<string[]>(); var row = new List<string>(); var field = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else if (quoted || field.Length == 0) quoted = !quoted; else throw new FormatException("Ungültiges Anführungszeichen in CSV."); }
            else if (c == separator && !quoted) { row.Add(field.ToString()); field.Clear(); }
            else if ((c == '\r' || c == '\n') && !quoted) { if (c == '\r' && i + 1 < text.Length && text[i+1] == '\n') i++; row.Add(field.ToString()); field.Clear(); rows.Add(row.ToArray()); row.Clear(); }
            else field.Append(c);
        }
        if (quoted) throw new FormatException("Nicht geschlossenes Anführungszeichen in CSV.");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
    public static string Quote(string text) { return "\"" + text.Replace("\"", "\"\"") + "\""; }
    public static double Value(string[,] cells, int row, int col, HashSet<string> active)
    {
        if (row < 0 || col < 0 || row >= cells.GetLength(0) || col >= cells.GetLength(1)) throw new FormatException("#BEZUG!");
        string key = row + ":" + col;
        if (active.Count > 200 || !active.Add(key)) throw new FormatException("#ZIRKEL!");
        try
        {
            string raw = cells[row,col] ?? "";
            if (raw.StartsWith("=")) return new Formula(raw.Substring(1), cells, active).Parse();
            if (raw.Trim().Length == 0) return 0;
            double value;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out value) || double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
            throw new FormatException("#WERT!");
        }
        finally { active.Remove(key); }
    }
    sealed class Formula
    {
        readonly string text; readonly string[,] cells; readonly HashSet<string> active; int pos;
        public Formula(string t, string[,] c, HashSet<string> a) { text = t.ToUpperInvariant(); cells = c; active = a; }
        void Space() { while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++; }
        bool Take(char c) { Space(); if (pos < text.Length && text[pos] == c) { pos++; return true; } return false; }
        public double Parse() { double n = Add(); Space(); if (pos != text.Length || double.IsNaN(n) || double.IsInfinity(n)) throw new FormatException("#FORMEL!"); return n; }
        double Add() { double n = Multiply(); while (true) { if (Take('+')) n += Multiply(); else if (Take('-')) n -= Multiply(); else return n; } }
        double Multiply() { double n = Atom(); while (true) { if (Take('*')) n *= Atom(); else if (Take('/')) { double d = Atom(); if (d == 0) throw new FormatException("#DIV/0!"); n /= d; } else return n; } }
        void Cell(out int r, out int c)
        {
            Space(); c = 0; int start = pos;
            while (pos < text.Length && text[pos] >= 'A' && text[pos] <= 'Z') { c = checked(c * 26 + text[pos++] - 'A' + 1); }
            if (pos == start) throw new FormatException("#BEZUG!");
            start = pos; while (pos < text.Length && char.IsDigit(text[pos])) pos++;
            if (pos == start || !int.TryParse(text.Substring(start, pos-start), out r)) throw new FormatException("#BEZUG!"); r--; c--;
        }
        double Atom()
        {
            Space(); if (Take('+')) return Atom(); if (Take('-')) return -Atom();
            if (Take('(')) { double v = Add(); if (!Take(')')) throw new FormatException("#FORMEL!"); return v; }
            var function = Regex.Match(text.Substring(pos), @"^(SUMME|SUM|MITTELWERT|AVERAGE|MIN|MAX)\s*\(");
            if (function.Success)
            {
                pos += function.Length; int r1,c1,r2,c2; Cell(out r1,out c1); r2=r1;c2=c1;
                if (Take(':')) Cell(out r2,out c2);
                if (!Take(')')) throw new FormatException("#FORMEL!");
                if (r1 < 0 || c1 < 0 || r2 >= cells.GetLength(0) || c2 >= cells.GetLength(1) || r2 < r1 || c2 < c1) throw new FormatException("#BEZUG!");
                var values = new List<double>();
                for (int r=r1;r<=r2;r++) for (int c=c1;c<=c2;c++)
                {
                    string raw = cells[r,c] ?? ""; double ignored;
                    if (raw.StartsWith("=") || double.TryParse(raw, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"),out ignored) || double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture,out ignored)) values.Add(Value(cells,r,c,active));
                }
                string name=function.Groups[1].Value;
                if (name=="SUM" || name=="SUMME") return values.Sum();
                if (values.Count==0) throw new FormatException("#WERT!");
                if (name=="MIN") return values.Min(); if (name=="MAX") return values.Max(); return values.Average();
            }
            if (pos < text.Length && char.IsLetter(text[pos])) { int r,c; Cell(out r,out c); return Value(cells,r,c,active); }
            var number=Regex.Match(text.Substring(pos), @"^(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:E[+-]?\d+)?");
            if (!number.Success) throw new FormatException("#FORMEL!"); pos+=number.Length;
            return double.Parse(number.Value.Replace(',','.'), CultureInfo.InvariantCulture);
        }
    }
}

internal sealed class Tabelle : Window
{
    readonly DataGrid grid=new DataGrid();
    readonly TextBox formula=new TextBox(),address=new TextBox(),search=new TextBox();
    readonly TextBlock status=new TextBlock(),note=new TextBlock(),modeLabel=new TextBlock();
    readonly TabControl worksheets=new TabControl();
    readonly DockPanel searchBar=new DockPanel();
    readonly PlotPanel plots;
    readonly Grid workspace=new Grid();
    readonly ColumnDefinition plotColumn=new ColumnDefinition {Width=new GridLength(0)};
    readonly GridSplitter plotSplitter=new GridSplitter {Width=5,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Stretch,Visibility=Visibility.Collapsed};
    readonly HashSet<Formats.Worksheet> changed=new HashSet<Formats.Worksheet>();
    readonly List<MenuItem> separatorItems=new List<MenuItem>(),themeItems=new List<MenuItem>();
    List<Formats.Worksheet> workbook;
    DataTable table;
    string path,sourceText,importNote="";
    char delimiter=';';char? forcedSeparator;
    bool rendering,editing=true,results,headings,wrap=true,frozen;
    int selectedRow,selectedCol;
    MenuItem editItem,resultItem,headingsItem,wrapItem,freezeItem,plotItem;
    Button applyButton;
    Action appearanceChanged;
    Formats.Worksheet Active {get{return workbook[worksheets.SelectedIndex<0?0:worksheets.SelectedIndex];}}
    string[,] Cells {get{return Active.Cells;}}
    int Offset {get{return headings?1:0;}}
    [STAThread]public static void Main(string[] args)
    {
        using(var running=new System.Threading.Mutex(false,"Local\\BybyTabelleRunning"))
        {
        var app=new Application();var window=new Tabelle();
        if(args.Length>0)window.Loaded+=delegate{window.SafeOpen(args[0]);};app.Run(window);
        }
    }
    public Tabelle()
    {
        Ui.Window(this,"Tabelle · Tabellen ansehen");
        Width=Math.Min(1440,SystemParameters.WorkArea.Width-24);Height=Math.Min(840,SystemParameters.WorkArea.Height-24);
        MinWidth=Math.Min(840,Width);MinHeight=Math.Min(560,Height);
        var root=new DockPanel();Content=root;
        var heading=new Border {Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,91,76)),Padding=new Thickness(20,10,20,10)};
        heading.Child=new TextBlock {Text="Tabelle  ·  Tabellen in Ruhe ansehen",Foreground=System.Windows.Media.Brushes.White,FontSize=20,FontWeight=FontWeights.SemiBold};
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var ribbon=new StackPanel();Theme.Bind(ribbon,Panel.BackgroundProperty,"App.Panel");DockPanel.SetDock(ribbon,Dock.Top);root.Children.Add(ribbon);
        var menus=new Menu {Padding=new Thickness(12,2,12,2)};ribbon.Children.Add(menus);BuildMenus(menus);
        var quick=new WrapPanel {Margin=new Thickness(16,8,16,10)};ribbon.Children.Add(quick);
        Ui.Button(quick,"Öffnen…",Open);Ui.Button(quick,"Speichern",delegate{Save();});Ui.Button(quick,"Spalten anpassen",FitColumns);
        Ui.Button(quick,"Plot erstellen",delegate{SetPlotsVisible(true);});
        modeLabel.VerticalAlignment=VerticalAlignment.Center;modeLabel.Margin=new Thickness(12,0,0,0);Theme.Bind(modeLabel,TextBlock.ForegroundProperty,"App.Muted");quick.Children.Add(modeLabel);
        var formulaBar=new DockPanel {Margin=new Thickness(16,10,16,8)};DockPanel.SetDock(formulaBar,Dock.Top);root.Children.Add(formulaBar);
        address.Width=84;address.Padding=new Thickness(8);address.Margin=new Thickness(0,0,10,0);address.ToolTip="Zelladresse, zum Beispiel D25. Enter springt zur Zelle.";
        address.KeyDown+=delegate(object s,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Enter){TryGoTo(address.Text);e.Handled=true;}};formulaBar.Children.Add(address);
        applyButton=Ui.Button(formulaBar,"Übernehmen",ApplyFormula);DockPanel.SetDock(applyButton,Dock.Right);
        formula.Padding=new Thickness(10,6,10,6);formula.VerticalContentAlignment=VerticalAlignment.Center;formula.ToolTip="Vollständiger Zellinhalt. Bearbeitung aktivieren, um ihn zu ändern.";
        formula.KeyDown+=delegate(object s,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Enter && editing){ApplyFormula();e.Handled=true;}};formulaBar.Children.Add(formula);
        searchBar.Margin=new Thickness(16,0,16,8);searchBar.Visibility=Visibility.Collapsed;DockPanel.SetDock(searchBar,Dock.Top);root.Children.Add(searchBar);
        Ui.Button(searchBar,"Weitersuchen",FindNext);Ui.Button(searchBar,"Schließen",delegate{searchBar.Visibility=Visibility.Collapsed;grid.Focus();});
        search.Padding=new Thickness(8);search.ToolTip="Zellinhalt suchen (Groß-/Kleinschreibung wird ignoriert)";
        search.KeyDown+=delegate(object s,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Enter){FindNext();e.Handled=true;}if(e.Key==System.Windows.Input.Key.Escape){searchBar.Visibility=Visibility.Collapsed;grid.Focus();e.Handled=true;}};searchBar.Children.Add(search);
        var footer=new StackPanel {Margin=new Thickness(16,8,16,10)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        status.FontSize=13;footer.Children.Add(status);note.FontSize=12;note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(0,3,0,0);Theme.Bind(note,TextBlock.ForegroundProperty,"App.Muted");footer.Children.Add(note);
        var sheetScroll=new ScrollViewer {HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(16,7,16,0),Content=worksheets};
        DockPanel.SetDock(sheetScroll,Dock.Bottom);root.Children.Add(sheetScroll);
        worksheets.BorderThickness=new Thickness(0);worksheets.SelectionChanged+=delegate(object s,SelectionChangedEventArgs e){if(rendering || e.Source!=worksheets || workbook==null || worksheets.SelectedIndex<0)return;Sync();selectedRow=Offset;selectedCol=0;Render();};
        ConfigureGrid();
        workspace.Margin=new Thickness(16,0,16,0);workspace.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star),MinWidth=250});workspace.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});workspace.ColumnDefinitions.Add(plotColumn);
        var frame=new Border {BorderThickness=new Thickness(1),Child=grid};Theme.Bind(frame,Border.BorderBrushProperty,"App.Border");workspace.Children.Add(frame);
        plots=new PlotPanel(PlotInput) {Visibility=Visibility.Collapsed};plots.CloseRequested+=delegate{SetPlotsVisible(false);};plots.RolesChanged+=UpdatePlotHeaders;Grid.SetColumn(plots,2);workspace.Children.Add(plots);Grid.SetColumn(plotSplitter,1);workspace.Children.Add(plotSplitter);root.Children.Add(workspace);
        appearanceChanged=delegate{foreach(var item in themeItems)item.IsChecked=(string)item.Tag==Theme.Current.Mode;};
        Theme.Changed+=appearanceChanged;
        CommandBindings.Add(new System.Windows.Input.CommandBinding(System.Windows.Input.ApplicationCommands.Open,delegate{Open();}));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(System.Windows.Input.ApplicationCommands.New,delegate{New();}));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(System.Windows.Input.ApplicationCommands.Save,delegate{Save();}));
        CommandBindings.Add(new System.Windows.Input.CommandBinding(System.Windows.Input.ApplicationCommands.Find,delegate{ShowSearch();}));
        AllowDrop=true;PreviewDragOver+=delegate(object s,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effects=DragDropEffects.Copy;e.Handled=true;}};
        PreviewDrop+=delegate(object s,DragEventArgs e){if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;e.Handled=true;var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null && files.Length>0 && CanLeave())SafeOpen(files[0]);};
        Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e){e.Cancel=!CanLeave();};
        Closed+=delegate{Theme.Changed-=appearanceChanged;plots.Dispose();};
        SetBook(new SheetImport {Sheets=new List<Formats.Worksheet>{new Formats.Worksheet {Name="Tabelle 1",Cells=new string[100,26]}},Note="Neue Tabelle · einfache Formeln beginnen mit ="} ,null,false);
    }
    MenuItem Item(ItemsControl parent,string title,Action action=null,string shortcut=null)
    {
        var item=new MenuItem {Header=title,InputGestureText=shortcut??""};if(action!=null)item.Click+=delegate{try{action();}catch(Exception ex){MessageBox.Show(ex.Message,"Aktion fehlgeschlagen",MessageBoxButton.OK,MessageBoxImage.Error);}};parent.Items.Add(item);return item;
    }
    MenuItem Check(ItemsControl parent,string title,bool value,Action<MenuItem> action)
    {
        var item=Item(parent,title);item.IsCheckable=true;item.IsChecked=value;item.Click+=delegate{try{action(item);}catch(Exception ex){MessageBox.Show(ex.Message,"Aktion fehlgeschlagen");}};return item;
    }
    void BuildMenus(Menu menus)
    {
        var file=Item(menus,"_Datei");Item(file,"_Neu",New,"Strg+N");Item(file,"Ö_ffnen / Importieren…",Open,"Strg+O");Item(file,"_Speichern",delegate{Save();},"Strg+S");Item(file,"Speichern unter / Blatt exportieren…",delegate{Save(true);});file.Items.Add(new Separator());Item(file,"Schließen",Close,"Alt+F4");
        var edit=Item(menus,"_Bearbeiten");editItem=Check(edit,"Bearbeitung aktivieren",true,delegate(MenuItem item){Sync();editing=item.IsChecked;if(editing){results=false;resultItem.IsChecked=false;}Render();});
        var copy=Item(edit,"Auswahl kopieren",null,"Strg+C");copy.Command=System.Windows.Input.ApplicationCommands.Copy;copy.CommandTarget=grid;
        Item(edit,"_Suchen…",ShowSearch,"Strg+F");edit.Items.Add(new Separator());
        Item(edit,"50 Zeilen hinzufügen",delegate{RequireEditing();Resize(Cells.GetLength(0)+50,Cells.GetLength(1));});
        Item(edit,"Spalte hinzufügen",delegate{RequireEditing();Resize(Cells.GetLength(0),Cells.GetLength(1)+1);});
        var view=Item(menus,"_Ansicht");headingsItem=Check(view,"Erste Zeile als Spaltennamen",false,delegate(MenuItem item){Sync();headings=item.IsChecked;Render();});
        freezeItem=Check(view,"Erste Spalte beim Scrollen festhalten",false,delegate(MenuItem item){frozen=item.IsChecked;grid.FrozenColumnCount=frozen?1:0;});
        wrapItem=Check(view,"Lange Zelltexte umbrechen",true,delegate(MenuItem item){Sync();wrap=item.IsChecked;Render();});
        Item(view,"Spaltenbreiten an Inhalt anpassen",FitColumns);
        plotItem=Check(view,"Plots anzeigen",false,delegate(MenuItem item){SetPlotsVisible(item.IsChecked);});
        var zoom=Item(view,"Zoom");foreach(int percent in new[]{80,100,120,150}){int chosen=percent;Item(zoom,percent+" %",delegate{grid.FontSize=14.0*chosen/100;grid.MinRowHeight=38.0*chosen/100;});}
        resultItem=Check(view,"Ergebnisse statt Formeln anzeigen",false,delegate(MenuItem item){Sync();results=item.IsChecked;Render();});
        var modes=Item(view,"Darstellung");foreach(string mode in new[]{"System","Light","Dark"}){string chosen=mode;var item=Item(modes,mode=="System"?"Wie Windows":mode=="Light"?"Hell":"Dunkel",delegate{Theme.Set(chosen,Theme.Current.DarkDocument);});item.IsCheckable=true;item.IsChecked=Theme.Current.Mode==mode;item.Tag=mode;themeItems.Add(item);}
        var data=Item(menus,"_Daten");Item(data,"Datei importieren…",Open);var separators=Item(data,"Text-Trennzeichen");
        foreach(char? value in new char?[]{null,';',',','\t'})
        {
            char? chosen=value;var item=Item(separators,value.HasValue?SheetFiles.SeparatorName(value.Value):"Automatisch erkennen",delegate{ChangeSeparator(chosen);});item.IsCheckable=true;item.IsChecked=!value.HasValue;item.Tag=value;separatorItems.Add(item);
        }
        Item(data,"Zum Anfang des Blatts",delegate{SelectCell(Offset,0);});
    }
    void ConfigureGrid()
    {
        grid.CanUserAddRows=false;grid.CanUserDeleteRows=false;grid.CanUserReorderColumns=false;grid.CanUserSortColumns=false;grid.AutoGenerateColumns=false;
        grid.HeadersVisibility=DataGridHeadersVisibility.All;grid.RowHeaderWidth=60;grid.MinRowHeight=38;grid.ColumnHeaderHeight=60;
        grid.FontSize=14;grid.BorderThickness=new Thickness(0);grid.GridLinesVisibility=DataGridGridLinesVisibility.All;
        grid.SelectionUnit=DataGridSelectionUnit.Cell;grid.SelectionMode=DataGridSelectionMode.Extended;
        grid.EnableRowVirtualization=true;grid.EnableColumnVirtualization=true;grid.ClipboardCopyMode=DataGridClipboardCopyMode.IncludeHeader;
        Theme.Bind(grid,Control.BackgroundProperty,"App.Control");Theme.Bind(grid,Control.ForegroundProperty,"App.Text");Theme.Bind(grid,DataGrid.RowBackgroundProperty,"App.Control");Theme.Bind(grid,DataGrid.AlternatingRowBackgroundProperty,"App.Panel");grid.AlternationCount=2;
        Theme.Bind(grid,DataGrid.HorizontalGridLinesBrushProperty,"App.Border");Theme.Bind(grid,DataGrid.VerticalGridLinesBrushProperty,"App.Border");
        grid.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(GridStyles));
        grid.LoadingRow+=delegate(object s,DataGridRowEventArgs e){e.Row.Header=(e.Row.GetIndex()+Offset+1).ToString();};
        grid.CurrentCellChanged+=delegate
        {
            if(rendering || grid.CurrentCell.Column==null)return;var item=grid.CurrentItem as DataRowView;if(item==null || table==null)return;
            int row=table.Rows.IndexOf(item.Row);if(row<0)return;int column=grid.Columns.IndexOf(grid.CurrentCell.Column);if(column<0)return;selectedRow=row+Offset;selectedCol=column;ShowCell();
        };
        grid.SelectedCellsChanged+=delegate{if(plots!=null)plots.SelectionChanged();};
    }
    void SetPlotsVisible(bool visible)
    {
        plots.Visibility=visible?Visibility.Visible:Visibility.Collapsed;plotSplitter.Visibility=plots.Visibility;
        plotColumn.MinWidth=visible?380:0;plotColumn.Width=new GridLength(visible?Math.Min(540,Math.Max(400,ActualWidth*.42)):0);plotItem.IsChecked=visible;
        if(visible)plots.Open();
        else plots.Hide();
    }
    PlotSource PlotInput()
    {
        Sync();var indices=new Dictionary<DataRow,int>();for(int r=0;r<table.Rows.Count;r++)indices[table.Rows[r]]=r+Offset;
        var selected=grid.SelectedCells.Select(cell=>cell.Item as DataRowView).Where(item=>item!=null && indices.ContainsKey(item.Row)).Select(item=>indices[item.Row]).Distinct().ToArray();
        return new PlotSource {Identity=Active,Name=Active.Name,Cells=Cells,Headings=headings,SelectedRows=selected};
    }
    void PlotAxis(int column,int axis){SetPlotsVisible(true);plots.Assign(column,axis);}
    string HeaderTitle(int column)
    {
        var roles=plots==null?null:plots.RolesFor(Active);var labels=new List<string>();
        if(roles!=null){if(roles.X==column)labels.Add("X");if(roles.Y==column)labels.Add("Y1");if(roles.Y2==column)labels.Add("Y2");if(roles.Group==column)labels.Add("Gruppe");}
        string name=headings?(Cells[0,column]??"").Replace("\r"," ").Replace("\n"," "):"";
        return Sheet.Column(column)+(labels.Count==0?"":" · "+string.Join(" / ",labels))+(name==""?"":"\n"+name);
    }
    void UpdatePlotHeaders()
    {
        if(rendering || workbook==null)return;
        for(int c=0;c<grid.Columns.Count;c++)
        {
            var old=grid.Columns[c].HeaderStyle;var context=old==null?null:old.Setters.OfType<Setter>().Where(s=>s.Property==FrameworkElement.ContextMenuProperty).Select(s=>s.Value as ContextMenu).FirstOrDefault();
            grid.Columns[c].Header=HeaderTitle(c);grid.Columns[c].HeaderStyle=PlotHeader(c,context);
        }
    }
    Style PlotHeader(int column,ContextMenu existing=null)
    {
        var menu=existing;
        if(menu==null)
        {
            menu=new ContextMenu();Item(menu,"Als X-Achse verwenden",delegate{PlotAxis(column,0);});Item(menu,"Als Y-Achse verwenden",delegate{PlotAxis(column,1);});Item(menu,"Als zweite Y-Reihe verwenden",delegate{PlotAxis(column,2);});Item(menu,"Als Gruppierungsvariable / Farbe verwenden",delegate{PlotAxis(column,3);});menu.Items.Add(new Separator());Item(menu,"Plot erstellen",delegate{SetPlotsVisible(true);});
            var popup=menu;
            popup.Opened+=delegate
            {
                var header=popup.PlacementTarget as System.Windows.Controls.Primitives.DataGridColumnHeader;if(header==null)return;
                header.SetResourceReference(Control.BackgroundProperty,"Plot.Pick");header.SetResourceReference(Control.BorderBrushProperty,"App.Accent");header.BorderThickness=new Thickness(2);
            };
            popup.Closed+=delegate
            {
                var header=popup.PlacementTarget as System.Windows.Controls.Primitives.DataGridColumnHeader;if(header==null)return;
                header.ClearValue(Control.BackgroundProperty);header.ClearValue(Control.BorderBrushProperty);header.ClearValue(Control.BorderThicknessProperty);
            };
        }
        var style=new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader),(Style)grid.FindResource(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)));style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty,menu));
        var roles=plots==null?null:plots.RolesFor(Active);string key=roles==null?null:roles.Group==column?"Plot.RoleGroup":roles.X==column?"Plot.RoleX":roles.Y==column?"Plot.RoleY":roles.Y2==column?"Plot.RoleY2":null;
        if(key!=null){style.Setters.Add(new Setter(Control.BackgroundProperty,new DynamicResourceExtension(key)));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1,2,1,2)));style.Setters.Add(new Setter(Control.BorderBrushProperty,new DynamicResourceExtension("App.Accent")));}
        return style;
    }
    void SetBook(SheetImport imported,string fileName,bool viewing)
    {
        plots.Reset();
        rendering=true;
        try{workbook=imported.Sheets;path=fileName;sourceText=imported.Text;delimiter=imported.Separator;importNote=imported.Note;changed.Clear();
            editing=!viewing;results=false;headings=viewing && CellsFor(0).GetLength(0)>1;editItem.IsChecked=editing;resultItem.IsChecked=results;headingsItem.IsChecked=headings;
            worksheets.Items.Clear();foreach(var sheet in workbook)worksheets.Items.Add(new TabItem {Header=sheet.Name});worksheets.SelectedIndex=0;
            if(sourceText!=null && fileName!=null)workbook[0].ExportPath=fileName;
        }finally{rendering=false;}
        selectedRow=Offset;selectedCol=0;Render();FitColumns();UpdateTitle();
    }
    string[,] CellsFor(int index){return workbook[index].Cells;}
    internal void OpenPath(string fileName)
    {
        var imported=SheetFiles.Read(fileName,forcedSeparator);SetBook(imported,fileName,!Path.GetExtension(fileName).Equals(".tabelle",StringComparison.OrdinalIgnoreCase));
    }
    void SafeOpen(string fileName){try{OpenPath(Path.GetFullPath(fileName));}catch(Exception ex){MessageBox.Show(ex.Message,"Datei konnte nicht geöffnet werden",MessageBoxButton.OK,MessageBoxImage.Error);}}
    void Open(){if(!CanLeave())return;var dialog=new OpenFileDialog {Filter=SheetFiles.Filter};if(dialog.ShowDialog()==true)SafeOpen(dialog.FileName);}
    void New(){if(!CanLeave())return;SetBook(new SheetImport {Sheets=new List<Formats.Worksheet>{new Formats.Worksheet {Name="Tabelle 1",Cells=new string[100,26]}},Note="Neue Tabelle · einfache Formeln beginnen mit ="},null,false);}
    void Sync(){grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);}
    void MarkChanged(){changed.Add(Active);UpdateTitle();UpdateStatus();plots.SourceChanged();}
    void UpdateTitle(){Title=(changed.Count>0?"● ":"")+(path==null?"Unbenannt":Path.GetFileName(path))+" · Tabelle";}
    bool CanLeave()
    {
        Sync();return Ui.ConfirmSave(changed.Count>0,delegate
        {
            foreach(var sheet in changed.ToArray()){worksheets.SelectedIndex=workbook.IndexOf(sheet);if(!Save())return false;}return true;
        });
    }
    void RequireEditing(){if(!editing)throw new InvalidOperationException("Unter Bearbeiten → Bearbeitung aktivieren kann die Tabelle geändert werden.");Sync();}
    void Resize(int rows,int columns)
    {
        SheetFiles.Bounds(rows,columns);var next=new string[rows,columns];for(int r=0;r<Cells.GetLength(0);r++)for(int c=0;c<Cells.GetLength(1);c++)next[r,c]=Cells[r,c];Active.Cells=next;MarkChanged();Render();
    }
    void Render()
    {
        rendering=true;
        try
        {
            table=new DataTable();grid.Columns.Clear();
            for(int c=0;c<Cells.GetLength(1);c++)
            {
                table.Columns.Add("C"+c,typeof(string));string title=HeaderTitle(c);
                var display=new Style(typeof(TextBlock));display.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(12,8,12,8)));display.Setters.Add(new Setter(TextBlock.TextWrappingProperty,wrap?TextWrapping.Wrap:TextWrapping.NoWrap));display.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));display.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center));
                grid.Columns.Add(new DataGridTextColumn {Header=title,HeaderStyle=PlotHeader(c),Binding=new System.Windows.Data.Binding("C"+c),ElementStyle=display,MinWidth=100,MaxWidth=900,Width=new DataGridLength(160)});
            }
            for(int r=Offset;r<Cells.GetLength(0);r++)
            {
                var row=table.NewRow();for(int c=0;c<Cells.GetLength(1);c++)
                {
                    string value=Cells[r,c]??"";if(results && value.StartsWith("=")){try{value=Sheet.Value(Cells,r,c,new HashSet<string>()).ToString("G12",CultureInfo.GetCultureInfo("de-DE"));}catch(Exception ex){value=ex is FormatException?ex.Message:"#FORMEL!";}}row[c]=value;
                }table.Rows.Add(row);
            }
            var rendered=table;var sheet=Active;int offset=Offset;
            rendered.ColumnChanged+=delegate(object s,DataColumnChangeEventArgs e)
            {
                int row=rendered.Rows.IndexOf(e.Row);if(row<0 || !editing || results)return;string value=Convert.ToString(e.ProposedValue);
                if(sheet.Cells[row+offset,e.Column.Ordinal]!=value){sheet.Cells[row+offset,e.Column.Ordinal]=value;changed.Add(sheet);UpdateTitle();UpdateStatus();plots.SourceChanged();}
            };
            grid.ItemsSource=table.DefaultView;grid.IsReadOnly=!editing || results;formula.IsReadOnly=!editing;applyButton.IsEnabled=editing;
            grid.FrozenColumnCount=frozen?1:0;headingsItem.IsChecked=headings;
            selectedRow=Math.Min(Math.Max(Offset,selectedRow),Cells.GetLength(0)-1);selectedCol=Math.Min(selectedCol,Cells.GetLength(1)-1);
        }
        finally{rendering=false;}
        SelectCell(selectedRow,selectedCol);UpdateStatus();plots.SourceChanged();
    }
    void ShowCell()
    {
        if(selectedRow<0 || selectedRow>=Cells.GetLength(0) || selectedCol<0 || selectedCol>=Cells.GetLength(1))return;
        address.Text=Sheet.Column(selectedCol)+(selectedRow+1);formula.Text=Cells[selectedRow,selectedCol]??"";
    }
    internal void SelectCell(int row,int col)
    {
        if(row<Offset || row>=Cells.GetLength(0) || col<0 || col>=Cells.GetLength(1)){ShowCell();return;}
        selectedRow=row;selectedCol=col;var item=table.DefaultView[row-Offset];grid.SelectedCells.Clear();grid.CurrentCell=new DataGridCellInfo(item,grid.Columns[col]);grid.SelectedCells.Add(grid.CurrentCell);grid.ScrollIntoView(item,grid.Columns[col]);ShowCell();
    }
    void TryGoTo(string text)
    {
        var match=Regex.Match(text.Trim(),@"^([A-Za-z]+)([1-9]\d*)$");int row,col=0;
        if(!match.Success || !int.TryParse(match.Groups[2].Value,out row)){MessageBox.Show("Bitte eine Zelladresse wie D25 eingeben.");return;}
        try{foreach(char letter in match.Groups[1].Value.ToUpperInvariant())col=checked(col*26+letter-'A'+1);}catch(OverflowException){MessageBox.Show("Zelladresse liegt außerhalb des Blatts.");return;}
        if(row>Cells.GetLength(0) || col>Cells.GetLength(1)){MessageBox.Show("Zelladresse liegt außerhalb des Blatts.");return;}
        if(row==1 && headings){headings=false;headingsItem.IsChecked=false;Render();}SelectCell(row-1,col-1);grid.Focus();
    }
    void ApplyFormula()
    {
        if(!editing)return;string value=formula.Text;Sync();if(selectedRow<0 || selectedRow>=Cells.GetLength(0))return;
        Cells[selectedRow,selectedCol]=value;MarkChanged();Render();
    }
    void UpdateStatus()
    {
        modeLabel.Text=editing?(results?"Bearbeitung · Ergebnisansicht":"Bearbeitung aktiviert"):"Leseansicht";
        status.Text=Cells.GetLength(0)+" Zeilen · "+Cells.GetLength(1)+" Spalten · "+Active.Name+" · "+(changed.Count>0?"Ungespeicherte Änderungen · ":"")+"Offline";
        note.Text=importNote;
    }
    void FitColumns()
    {
        if(table==null)return;
        for(int c=0;c<grid.Columns.Count;c++)
        {
            int length=headings?(Cells[0,c]??"").Length:2;
            for(int r=Offset;r<Math.Min(Cells.GetLength(0),Offset+200);r++)foreach(string line in (Cells[r,c]??"").Split('\n'))length=Math.Max(length,line.Length);
            grid.Columns[c].Width=new DataGridLength(Math.Max(140,Math.Min(420,32+length*grid.FontSize*0.56)));
        }
    }
    void ChangeSeparator(char? value)
    {
        Sync();
        if(sourceText!=null)
        {
            if(!CanLeave())return;var imported=SheetFiles.FromText(sourceText,value,Active.Name);SetBook(imported,path,true);
        }
        forcedSeparator=value;foreach(var item in separatorItems)item.IsChecked=object.Equals(item.Tag,value);
        if(value.HasValue)delimiter=value.Value;
    }
    void ShowSearch(){searchBar.Visibility=Visibility.Visible;search.Focus();search.SelectAll();}
    void FindNext()
    {
        string query=search.Text;if(string.IsNullOrWhiteSpace(query))return;Sync();
        int rows=Cells.GetLength(0),columns=Cells.GetLength(1),total=rows*columns,start=selectedRow*columns+selectedCol;
        for(int i=1;i<=total;i++)
        {
            int position=(start+i)%total,r=position/columns,c=position%columns;
            if((Cells[r,c]??"").IndexOf(query,StringComparison.CurrentCultureIgnoreCase)<0)continue;
            if(r==0 && headings){headings=false;headingsItem.IsChecked=false;Render();}SelectCell(r,c);return;
        }
        MessageBox.Show("Kein passender Zellinhalt gefunden.","Suchen");
    }
    bool Save(bool saveAs=false)
    {
        Sync();string suggested=Active.ExportPath??(Active.Name+(path==null?".tabelle":".csv"));string target=null;
        if(!saveAs && path!=null && Path.GetExtension(path).Equals(".tabelle",StringComparison.OrdinalIgnoreCase))target=path;
        if(target==null)
        {
            var dialog=new SaveFileDialog {Filter="Tabelle-Datei (*.tabelle)|*.tabelle|CSV (*.csv)|*.csv|TSV (*.tsv)|*.tsv",FileName=Path.GetFileName(suggested),DefaultExt=Path.GetExtension(suggested),FilterIndex=Path.GetExtension(suggested)==".csv"?2:Path.GetExtension(suggested)==".tsv"?3:1,AddExtension=true};
            if(dialog.ShowDialog()!=true)return false;target=dialog.FileName;
        }
        try
        {
            string extension=Path.GetExtension(target).ToLowerInvariant();
            if(extension!=".tabelle" && extension!=".csv" && extension!=".tsv" && extension!=".txt")throw new FormatException("Bitte eine Endung .tabelle, .csv oder .tsv wählen.");
            char output=extension==".tabelle"?';':extension==".tsv"?'\t':delimiter;
            int lastRow=0,lastCol=0;for(int r=0;r<Cells.GetLength(0);r++)for(int c=0;c<Cells.GetLength(1);c++)if(!string.IsNullOrEmpty(Cells[r,c])){lastRow=Math.Max(lastRow,r);lastCol=Math.Max(lastCol,c);}
            Ui.AtomicWrite(target,delegate(string temporary)
            {
                using(var writer=new StreamWriter(temporary,false,new UTF8Encoding(true)))for(int r=0;r<=lastRow;r++){var fields=new List<string>();for(int c=0;c<=lastCol;c++)fields.Add(Sheet.Quote(Cells[r,c]??""));writer.WriteLine(string.Join(output.ToString(),fields));}
            });
            Active.ExportPath=target;changed.Remove(Active);if(workbook.Count==1){path=target;sourceText=File.ReadAllText(path);delimiter=output;}UpdateTitle();UpdateStatus();return true;
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"Speichern fehlgeschlagen");return false;}
    }
    const string GridStyles=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='{x:Type DataGridColumnHeader}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Selected}'/><Setter Property='BorderBrush' Value='{DynamicResource App.Border}'/><Setter Property='BorderThickness' Value='0,0,1,1'/><Setter Property='Padding' Value='12,6'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type DataGridColumnHeader}'><Grid><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter VerticalAlignment='Center'/></Border><Thumb x:Name='PART_LeftHeaderGripper' HorizontalAlignment='Left' Width='5' Opacity='0' Cursor='SizeWE'/><Thumb x:Name='PART_RightHeaderGripper' HorizontalAlignment='Right' Width='5' Opacity='0' Cursor='SizeWE'/></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type DataGridRowHeader}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Selected}'/><Setter Property='FontWeight' Value='SemiBold'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type DataGridRowHeader}'><Border Background='{TemplateBinding Background}' BorderBrush='{DynamicResource App.Border}' BorderThickness='0,0,1,1' Padding='6'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsRowSelected' Value='True'><Setter Property='Background' Value='{DynamicResource App.Hover}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type DataGridCell}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='BorderThickness' Value='1'/><Setter Property='BorderBrush' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type DataGridCell}'><Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'><ContentPresenter VerticalAlignment='Center'/></Border></ControlTemplate></Setter.Value></Setter><Style.Triggers><Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='{DynamicResource App.Selected}'/><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='BorderBrush' Value='{DynamicResource App.Accent}'/></Trigger></Style.Triggers></Style>
</ResourceDictionary>";
}
