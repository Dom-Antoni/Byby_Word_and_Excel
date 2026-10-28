using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using M=MigraDoc.DocumentObjectModel;

// Convert document content, never editor adorners, selection, theme, or proofreading marks.
internal static class PdfExport
{
    static M.Unit Pt(double pixels){return M.Unit.FromPoint(pixels*.75);}
    static void Font(TextElement from,M.Font to)
    {
        GlyphTypeface glyph;to.Name=new Typeface(from.FontFamily,from.FontStyle,from.FontWeight,from.FontStretch).TryGetGlyphTypeface(out glyph)?from.FontFamily.Source.Split(',')[0]:"Arial";to.Size=Pt(from.FontSize);to.Bold=from.FontWeight.ToOpenTypeWeight()>=600;to.Italic=from.FontStyle!=FontStyles.Normal;
        var brush=from.Foreground as SolidColorBrush;
        if(brush!=null)to.Color=M.Color.FromRgb(brush.Color.R,brush.Color.G,brush.Color.B);
        var inline=from as Inline;if(inline!=null && inline.TextDecorations.Any(d=>d.Location==TextDecorationLocation.Underline))to.Underline=M.Underline.Single;
    }
    static string Picture(Image image)
    {
        var bitmap=image.Source as BitmapSource;if(bitmap==null)throw new FormatException("Ein Bild kann nicht als PDF exportiert werden.");
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=new MemoryStream()){encoder.Save(stream);return "base64:"+Convert.ToBase64String(stream.ToArray());}
    }
    static void Inlines(InlineCollection inlines,M.Paragraph paragraph,double maxWidth,double maxHeight)
    {
        foreach(Inline inline in inlines)
        {
            var run=inline as Run;var span=inline as Span;var ui=inline as InlineUIContainer;
            if(run!=null){var text=paragraph.AddFormattedText(run.Text);Font(run,text.Font);}
            else if(inline is LineBreak)paragraph.AddLineBreak();
            else if(span!=null)Inlines(span.Inlines,paragraph,maxWidth,maxHeight);
            else if(ui!=null)
            {
                var image=ui.Child as Image;if(image==null)throw new FormatException("Dieser eingebettete Inhalt wird beim PDF-Export nicht unterstützt.");
                var bitmap=image.Source as BitmapSource;double w=double.IsNaN(image.Width)?bitmap.Width:image.Width,h=double.IsNaN(image.Height)?w*bitmap.PixelHeight/bitmap.PixelWidth:image.Height;
                double scale=Math.Min(1,Math.Min(maxWidth/w,maxHeight/h));var picture=paragraph.AddImage(Picture(image));picture.Width=Pt(w*scale);picture.Height=Pt(h*scale);picture.LockAspectRatio=true;
            }
        }
    }
    static void Blocks(BlockCollection blocks,M.DocumentElements target,double width,double height,string prefix="")
    {
        foreach(Block block in blocks)
        {
            var p=block as Paragraph;var section=block as Section;var list=block as System.Windows.Documents.List;var table=block as Table;var embedded=block as BlockUIContainer;
            if(p!=null)
            {
                var result=target.AddParagraph();Font(p,result.Format.Font);
                // WPF enum ordering differs: Left=0, Right=1, Center=2, Justify=3.
                result.Format.Alignment=p.TextAlignment==TextAlignment.Center?M.ParagraphAlignment.Center:p.TextAlignment==TextAlignment.Right?M.ParagraphAlignment.Right:p.TextAlignment==TextAlignment.Justify?M.ParagraphAlignment.Justify:M.ParagraphAlignment.Left;
                result.Format.SpaceBefore=Pt(p.Margin.Top);result.Format.SpaceAfter=Pt(p.Margin.Bottom);result.Format.LeftIndent=Pt(p.Margin.Left);result.Format.RightIndent=Pt(p.Margin.Right);result.Format.FirstLineIndent=Pt(p.TextIndent);result.Format.KeepTogether=p.KeepTogether;result.Format.KeepWithNext=p.KeepWithNext;result.Format.PageBreakBefore=p.BreakPageBefore;
                if(prefix!="")result.AddText(prefix);Inlines(p.Inlines,result,width,height);
            }
            else if(section!=null)Blocks(section.Blocks,target,width,height,prefix);
            else if(list!=null){int n=list.StartIndex;foreach(ListItem item in list.ListItems){Blocks(item.Blocks,target,width-18,height,list.MarkerStyle==TextMarkerStyle.Decimal?(n++)+". ":"• ");}}
            else if(table!=null)
            {
                int columns=table.RowGroups.SelectMany(g=>g.Rows.Cast<TableRow>()).Select(r=>r.Cells.Sum(c=>c.ColumnSpan)).DefaultIfEmpty(1).Max();
                var output=target.AddTable();for(int i=0;i<columns;i++)output.AddColumn(Pt(width/columns));output.Borders.Width=.5;output.Borders.Color=M.Colors.Gray;
                foreach(var row in table.RowGroups.SelectMany(g=>g.Rows.Cast<TableRow>())){var added=output.AddRow();int c=0;foreach(TableCell cell in row.Cells){if(cell.RowSpan>1)throw new FormatException("Tabellen mit verbundenen Zeilen bitte vor dem PDF-Export vereinfachen.");added.Cells[c].MergeRight=cell.ColumnSpan-1;Blocks(cell.Blocks,added.Cells[c].Elements,width*cell.ColumnSpan/columns-8,height);c+=cell.ColumnSpan;}}
            }
            else if(embedded!=null)
            {var image=embedded.Child as Image;if(image==null)throw new FormatException("Unbekannter eingebetteter Inhalt im PDF.");var holder=new Paragraph(new InlineUIContainer(new Image {Source=image.Source,Width=image.Width,Height=image.Height}));var one=new Section(holder);Blocks(one.Blocks,target,width,height);}
            else throw new FormatException("Ein Dokumentelement wird beim PDF-Export nicht unterstützt.");
        }
    }
    internal static M.Document Model(DocumentEditor editor,string title)
    {
        // A light clone keeps export colours independent of the current dark document theme.
        var clone=new DocumentEditor();clone.LoadRtf(editor.SaveRtf());var margins=editor.PageMargins;
        var doc=new M.Document();doc.Info.Title=title??"Dokument";
        var section=doc.AddSection();section.PageSetup.PageFormat=M.PageFormat.A4;section.PageSetup.LeftMargin=M.Unit.FromMillimeter(margins.Left);section.PageSetup.RightMargin=M.Unit.FromMillimeter(margins.Right);section.PageSetup.TopMargin=M.Unit.FromMillimeter(margins.Top);section.PageSetup.BottomMargin=M.Unit.FromMillimeter(margins.Bottom);
        Blocks(clone.Document.Blocks,section.Elements,DocumentMargins.Pixels(210-margins.Left-margins.Right),DocumentMargins.Pixels(297-margins.Top-margins.Bottom)-40);return doc;
    }
    public static int Write(DocumentEditor editor,string path,string title="Dokument")
    {
        var renderer=new MigraDoc.Rendering.PdfDocumentRenderer(true) {Document=Model(editor,title)};renderer.RenderDocument();renderer.PdfDocument.Info.Creator="Schreib";
        int pages=renderer.PdfDocument.PageCount;Ui.AtomicWrite(path,delegate(string temporary){renderer.PdfDocument.Save(temporary);});renderer.PdfDocument.Close();return pages;
    }
}
