using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

internal sealed class PageSurface : ScrollViewer
{
    readonly DocumentEditor editor;
    readonly RulerCanvas horizontal=new RulerCanvas(true) {Height=28},vertical=new RulerCanvas(false) {Width=28};
    readonly Canvas guides=new Canvas {IsHitTestVisible=false};
    readonly Grid paper=new Grid();
    readonly Thumb[] handles=new Thumb[4];
    bool revealQueued;
    public bool MouseMargins {get;private set;}
    public PageSurface(DocumentEditor editor)
    {
        this.editor=editor;HorizontalScrollBarVisibility=ScrollBarVisibility.Auto;VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
        Margin=new Thickness(16,12,16,0);Padding=new Thickness(8);
        var root=new Grid {HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top};
        root.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});root.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});Content=root;
        var unit=new TextBlock {Text="cm",FontSize=10,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};Theme.Bind(unit,TextBlock.ForegroundProperty,"App.Muted");root.Children.Add(unit);
        root.Children.Add(horizontal);Grid.SetColumn(horizontal,1);root.Children.Add(vertical);Grid.SetRow(vertical,1);
        paper.Width=DocumentMargins.Pixels(210);paper.MinHeight=DocumentMargins.Pixels(297);Grid.SetRow(paper,1);Grid.SetColumn(paper,1);root.Children.Add(paper);
        editor.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;editor.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;
        editor.MinHeight=paper.MinHeight;editor.BorderThickness=new Thickness(0);paper.Children.Add(editor);paper.Children.Add(guides);
        var edge=new Border {BorderThickness=new Thickness(1),IsHitTestVisible=false};Theme.Bind(edge,Border.BorderBrushProperty,"App.Border");paper.Children.Add(edge);
        editor.LayoutChanged+=DrawRulers;paper.SizeChanged+=delegate{DrawRulers();};
        editor.RevealPosition+=delegate(int offset){Reveal(editor.Pointer(offset));};
        editor.SelectionChanged+=delegate{if(!editor.IsKeyboardFocusWithin || revealQueued)return;revealQueued=true;Dispatcher.BeginInvoke(new Action(delegate{revealQueued=false;if(editor.IsKeyboardFocusWithin)RevealCaret();}),System.Windows.Threading.DispatcherPriority.Background);};
        ScrollChanged+=delegate{editor.SetProofMarksVisibleChanged();};
        for(int side=0;side<4;side++)
        {
            int index=side;var thumb=new Thumb {Width=14,Height=14,Cursor=side<2?System.Windows.Input.Cursors.SizeWE:System.Windows.Input.Cursors.SizeNS,ToolTip=new[]{"Linker Rand","Rechter Rand","Oberer Rand","Unterer Rand"}[side]};
            Theme.Bind(thumb,Control.BackgroundProperty,"App.Accent");handles[side]=thumb;(side<2?horizontal:vertical).Children.Add(thumb);
            thumb.DragDelta+=delegate(object sender,DragDeltaEventArgs e){DragMargin(index,index<2?e.HorizontalChange:e.VerticalChange);};
            thumb.DragCompleted+=delegate{editor.Focus();};
        }
        DrawRulers();
    }
    public void SetMouseMargins(bool enabled){MouseMargins=enabled;DrawRulers();}
    public void RefreshRulers(){DrawRulers();}
    public void RevealCaret(){Reveal(editor.CaretPosition);}
    void Reveal(TextPointer pointer)
    {
        if(!pointer.HasValidLayout)return;var rect=pointer.GetCharacterRect(LogicalDirection.Forward);if(rect.IsEmpty)return;
        var root=Content as Visual;if(root==null)return;var position=editor.TransformToAncestor(root).TransformBounds(rect);
        if(position.Bottom>VerticalOffset+ViewportHeight-20)ScrollToVerticalOffset(position.Bottom-ViewportHeight+20);
        else if(position.Top<VerticalOffset+12)ScrollToVerticalOffset(Math.Max(0,position.Top-12));
        if(position.Right>HorizontalOffset+ViewportWidth-20)ScrollToHorizontalOffset(position.Right-ViewportWidth+20);
        else if(position.Left<HorizontalOffset+12)ScrollToHorizontalOffset(Math.Max(0,position.Left-12));
    }
    internal void DragMargin(int side,double pixels)
    {
        var value=editor.PageMargins;double delta=DocumentMargins.Millimeters(pixels)*(side==1 || side==3?-1:1);
        if(side==0)value.Left=Clamp(value.Left+delta);else if(side==1)value.Right=Clamp(value.Right+delta);else if(side==2)value.Top=Clamp(value.Top+delta);else value.Bottom=Clamp(value.Bottom+delta);
        editor.SetMargins(value);DrawRulers();
    }
    static double Clamp(double value){return Math.Round(Math.Max(0,Math.Min(50,value)),1);}
    void DrawRulers()
    {
        // Reuse the four drag controls; guides are purely visual and never become text.
        horizontal.Children.Clear();vertical.Children.Clear();guides.Children.Clear();
        double width=paper.Width,height=Math.Max(paper.MinHeight,paper.ActualHeight);
        horizontal.Width=width;vertical.Height=height;
        Theme.Bind(horizontal,Panel.BackgroundProperty,"App.Panel");Theme.Bind(vertical,Panel.BackgroundProperty,"App.Panel");
        horizontal.InvalidateVisual();vertical.InvalidateVisual();
        var padding=editor.Padding;double[] positions={padding.Left,width-padding.Right,padding.Top,height-padding.Bottom};
        for(int i=0;i<4;i++)
        {
            var thumb=handles[i];if(thumb==null)continue;thumb.Visibility=MouseMargins?Visibility.Visible:Visibility.Collapsed;(i<2?horizontal:vertical).Children.Add(thumb);
            Canvas.SetLeft(thumb,i<2?positions[i]-7:14);Canvas.SetTop(thumb,i<2?14:positions[i]-7);
            if(MouseMargins){var line=new System.Windows.Shapes.Line {X1=i<2?positions[i]:0,X2=i<2?positions[i]:width,Y1=i<2?0:positions[i],Y2=i<2?height:positions[i],Stroke=Theme.Color("App.Accent"),StrokeThickness=1,StrokeDashArray=new DoubleCollection {3,4},Opacity=.6};guides.Children.Add(line);}
        }
    }
    sealed class RulerCanvas : Canvas
    {
        readonly bool horizontal;
        public RulerCanvas(bool horizontal){this.horizontal=horizontal;}
        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing);var brush=Theme.Color("App.Muted");var pen=new Pen(brush,1);var font=new Typeface("Segoe UI");double size=horizontal?ActualWidth:ActualHeight;
            for(int mm=0;DocumentMargins.Pixels(mm)<=size;mm++)
            {
                double position=DocumentMargins.Pixels(mm),length=mm%10==0?10:mm%5==0?6:3;
                drawing.DrawLine(pen,new Point(horizontal?position:28-length,horizontal?28-length:position),new Point(horizontal?position:28,horizontal?28:position));
                if(mm%10==0)drawing.DrawText(new FormattedText((mm/10).ToString(),CultureInfo.CurrentCulture,FlowDirection.LeftToRight,font,9,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(horizontal?position+2:1,horizontal?1:position+2));
            }
        }
    }
}

