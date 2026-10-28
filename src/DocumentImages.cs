using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

internal static class DocumentImages
{
    public static BitmapSource Read(string path,bool compact)
    {
        if(new FileInfo(path).Length>50*1024*1024)throw new FormatException("Bitte ein Bild unter 50 MB wählen.");
        BitmapSource source;
        using(var stream=File.OpenRead(path))
        {
            var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
            source=decoder.Frames[0];
            if((long)source.PixelWidth*source.PixelHeight>40000000)throw new FormatException("Bitte ein Bild mit höchstens 40 Megapixeln wählen.");
            if(compact && Math.Max(source.PixelWidth,source.PixelHeight)>1600)
            {double scale=1600.0/Math.Max(source.PixelWidth,source.PixelHeight);source=new TransformedBitmap(source,new ScaleTransform(scale,scale));}
            // PNG retains transparency. JPEG is used only for opaque, compact images.
            BitmapEncoder encoder=compact && !source.Format.ToString().Contains("a") && !source.Format.ToString().Contains("A")?(BitmapEncoder)new JpegBitmapEncoder {QualityLevel=85}:new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));using(var copy=new MemoryStream()){encoder.Save(copy);copy.Position=0;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=copy;image.EndInit();source=image;}
        }
        source.Freeze();return source;
    }
    public static Image Make(BitmapSource source,double width)
    {
        return new Image {Source=DocumentEditor.UndoableImage(source),Width=width,Height=width*source.PixelHeight/source.PixelWidth,Stretch=Stretch.Uniform,ToolTip="Bild · wird von der Schreibprüfung nicht erfasst"};
    }
    public static void Insert(DocumentEditor editor,BitmapSource bitmap,double width,string caption,bool above)
    {
        if(editor.IsReadOnly)throw new InvalidOperationException("Bitte zuerst den Lesemodus ausschalten.");
        double available=DocumentMargins.Pixels(210-editor.PageMargins.Left-editor.PageMargins.Right);
        double maxHeight=DocumentMargins.Pixels(297-editor.PageMargins.Top-editor.PageMargins.Bottom)-70;
        width=Math.Min(width,Math.Min(available,maxHeight*bitmap.PixelWidth/bitmap.PixelHeight));
        if(double.IsNaN(width) || double.IsInfinity(width) || width<10)throw new FormatException("Die Bildbreite ist ungültig.");
        editor.BeginChange();
        try
        {
            editor.SelectedText="";var position=editor.Selection.Start.InsertParagraphBreak();
            var next=position.Paragraph;if(next==null)throw new InvalidOperationException("Hier kann kein Bild eingefügt werden.");
            var photo=new Paragraph(new InlineUIContainer(Make(bitmap,width))) {Margin=new Thickness(0,8,0,4),TextAlignment=TextAlignment.Center,KeepTogether=true,KeepWithNext=!above && !string.IsNullOrWhiteSpace(caption)};
            var label=new Paragraph(new Run(caption??"")) {FontSize=12,FontStyle=FontStyles.Italic,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,3,0,8),KeepTogether=true,KeepWithNext=above};
            var blocks=next.SiblingBlocks;
            if(above && !string.IsNullOrWhiteSpace(caption))blocks.InsertBefore(next,label);
            blocks.InsertBefore(next,photo);
            if(!above && !string.IsNullOrWhiteSpace(caption))blocks.InsertBefore(next,label);
            editor.CaretPosition=next.ContentStart;editor.Focus();
        }
        finally{editor.EndChange();}
    }
    public static void Choose(DocumentEditor editor,Window owner)
    {
        if(editor.IsReadOnly)throw new InvalidOperationException("Bitte zuerst den Lesemodus ausschalten.");
        var file=new OpenFileDialog {Filter="Bilder (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"};if(file.ShowDialog(owner)!=true)return;
        var dialog=new Window {Title="Bild einfügen",Width=460,MinHeight=430,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,Owner=owner,WindowStartupLocation=WindowStartupLocation.CenterOwner};Theme.Install(dialog);
        var panel=new StackPanel {Margin=new Thickness(20)};dialog.Content=panel;
        panel.Children.Add(new TextBlock {Text=Path.GetFileName(file.FileName),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
        var compact=new CheckBox {Content="Kompakt: maximal 1600 Pixel, JPEG-Qualität 85 %",IsChecked=true,Margin=new Thickness(0,0,0,12)};panel.Children.Add(compact);
        panel.Children.Add(new TextBlock {Text="Ohne Häkchen: volle Pixelauflösung. Die Anzeige wird an das Blatt angepasst.",TextWrapping=TextWrapping.Wrap});
        panel.Children.Add(new TextBlock {Text="Anzeigebreite in cm",Margin=new Thickness(0,12,0,4)});var width=new TextBox {Text="12",Padding=new Thickness(6)};panel.Children.Add(width);
        panel.Children.Add(new TextBlock {Text="Bildbeschriftung (optional)",Margin=new Thickness(0,12,0,4)});var caption=new TextBox {Padding=new Thickness(6),MaxLength=1000};panel.Children.Add(caption);
        var above=new CheckBox {Content="Beschriftung über dem Bild",Margin=new Thickness(0,10,0,12)};panel.Children.Add(above);
        var buttons=new WrapPanel();panel.Children.Add(buttons);Ui.Button(buttons,"Einfügen",delegate{double cm;if(!double.TryParse(width.Text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out cm) || cm<=0 || cm>21)throw new FormatException("Bitte eine Bildbreite über 0 und bis 21 cm angeben.");var bitmap=Read(file.FileName,compact.IsChecked==true);Insert(editor,bitmap,DocumentMargins.Pixels(cm*10),caption.Text,above.IsChecked==true);dialog.DialogResult=true;});Ui.Button(buttons,"Abbrechen",delegate{dialog.Close();});dialog.ShowDialog();
    }
}
