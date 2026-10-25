using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

internal sealed class PlotShape
{
    public string Type,Fill="none",Stroke="none",Text,Anchor="start",Tip;
    public double X,Y,W,H,Radius,Size=12,Weight=1,Rotate;
    public bool Bold;
    public List<Point> Points;
}
internal sealed class PlotScene
{
    public double Width,Height;
    public string Background;
    public List<PlotShape> Shapes=new List<PlotShape>();
}
internal static class PlotDrawing
{
    static readonly string[] Colors={"#2675D8","#0D947C"};
    static string Trim(string value,int length){value=(value??"").Replace("\r"," ").Replace("\n"," ");return value.Length>length?value.Substring(0,Math.Max(1,length-1))+"…":value;}
    static void Text(PlotScene scene,string text,double x,double y,double size,string color,string anchor="start",bool bold=false,double rotate=0)
    {scene.Shapes.Add(new PlotShape {Type="text",Text=text,X=x,Y=y,Size=size,Fill=color,Anchor=anchor,Bold=bold,Rotate=rotate});}
    static void Line(PlotScene scene,double x,double y,double endX,double endY,string color,double weight=1)
    {scene.Shapes.Add(new PlotShape {Type="line",X=x,Y=y,W=endX,H=endY,Stroke=color,Weight=weight});}
    static void Rect(PlotScene scene,double x,double y,double width,double height,string fill,string tip=null,string stroke="none")
    {scene.Shapes.Add(new PlotShape {Type="rect",X=x,Y=y,W=Math.Max(.5,width),H=Math.Max(.5,height),Fill=fill,Stroke=stroke,Tip=tip});}
    static void Dot(PlotScene scene,double x,double y,double radius,string fill,string tip)
    {scene.Shapes.Add(new PlotShape {Type="circle",X=x,Y=y,Radius=radius,Fill=fill,Tip=tip});}
    static List<double> Ticks(double min,double max)
    {
        Plots.FiniteSpan(min,max);double span=max-min;if(span<=0)return new List<double>{min};
        double power=Math.Pow(10,Math.Floor(Math.Log10(span/5))),ratio=span/5/power;
        double step=(ratio<=1?1:ratio<=2?2:ratio<=2.5?2.5:ratio<=5?5:10)*power;
        if(step<=0 || double.IsInfinity(step))return new List<double>{min,max};
        var result=new List<double>();double first=Math.Ceiling(min/step)*step;
        for(int i=0;i<12;i++){double value=first+i*step;if(value>max+step*.00001)break;result.Add(value);}return result;
    }
    static void Range(ref double min,ref double max,bool zero)
    {
        Plots.FiniteSpan(min,max);if(zero){min=Math.Min(0,min);max=Math.Max(0,max);}
        if(min==max){double delta=min==0?1:Math.Max(double.Epsilon,Math.Abs(min)*.1);min-=delta;max+=delta;}
        double padding=(max-min)*.07;if(!zero || min<0)min-=padding;if(!zero || max>0)max+=padding;
        Plots.FiniteSpan(min,max);
    }
    public static PlotScene Scene(PlotData data,double width,double height)
    {
        if(width<260 || height<140)throw new FormatException("Bitte den Plotbereich etwas vergrößern (↗).");
        var scene=new PlotScene {Width=width,Height=height,Background=data.Dark?"#18212D":"#FFFFFF"};
        string ink=data.Dark?"#EBF0F7":"#243248",muted=data.Dark?"#BCC8DA":"#52617A",grid=data.Dark?"#3A485C":"#E2E8F0";
        double scale=Math.Max(.7,Math.Min(Math.Max(.85,Math.Min(2,width/700)),height/300)),font=12*scale;
        bool horizontal=data.Kind==PlotKind.Bars;double left=(horizontal?125:76)*scale,right=26*scale,top=(data.Series.Count>1?78:56)*scale,bottom=88*scale;
        double pw=width-left-right,ph=height-top-bottom;
        if(ph<55){top=(data.Series.Count>1?65:45)*scale;bottom=55*scale;ph=height-top-bottom;}
        if(pw<90 || ph<35)throw new FormatException("Bitte den Plotbereich etwas vergrößern.");
        Text(scene,Trim(data.Title,(int)(width/(10*scale))),left,13*scale,18*scale,ink,"start",true);
        if(data.Series.Count>1)for(int n=0;n<data.Series.Count;n++){double x=left+n*pw/2;Rect(scene,x,44*scale,12*scale,10*scale,Colors[n%Colors.Length]);Text(scene,Trim(data.Series[n].Name,22),x+18*scale,41*scale,font,ink);}
        var all=data.Series.SelectMany(x=>x.Points).ToList();
        var ys=all.Select(x=>x.Y).Concat(data.Boxes.SelectMany(x=>new[]{x.Low,x.High}.Concat(x.Outliers))).ToArray();
        if(ys.Length==0)throw new FormatException("Keine darstellbaren Zahlenwerte vorhanden.");
        double ymin=ys.Min(),ymax=ys.Max();Range(ref ymin,ref ymax,data.Zero || data.Kind==PlotKind.Columns || horizontal || data.Kind==PlotKind.Histogram);
        double xmin=-.5,xmax=Math.Max(.5,data.Categories.Count-.5);
        if(data.NumericX)
        {
            xmin=all.Min(x=>x.X);xmax=all.Max(x=>x.X);
            if(data.Kind==PlotKind.Histogram){xmin-=data.BinWidth/2;xmax+=data.BinWidth/2;}
            else Range(ref xmin,ref xmax,false);
        }
        double yspan=ymax-ymin,xspan=xmax-xmin;Plots.FiniteSpan(xmin,xmax);
        Func<double,double> sx=x=>left+(x-xmin)/xspan*pw,sy=y=>top+ph-(y-ymin)/yspan*ph;
        Func<double,double> valueX=y=>left+(y-ymin)/yspan*pw;
        var ticks=Ticks(ymin,ymax);
        foreach(double tick in ticks)
        {
            if(horizontal){double x=valueX(tick);Line(scene,x,top,x,top+ph,grid);Text(scene,Plots.Short(tick),x,top+ph+10*scale,font,muted,"middle");}
            else{double y=sy(tick);Line(scene,left,y,left+pw,y,grid);Text(scene,Plots.Short(tick),left-10*scale,y-font*.55,font,muted,"end");}
        }
        Line(scene,left,top,left,top+ph,muted);Line(scene,left,top+ph,left+pw,top+ph,muted);
        if(horizontal)
        {
            double slot=ph/Math.Max(1,data.Categories.Count);
            for(int i=0;i<data.Categories.Count;i++)Text(scene,Trim(data.Categories[i],18),left-9*scale,top+(i+.5)*slot-font*.6,font,muted,"end");
            Text(scene,Trim(data.YLabel,60),left+pw/2,height-25*scale,font,ink,"middle");
        }
        else
        {
            if(data.NumericX)
            {
                foreach(double tick in Ticks(xmin,xmax))
                {
                    string label=Plots.Short(tick);if(data.DateX){try{label=DateTime.FromOADate(tick).ToString(xspan<2?"dd.MM HH:mm":"dd.MM.yy",CultureInfo.InvariantCulture);}catch(ArgumentException){}}
                    Text(scene,label,sx(tick),top+ph+10*scale,font,muted,"middle");
                }
            }
            else
            {
                int every=Math.Max(1,(int)Math.Ceiling(data.Categories.Count/Math.Max(1,pw/(75*scale))));
                for(int i=0;i<data.Categories.Count;i+=every)Text(scene,Trim(data.Categories[i],15),sx(i),top+ph+10*scale,font,muted,"middle");
            }
            Text(scene,Trim(data.XLabel,60),left+pw/2,height-25*scale,font,ink,"middle");
            Text(scene,Trim(data.YLabel,60),16*scale,top+ph/2,font,ink,"middle",false,-90);
        }
        double baseline=horizontal?valueX(0):sy(0);
        for(int n=0;n<data.Series.Count;n++)
        {
            var series=data.Series[n];string color=Colors[n%Colors.Length];
            if(data.Kind==PlotKind.Columns || horizontal)
            {
                double slot=(horizontal?ph:pw)/Math.Max(1,data.Categories.Count),bar=slot*.76/data.Series.Count;
                foreach(var point in series.Points)
                {
                    string tip=point.Label+" · "+series.Name+": "+Plots.Short(point.Y);
                    if(horizontal){double y=top+point.X*slot+slot*.12+n*bar,x=valueX(point.Y);Rect(scene,Math.Min(x,baseline),y,Math.Abs(x-baseline),Math.Max(1,bar-1),color,tip);}
                    else{double x=left+(point.X+.5)*slot-slot*.38+n*bar,y=sy(point.Y);Rect(scene,x,Math.Min(y,baseline),Math.Max(1,bar-1),Math.Abs(y-baseline),color,tip);}
                }
            }
            else if(data.Kind==PlotKind.Histogram)
            {
                foreach(var point in series.Points)Rect(scene,sx(point.X-data.BinWidth/2)+1,sy(point.Y),Math.Max(1,pw*data.BinWidth/xspan-2),sy(0)-sy(point.Y),color,point.Label+": "+Plots.Short(point.Y));
            }
            else
            {
                if(data.Kind==PlotKind.Line && series.Points.Count>1)scene.Shapes.Add(new PlotShape {Type="polyline",Stroke=color,Weight=2*scale,Points=series.Points.Select(p=>new Point(sx(p.X),sy(p.Y))).ToList()});
                int every=data.Kind==PlotKind.Line?Math.Max(1,series.Points.Count/150):1;
                for(int i=0;i<series.Points.Count;i+=every){var point=series.Points[i];Dot(scene,sx(point.X),sy(point.Y),(data.Kind==PlotKind.Scatter?3:2.5)*scale,color,point.Label+" · "+series.Name+": "+Plots.Short(point.Y));}
            }
        }
        for(int i=0;i<data.Boxes.Count;i++)
        {
            var box=data.Boxes[i];double x=sx(i),bw=Math.Min(65*scale,pw/Math.Max(1,data.Boxes.Count)*.6);string color=Colors[0];
            Line(scene,x,sy(box.Low),x,sy(box.High),color,1.5*scale);Line(scene,x-bw*.25,sy(box.Low),x+bw*.25,sy(box.Low),color);Line(scene,x-bw*.25,sy(box.High),x+bw*.25,sy(box.High),color);
            Rect(scene,x-bw/2,sy(box.Q3),bw,sy(box.Q1)-sy(box.Q3),data.Dark?"#234B7A":"#D3E5FD",box.Label+" · Median: "+Plots.Short(box.Median)+" · Q1: "+Plots.Short(box.Q1)+" · Q3: "+Plots.Short(box.Q3),color);
            Line(scene,x-bw/2,sy(box.Median),x+bw/2,sy(box.Median),color,2.5*scale);
            foreach(double outlier in box.Outliers.Take(1000))Dot(scene,x,sy(outlier),2.5*scale,color,box.Label+" · Ausreißer: "+Plots.Short(outlier));
        }
        return scene;
    }
    static Brush Brush(string color){return color=="none"?null:(Brush)new BrushConverter().ConvertFromInvariantString(color);}
    public static void Draw(DrawingContext dc,PlotScene scene)
    {
        dc.DrawRectangle(Brush(scene.Background),null,new Rect(0,0,scene.Width,scene.Height));
        foreach(var shape in scene.Shapes)
        {
            var pen=shape.Stroke=="none"?null:new Pen(Brush(shape.Stroke),shape.Weight);var fill=Brush(shape.Fill);
            if(shape.Type=="rect")dc.DrawRectangle(fill,pen,new Rect(shape.X,shape.Y,shape.W,shape.H));
            else if(shape.Type=="line")dc.DrawLine(pen,new Point(shape.X,shape.Y),new Point(shape.W,shape.H));
            else if(shape.Type=="circle")dc.DrawEllipse(fill,pen,new Point(shape.X,shape.Y),shape.Radius,shape.Radius);
            else if(shape.Type=="polyline")
            {
                var geometry=new StreamGeometry();using(var context=geometry.Open()){context.BeginFigure(shape.Points[0],false,false);context.PolyLineTo(shape.Points.Skip(1).ToArray(),true,false);}dc.DrawGeometry(null,pen,geometry);
            }
            else if(shape.Type=="text")
            {
                var text=new FormattedText(shape.Text??"",CultureInfo.GetCultureInfo("de-DE"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,shape.Bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),shape.Size,fill,1);
                double x=shape.X-(shape.Anchor=="middle"?text.Width/2:shape.Anchor=="end"?text.Width:0);
                if(shape.Rotate!=0)dc.PushTransform(new RotateTransform(shape.Rotate,shape.X,shape.Y));dc.DrawText(text,new Point(x,shape.Y));if(shape.Rotate!=0)dc.Pop();
            }
        }
    }
    static string N(double value){return value.ToString("0.######",CultureInfo.InvariantCulture);}
    public static XDocument Svg(PlotScene scene,string notice)
    {
        XNamespace ns="http://www.w3.org/2000/svg";var root=new XElement(ns+"svg",new XAttribute("width",N(scene.Width)),new XAttribute("height",N(scene.Height)),new XAttribute("viewBox","0 0 "+N(scene.Width)+" "+N(scene.Height)),new XAttribute("role","img"));
        var firstText=scene.Shapes.FirstOrDefault(x=>x.Type=="text");
        root.Add(new XElement(ns+"title",firstText==null?"Diagramm":firstText.Text),new XElement(ns+"desc",notice??""));
        root.Add(new XElement(ns+"rect",new XAttribute("width","100%"),new XAttribute("height","100%"),new XAttribute("fill",scene.Background)));
        foreach(var shape in scene.Shapes)
        {
            var element=new XElement(ns+shape.Type);
            if(shape.Type=="polyline")element.Add(new XAttribute("points",string.Join(" ",shape.Points.Select(p=>N(p.X)+","+N(p.Y)))));
            else if(shape.Type=="line")element.Add(new XAttribute("x1",N(shape.X)),new XAttribute("y1",N(shape.Y)),new XAttribute("x2",N(shape.W)),new XAttribute("y2",N(shape.H)));
            else if(shape.Type=="circle")element.Add(new XAttribute("cx",N(shape.X)),new XAttribute("cy",N(shape.Y)),new XAttribute("r",N(shape.Radius)));
            else element.Add(new XAttribute("x",N(shape.X)),new XAttribute("y",N(shape.Y)));
            if(shape.Type=="rect")element.Add(new XAttribute("width",N(shape.W)),new XAttribute("height",N(shape.H)));
            element.Add(new XAttribute("fill",shape.Fill),new XAttribute("stroke",shape.Stroke),new XAttribute("stroke-width",N(shape.Weight)));
            if(shape.Type=="text")
            {
                element.Add(new XAttribute("font-family","Segoe UI, Arial, sans-serif"),new XAttribute("font-size",N(shape.Size)),new XAttribute("text-anchor",shape.Anchor),new XAttribute("dominant-baseline","text-before-edge"));
                if(shape.Bold)element.Add(new XAttribute("font-weight","600"));
                if(shape.Rotate!=0)element.Add(new XAttribute("transform","rotate("+N(shape.Rotate)+" "+N(shape.X)+" "+N(shape.Y)+")"));element.Add(new XText(shape.Text??""));
            }
            if(shape.Tip!=null)element.Add(new XElement(ns+"title",shape.Tip));root.Add(element);
        }
        return new XDocument(new XDeclaration("1.0","utf-8",null),root);
    }
    public static void Export(PlotData data,string path,int width=1600,int height=1000)
    {
        string extension=Path.GetExtension(path).ToLowerInvariant();if(!new[]{".png",".jpg",".jpeg",".svg"}.Contains(extension))throw new FormatException("Bitte PNG, JPG oder SVG als Dateiendung wählen.");
        if(width<600 || width>4000 || height<400 || height>3000)throw new FormatException("Bitte eine Exportgröße zwischen 600 × 400 und 4000 × 3000 wählen.");
        var scene=Scene(data,width,height);
        Ui.AtomicWrite(path,delegate(string temporary)
        {
            if(extension==".svg"){using(var writer=new StreamWriter(temporary,false,new UTF8Encoding(false)))Svg(scene,data.Notice).Save(writer);return;}
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen())Draw(dc,scene);var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
            BitmapEncoder encoder=extension==".png"?(BitmapEncoder)new PngBitmapEncoder():new JpegBitmapEncoder {QualityLevel=94};encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(temporary))encoder.Save(stream);
        });
    }
}
internal sealed class PlotView : FrameworkElement
{
    public PlotData Data;
    PlotScene scene;
    public PlotView()
    {
        ClipToBounds=true;SizeChanged+=delegate{InvalidateVisual();};MouseMove+=delegate(object sender,System.Windows.Input.MouseEventArgs e)
        {
            if(scene==null)return;Point point=e.GetPosition(this);string tip=null;
            foreach(var shape in scene.Shapes.Where(x=>x.Tip!=null))
            {
                if(shape.Type=="rect" && new Rect(shape.X,shape.Y,shape.W,shape.H).Contains(point) || shape.Type=="circle" && (point-new Point(shape.X,shape.Y)).Length<shape.Radius+4){tip=shape.Tip;break;}
            }
            ToolTip=tip;
        };
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);scene=null;
        try{if(Data!=null){scene=PlotDrawing.Scene(Data,ActualWidth,ActualHeight);PlotDrawing.Draw(dc,scene);return;}}
        catch(FormatException ex){dc.DrawRectangle(Brushes.White,null,new Rect(RenderSize));dc.DrawText(new FormattedText(ex.Message,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,Brushes.DimGray,1){MaxTextWidth=Math.Max(50,ActualWidth-24)},new Point(12,20));return;}
        dc.DrawRectangle(Brushes.White,null,new Rect(RenderSize));
    }
}