internal sealed class PageMarginsWindow : Window
{
    readonly DocumentEditor editor;
    readonly DocumentMargins original;
    readonly TextBox[] fields=new TextBox[4];
    readonly ComboBox unit=new ComboBox {ItemsSource=new[]{"cm","mm"},SelectedIndex=0,Width=85};
    readonly TextBlock state=new TextBlock {TextWrapping=TextWrapping.Wrap};
    bool filling,accepted;
    public PageMarginsWindow(DocumentEditor editor)
    {
        this.editor=editor;original=editor.PageMargins;Ui.Window(this,"Schreib · Seitenränder");Width=460;Height=480;MinWidth=430;MinHeight=460;WindowStartupLocation=WindowStartupLocation.CenterOwner;ResizeMode=ResizeMode.NoResize;
        var root=new StackPanel {Margin=new Thickness(24)};Content=root;
        root.Children.Add(new TextBlock {Text="Seitenränder mit direkter Vorschau",FontSize=20,Margin=new Thickness(0,0,0,12)});
        var units=new StackPanel {Orientation=Orientation.Horizontal};units.Children.Add(new TextBlock {Text="Einheit",Width=100,VerticalAlignment=VerticalAlignment.Center});units.Children.Add(unit);root.Children.Add(units);
        string[] labels={"Links","Rechts","Oben","Unten"};
        for(int i=0;i<4;i++){var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)};row.Children.Add(new TextBlock {Text=labels[i],Width=100,VerticalAlignment=VerticalAlignment.Center});fields[i]=new TextBox {Width=100,Padding=new Thickness(6)};row.Children.Add(fields[i]);root.Children.Add(row);fields[i].TextChanged+=delegate{Preview();};}
        unit.SelectionChanged+=delegate{Fill(editor.PageMargins);};
        var presets=new WrapPanel {Margin=new Thickness(0,14,0,8)};root.Children.Add(presets);
        Ui.Button(presets,"Normal",delegate{Preset(25);});Ui.Button(presets,"Schmal",delegate{Preset(12.7);});Ui.Button(presets,"Breit",delegate{Preset(30);});
        state.FontSize=12;root.Children.Add(state);
        var buttons=new WrapPanel {Margin=new Thickness(0,14,0,0)};root.Children.Add(buttons);
        Ui.Button(buttons,"Übernehmen",delegate{if(!Preview())return;accepted=true;DialogResult=true;}).IsDefault=true;
        Ui.Button(buttons,"Abbrechen",delegate{DialogResult=false;}).IsCancel=true;
        Closed+=delegate{if(!accepted)editor.SetMargins(original,false);else if(!original.Same(editor.PageMargins))editor.CommitLayout();};Fill(original);
    }
    void Preset(double mm){var value=new DocumentMargins {Left=mm,Right=mm,Top=mm,Bottom=mm};editor.SetMargins(value,false);Fill(value);}
    void Fill(DocumentMargins value){filling=true;double divisor=unit.SelectedIndex==0?10:1;double[] values={value.Left,value.Right,value.Top,value.Bottom};for(int i=0;i<4;i++)fields[i].Text=(values[i]/divisor).ToString("0.##",CultureInfo.GetCultureInfo("de-DE"));filling=false;state.Text="0–5 cm / 0–50 mm je Rand · Änderungen erscheinen sofort im Blatt.";}
    bool Preview()
    {
        if(filling)return true;var values=new double[4];for(int i=0;i<4;i++)if(!DocumentMargins.TryLength(fields[i].Text,unit.SelectedIndex==0,out values[i])){state.Text="Bitte gültige Ränder von 0 bis 5 cm / 50 mm eingeben.";return false;}
        editor.SetMargins(new DocumentMargins {Left=values[0],Right=values[1],Top=values[2],Bottom=values[3]},false);state.Text="Vorschau aktiv · Übernehmen speichert die Ränder im Dokument.";return true;
    }
}
