using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

internal sealed class DocumentEditor : RichTextBox
{
    public event Action<string> CitationClicked;
    public bool PresentationChanging {get;private set;}
    bool darkPaper;
    readonly SolidColorBrush pageInk=new SolidColorBrush(Colors.Black);
    public DocumentEditor()
    {
        AcceptsReturn=true;AcceptsTab=true;IsDocumentEnabled=true;
        Document=new FlowDocument {FontFamily=new FontFamily("Calibri"),FontSize=16,PagePadding=new Thickness(64),ColumnWidth=10000};
        Document.Blocks.Add(new Paragraph());
        Document.Foreground=pageInk;Foreground=pageInk;CaretBrush=pageInk;
        PreviewMouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e)
        {
            var pointer=GetPositionFromPoint(e.GetPosition(this),true);if(pointer==null)return;
            DependencyObject parent=pointer.Parent;
            while(parent!=null){var link=parent as Hyperlink;if(link!=null && link.NavigateUri!=null && link.NavigateUri.Scheme=="schreibref"){if(CitationClicked!=null)CitationClicked(Uri.UnescapeDataString(link.NavigateUri.AbsolutePath));e.Handled=true;return;}var element=parent as FrameworkContentElement;parent=element==null?null:element.Parent;}
        };
    }
    public string Text
    {
        get{string text=new TextRange(Document.ContentStart,Document.ContentEnd).Text;return text.EndsWith("\r\n")?text.Substring(0,text.Length-2):text;}
        set{Document.Blocks.Clear();foreach(string line in (value??"").Replace("\r\n","\n").Split('\n'))Document.Blocks.Add(new Paragraph(new Run(line)));ApplyAppearance(darkPaper);}
    }
    TextPointer Pointer(int offset)
    {
        int limit=new TextRange(Document.ContentStart,Document.ContentEnd).Text.Length;offset=Math.Max(0,Math.Min(offset,limit));
        TextPointer start=Document.ContentStart;int low=0,high=start.GetOffsetToPosition(Document.ContentEnd);
        while(low<high){int mid=low+(high-low)/2;var pointer=start.GetPositionAtOffset(mid,LogicalDirection.Forward);int length=new TextRange(start,pointer).Text.Length;if(length<offset)low=mid+1;else high=mid;}
        var result=start.GetPositionAtOffset(low,LogicalDirection.Forward);return result.GetInsertionPosition(LogicalDirection.Forward)??Document.ContentEnd;
    }
    public int SelectionStart{get{return new TextRange(Document.ContentStart,Selection.Start).Text.Length;}}
    public int SelectionLength{get{return Selection.Text.Length;}}
    public string SelectedText{get{return Selection.Text;}set{Selection.Text=value;}}
    public string ProofText
    {
        get{var text=Text.ToCharArray();foreach(var mark in Citations())for(int i=mark.Start;i<Math.Min(text.Length,mark.Start+mark.Length);i++)if(text[i]!='\r' && text[i]!='\n')text[i]=' ';return new string(text);}
    }
    public void Select(int start,int length){Selection.Select(Pointer(start),Pointer(start+length));}
    public void Clear(){Text="";}
    public void ClearUndo(){bool enabled=IsUndoEnabled;IsUndoEnabled=false;IsUndoEnabled=enabled;}
    public int GetLineIndexFromCharacterIndex(int index){return index;}
    public void ScrollToLine(int index){var rect=Pointer(index).GetCharacterRect(LogicalDirection.Forward);if(!rect.IsEmpty)ScrollToVerticalOffset(VerticalOffset+rect.Top-40);}
    public void InsertCitation(Reference reference,string label)
    {
        Selection.Text="";var position=Selection.Start.GetInsertionPosition(LogicalDirection.Forward);var link=new Hyperlink(new Run(label),position){NavigateUri=new Uri("schreibref:"+reference.Id),Foreground=Brushes.Teal,Background=new SolidColorBrush(Color.FromRgb(225,242,243)),TextDecorations=System.Windows.TextDecorations.Underline};
        CaretPosition=link.ElementEnd.GetInsertionPosition(LogicalDirection.Forward);Focus();
    }
    IEnumerable<Hyperlink> Links(DependencyObject root)
    {
        foreach(object child in LogicalTreeHelper.GetChildren(root)){var link=child as Hyperlink;if(link!=null && link.NavigateUri!=null && link.NavigateUri.Scheme=="schreibref")yield return link;var dependency=child as DependencyObject;if(dependency!=null)foreach(var nested in Links(dependency))yield return nested;}
    }
    public List<CitationMark> Citations()
    {
        return Links(Document).Select(link=>new CitationMark {Start=new TextRange(Document.ContentStart,link.ContentStart).Text.Length,Length=new TextRange(link.ContentStart,link.ContentEnd).Text.Length,ReferenceId=Uri.UnescapeDataString(link.NavigateUri.AbsolutePath)}).ToList();
    }
    public void ApplyAppearance(bool dark)
    {
        PresentationChanging=true;try{darkPaper=dark;Background=dark?new SolidColorBrush(Color.FromRgb(30,34,41)):Brushes.White;pageInk.Color=dark?Colors.WhiteSmoke:Colors.Black;}finally{PresentationChanging=false;}
    }
    public string SaveRtf()
    {
        PresentationChanging=true;var foreground=pageInk.Color;
        try{pageInk.Color=Colors.Black;using(var stream=new MemoryStream()){new TextRange(Document.ContentStart,Document.ContentEnd).Save(stream,DataFormats.Rtf);return Convert.ToBase64String(stream.ToArray());}}
        finally{pageInk.Color=foreground;PresentationChanging=false;}
    }
    public void LoadRtf(string value)
    {
        using(var stream=new MemoryStream(Convert.FromBase64String(value)))new TextRange(Document.ContentStart,Document.ContentEnd).Load(stream,DataFormats.Rtf);
        Normalize(Document);Document.Foreground=pageInk;Document.Background=Brushes.Transparent;ApplyAppearance(darkPaper);ClearUndo();
    }
    static void Normalize(DependencyObject root)
    {
        foreach(object item in LogicalTreeHelper.GetChildren(root)){var element=item as TextElement;if(element!=null){var foreground=element.ReadLocalValue(TextElement.ForegroundProperty) as SolidColorBrush;if(foreground!=null && foreground.Color==Colors.Black)element.ClearValue(TextElement.ForegroundProperty);var background=element.ReadLocalValue(TextElement.BackgroundProperty) as SolidColorBrush;if(background!=null && background.Color==Colors.White)element.ClearValue(TextElement.BackgroundProperty);}var child=item as DependencyObject;if(child!=null)Normalize(child);}
    }
    public void RestoreCitations(IEnumerable<CitationMark> marks,IEnumerable<Reference> references)
    {
        foreach(var mark in marks.OrderByDescending(x=>x.Start)){var reference=references.FirstOrDefault(x=>x.Id==mark.ReferenceId);if(reference==null || mark.Start<0 || mark.Length<1 || mark.Start+mark.Length>Text.Length)continue;Select(mark.Start,mark.Length);string label=SelectedText;InsertCitation(reference,label);}
        Select(0,0);
    }
}
