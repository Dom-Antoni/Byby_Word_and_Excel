using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;

// Rasterize our path-only vector sources and write a multi-resolution Windows ICO.
// No external programs, downloaded artwork, fonts or image libraries are needed.
internal static class IconBuilder
{
    static readonly int[] Sizes={16,20,24,32,40,48,64,128,256};
    [STAThread]public static int Main(string[] args)
    {
        try
        {
            if(args.Length!=2)throw new ArgumentException("IconBuilder <SVG-Quellordner> <Ausgabeordner>");
            Directory.CreateDirectory(args[1]);
            foreach(string name in new[]{"Tabelle","Schreib"})
            {
                string path=Path.Combine(args[0],name+".svg");XDocument svg;
                using(var reader=XmlReader.Create(path,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))svg=XDocument.Load(reader);
                var frames=Sizes.Select(size=>Png(svg,size)).ToList();
                using(var stream=File.Create(Path.Combine(args[1],name+".ico")))
                using(var writer=new BinaryWriter(stream))
                {
                    writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)Sizes.Length);
                    int offset=6+16*Sizes.Length;
                    for(int i=0;i<Sizes.Length;i++)
                    {
                        writer.Write((byte)(Sizes[i]==256?0:Sizes[i]));writer.Write((byte)(Sizes[i]==256?0:Sizes[i]));
                        writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);
                        writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;
                    }
                    foreach(byte[] frame in frames)writer.Write(frame);
                }
                File.WriteAllBytes(Path.Combine(args[1],name+".png"),frames[frames.Count-1]);
                File.Copy(path,Path.Combine(args[1],name+".svg"),true);
            }
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
    }
    static Brush Color(string text){return text==null || text=="none"?null:(Brush)new BrushConverter().ConvertFromInvariantString(text);}
    static byte[] Png(XDocument svg,int size)
    {
        XNamespace ns="http://www.w3.org/2000/svg";var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size/64.0,size/64.0));
            foreach(var path in svg.Root.Elements(ns+"path"))
            {
                var geometry=Geometry.Parse("F0 "+(string)path.Attribute("d"));
                Brush fill=Color((string)path.Attribute("fill")),stroke=Color((string)path.Attribute("stroke"));
                Pen pen=null;
                if(stroke!=null)
                {
                    pen=new Pen(stroke,double.Parse((string)path.Attribute("stroke-width")??"1",CultureInfo.InvariantCulture));
                    if((string)path.Attribute("stroke-linecap")=="round")pen.StartLineCap=pen.EndLineCap=PenLineCap.Round;
                }
                dc.DrawGeometry(fill,pen,geometry);
            }
            dc.Pop();
        }
        var bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}
    }
}
