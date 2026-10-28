using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Web.Script.Serialization;

internal static class ExportChecks
{
    static int count;static string folder;static bool rIntegration;static readonly List<RExportResult> projects=new List<RExportResult>();
    static void Assert(bool value,string label){if(!value)throw new Exception(label);count++;}
    static IEnumerable<Image> Images(DependencyObject root){foreach(object child in LogicalTreeHelper.GetChildren(root)){var image=child as Image;if(image!=null)yield return image;var d=child as DependencyObject;if(d!=null)foreach(var nested in Images(d))yield return nested;}}
    [STAThread]public static int Main(string[] args)
    {
        try{rIntegration=args.Contains("--r-integration");new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"export-qa");Directory.CreateDirectory(folder);PicturesAndPdf();RProjects();Console.WriteLine(count+" Bild-, PDF- und R-Exportprüfungen bestanden. Vorschau: "+folder);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static void PicturesAndPdf()
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.AliceBlue,null,new Rect(0,0,2200,1000));for(int i=0;i<6;i++)dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(35,110,(byte)(100+i*20))),null,new Rect(100+i*300,850-i*100,180,100+i*100));}
        var bitmap=new RenderTargetBitmap(2200,1000,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));string input=Path.Combine(folder,"bild.png");using(var file=File.Create(input))encoder.Save(file);
        var compact=DocumentImages.Read(input,true);var original=DocumentImages.Read(input,false);Assert(compact.PixelWidth==1600 && original.PixelWidth==2200,"Kompakt und volle Auflösung");
        var editor=new DocumentEditor();editor.Text="Bilder, Texte und PDF\r\nEin Absatz vor dem Diagramm.\r\nHier geht der Text danach weiter.";editor.Select(editor.Text.IndexOf("Hier"),0);
        DocumentImages.Insert(editor,compact,400,"Abbildung 1: Messungen über sechs Wochen.",false);
        Assert(Images(editor.Document).Count()==1,"Bild eingefügt");Assert(editor.Text.Contains("Abbildung 1"),"Bildunterschrift als Text");Assert(editor.CaretPosition.Paragraph!=null && !Images(editor.CaretPosition.Paragraph).Any(),"Cursor nach Bild und Beschriftung");
        Assert(!editor.ProofText.Contains("bild.png") && editor.ProofText.Contains("Abbildung 1") && editor.ProofText.Length==editor.Text.Length,"Nur Text und Caption prüfen, Positionen erhalten");
        editor.Select(editor.Text.Length,0);DocumentImages.Insert(editor,original,300,"Abbildung 2: Überschrift über dem Bild.",true);Assert(Images(editor.Document).Count()==2,"Zweites Bild mit Überschrift");
        var saved=new WritingDocument {Rtf=editor.SaveRtf(),Margins=editor.PageMargins};string json=new JavaScriptSerializer {MaxJsonLength=50000000}.Serialize(saved);File.WriteAllText(Path.Combine(folder,"Bilder.schreib"),json);
        var restored=new DocumentEditor();restored.LoadRtf(new JavaScriptSerializer {MaxJsonLength=50000000}.Deserialize<WritingDocument>(json).Rtf);
        Assert(Images(restored.Document).Count()==2,"Bilder in .schreib/RTF wieder öffnen");Assert(Images(restored.Document).First().Source.Width>0 && Math.Abs(Images(restored.Document).First().Width-400)<3,"Bild und Anzeigegröße bleiben erhalten");
        int before=restored.Text.Length;restored.Select(restored.Text.IndexOf("Abbildung 1"),9);Assert(restored.SelectedText=="Abbildung","Textpositionen nach eingebettetem Bild");
        editor.Select(0,"Bilder, Texte und PDF".Length);editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,FontWeights.Bold);editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,26.0);
        for(int i=0;i<75;i++)editor.Document.Blocks.Add(new Paragraph(new Run("Absatz "+(i+1)+": Rechtschreibung, Größe und Umlaute ä ö ü ß bleiben als auswählbarer Text erhalten.")));
        editor.SetMargins(new DocumentMargins {Left=20,Right=25,Top=18,Bottom=22},false);editor.ApplyAppearance(true);string textBefore=editor.Text;int pages=PdfExport.Write(editor,Path.Combine(folder,"Dokument.pdf"),"PDF-Test mit Bildern");
        Assert(pages>=2,"Mehrseitiger PDF-Export");Assert(editor.Text==textBefore && Images(editor.Document).Count()==2,"PDF verändert Dokument nicht");
        using(var pdf=PdfSharp.Pdf.IO.PdfReader.Open(Path.Combine(folder,"Dokument.pdf"),PdfSharp.Pdf.IO.PdfDocumentOpenMode.ReadOnly)){Assert(pdf.PageCount==pages,"PDF wieder lesbar");Assert(Math.Abs(pdf.Pages[0].Width.Millimeter-210)<1,"PDF A4-Breite");Assert(pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>().Any(p=>p.Resources.Elements.ContainsKey("/XObject")),"Bilder in PDF eingebettet");}
        var undo=new DocumentEditor();using(var host=new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Bilder-offscreen") {Width=800,Height=600,PositionX=-32000,PositionY=-32000,WindowStyle=unchecked((int)0x80000000)})){host.RootVisual=undo;undo.Measure(new Size(800,600));undo.Arrange(new Rect(0,0,800,600));undo.UpdateLayout();undo.Text="vorher";undo.ClearUndo();undo.Select(6,0);DocumentImages.Insert(undo,compact,200,"Caption",false);undo.Undo();Assert(undo.Text=="vorher" && !Images(undo.Document).Any(),"Bild mit Caption als eine Aktion rückgängig: "+undo.Text.Replace("\r\n","|")+" / "+Images(undo.Document).Count());undo.Redo();Assert(Images(undo.Document).Count()==1 && undo.Text.Contains("Caption"),"Bild und Caption wiederherstellen: "+undo.Text.Replace("\r\n","|")+" / "+Images(undo.Document).Count()+" / "+undo.CanRedo);}
    }
    static void RProjects()
    {
        var source=new PlotSource {Name="Beispiel",Headings=true,Identity=new object(),Cells=new[,]{{"Datum","Standort","Messung A","Messung B","Kennung","Kommentar"},{"2026-01-01","Nord","1,5","3","001","Anführungszeichen \" und \\ sowie\nZeile"},{"2026-01-02","Süd","2,5","4","002","'); stop('injection') #"},{"2026-01-03","Nord","=C2+C3","5","003","NA"},{"2026-01-04","Süd","6,5","8","004",""}}};
        foreach(PlotKind kind in Enum.GetValues(typeof(PlotKind)))
        {
            var options=new PlotOptions {Kind=kind,X=kind==PlotKind.Line?0:kind==PlotKind.Scatter?3:1,Y=2,Y2=3,Group=1};var plot=Plots.Build(source,options);string target=Path.Combine(folder,"R-"+kind+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
            var result=RExport.Create(target,source,plot);projects.Add(result);Assert(File.Exists(result.Project) && File.Exists(result.Script),kind+" Projekt und Skript");
            Assert(File.Exists(Path.Combine(target,"plot.svg")) && File.Exists(Path.Combine(target,"plot.png")),kind+" Plotbilder");
            var rows=Sheet.Read(File.ReadAllText(Path.Combine(target,"plot_daten.csv")),',');Assert(rows.Count==1+(kind==PlotKind.Box?plot.Boxes.Count:plot.Series.Sum(s=>s.Points.Count)),kind+" dargestellte Werte exportieren");
            var raw=Sheet.Read(File.ReadAllText(Path.Combine(target,"daten_roh.csv")),',');var data=Sheet.Read(File.ReadAllText(Path.Combine(target,"daten.csv")),',');Assert(raw[3][2]=="=C2+C3" && data[3][2]=="4" && data[1][4]=="001",kind+" Originalformeln, Ergebnisse und IDs");
            bool duplicate=false;try{RExport.Create(target,source,plot);}catch(IOException){duplicate=true;}Assert(duplicate,"Existierende R-Projekte schützen");
        }
        string only=Path.Combine(folder,"R-Daten-"+Guid.NewGuid().ToString("N").Substring(0,6));var justData=RExport.Create(only,source);projects.Add(justData);Assert(!File.Exists(Path.Combine(only,"plot_daten.csv")),"Datenexport ohne Plot");
        File.WriteAllLines(Path.Combine(folder,"r-projects.txt"),projects.Select(p=>p.Directory),new System.Text.UTF8Encoding(false));
        if(rIntegration){Assert(RExport.FindRscript()!=null,"R für Integration vorhanden");foreach(var project in projects){RExport.PrepareObjects(project).GetAwaiter().GetResult();Assert(File.Exists(Path.Combine(project.Directory,"daten.rds")),project.Message);if(project!=justData)Assert(project.PlotObjectCreated,project.Message);}}
        Assert(RExport.Q("a\"\n\\b")=="\"a\\\"\\n\\\\b\"","R-Strings sicher maskieren");
        File.WriteAllText(Path.Combine(folder,"R-Pruefen.R"),@"args <- commandArgs(TRUE)
for (dir in readLines(args[1], encoding='UTF-8')) {
  if (!file.exists(file.path(dir, 'start.R'))) next
  source(file.path(dir, 'start.R'), encoding='UTF-8')
  stopifnot(is.data.frame(daten), is.numeric(daten[[3]]), daten[[3]][3] == 4, daten[[5]][1] == '001', daten_roh[[3]][3] == '=C2+C3', daten[[6]][3] == 'NA')
  if (file.exists(file.path(dir, 'plot_daten.csv'))) {
    p <- readRDS(file.path(dir, 'plot.rds'))
    stopifnot(inherits(p, 'ggplot'), nrow(ggplot_build(p)$data[[1]]) > 0)
    saveRDS(ggplot_build(p), file.path(dir, 'gebaut.rds'))
  }
}
cat('R/ggplot2: alle exportierten Projekte erfolgreich ausgeführt.\n')
",new System.Text.UTF8Encoding(false));
    }
}
