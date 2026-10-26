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
    bool adjustingSelection;
    ProofAdorner proofAdorner;
    List<ProofMark> proofMarks=new List<ProofMark>();
    readonly SolidColorBrush pageInk=new SolidColorBrush(Colors.Black);
    readonly SolidColorBrush citationInk=new SolidColorBrush(Color.FromRgb(100,118,140));
    public DocumentEditor()
    {
        AcceptsReturn=true;AcceptsTab=true;IsDocumentEnabled=true;
        Document=new FlowDocument {FontFamily=new FontFamily("Calibri"),FontSize=16,PagePadding=new Thickness(64),ColumnWidth=10000};
        Document.Blocks.Add(new Paragraph());
        Document.Foreground=pageInk;Foreground=pageInk;CaretBrush=pageInk;
        SelectionChanged+=delegate{if(!adjustingSelection)NormalizeSelection();};
        Loaded+=delegate{var layer=AdornerLayer.GetAdornerLayer(this);if(layer!=null && proofAdorner==null){proofAdorner=new ProofAdorner(this);layer.Add(proofAdorner);}RefreshProofMarks();};
        Unloaded+=delegate{if(proofAdorner!=null){var layer=VisualTreeHelper.GetParent(proofAdorner) as AdornerLayer;if(layer!=null)layer.Remove(proofAdorner);proofAdorner=null;}};
        AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler(delegate{RefreshProofMarks();}));
        SizeChanged+=delegate{RefreshProofMarks();};TextChanged+=delegate{RefreshProofMarks();};
        CommandManager.AddPreviewExecutedHandler(this,BeforeCommand);
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
    internal TextPointer Pointer(int offset)
    {
        int limit=new TextRange(Document.ContentStart,Document.ContentEnd).Text.Length;offset=Math.Max(0,Math.Min(offset,limit));
        TextPointer start=Document.ContentStart;int low=0,high=start.GetOffsetToPosition(Document.ContentEnd);
        while(low<high){int mid=low+(high-low)/2;var pointer=start.GetPositionAtOffset(mid,LogicalDirection.Forward);int length=new TextRange(start,pointer).Text.Length;if(length<offset)low=mid+1;else high=mid;}
        var result=start.GetPositionAtOffset(low,LogicalDirection.Forward);return result.GetInsertionPosition(LogicalDirection.Forward)??Document.ContentEnd;
    }
    public int SelectionStart{get{return new TextRange(Document.ContentStart,Selection.Start).Text.Length;}}
    public int SelectionLength{get{return Selection.Text.Length;}}
    public string SelectedText{get{return Selection.Text;}set{ReplaceSelection(value);}}
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
        BeginChange();adjustingSelection=true;
        try
        {
            DeleteSelectedCitations();Selection.Text="";var position=Selection.Start.GetInsertionPosition(LogicalDirection.Forward);
            var link=new Hyperlink(new Run(label),position){NavigateUri=new Uri("schreibref:"+reference.Id)};StyleCitation(link);
            CaretPosition=Outside(link,false);Focus();
        }
        finally{adjustingSelection=false;EndChange();}
    }
    static Hyperlink CitationAt(TextPointer pointer)
    {
        for(var parent=pointer.Parent as FrameworkContentElement;parent!=null;parent=parent.Parent as FrameworkContentElement)
        {var link=parent as Hyperlink;if(link!=null && link.NavigateUri!=null && link.NavigateUri.Scheme=="schreibref")return link;}
        return null;
    }
    TextPointer Outside(Hyperlink link,bool before)
    {
        var run=(before?link.PreviousInline:link.NextInline) as Run;
        if(run==null){run=new Run();var span=link.Parent as Span;var paragraph=link.Parent as Paragraph;var inlines=span!=null?span.Inlines:paragraph.Inlines;if(before)inlines.InsertBefore(link,run);else inlines.InsertAfter(link,run);}
        return before?run.ContentEnd:run.ContentStart;
    }
    void NormalizeSelection()
    {
        adjustingSelection=true;
        try
        {
            if(Selection.IsEmpty){var link=CitationAt(CaretPosition);if(link!=null)CaretPosition=Outside(link,false);return;}
            var start=Selection.Start;var end=Selection.End;
            foreach(var link in Links(Document))if(start.CompareTo(link.ContentEnd)<0 && end.CompareTo(link.ContentStart)>0)
            {if(start.CompareTo(link.ElementStart)>0)start=link.ElementStart;if(end.CompareTo(link.ElementEnd)<0)end=link.ElementEnd;}
            if(start.CompareTo(Selection.Start)!=0 || end.CompareTo(Selection.End)!=0)Selection.Select(start,end);
        }
        finally{adjustingSelection=false;}
    }
    void DeleteSelectedCitations()
    {
        if(Selection.IsEmpty)return;
        var links=Links(Document).Where(x=>Selection.Start.CompareTo(x.ContentEnd)<0 && Selection.End.CompareTo(x.ContentStart)>0).ToList();
        foreach(var link in links)new TextRange(link.ElementStart,link.ElementEnd).Text="";
    }
    void ReplaceSelection(string value)
    {
        NormalizeSelection();BeginChange();adjustingSelection=true;
        try{DeleteSelectedCitations();Selection.Text=value??"";}
        finally{adjustingSelection=false;EndChange();}
    }
    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        // Give text after a source its own Run, rather than extending the Hyperlink.
        NormalizeSelection();base.OnPreviewTextInput(e);
    }
    void BeforeCommand(object sender,ExecutedRoutedEventArgs e)
    {
        bool left=e.Command==EditingCommands.MoveLeftByCharacter || e.Command==EditingCommands.MoveLeftByWord;
        bool right=e.Command==EditingCommands.MoveRightByCharacter || e.Command==EditingCommands.MoveRightByWord;
        if((left || right) && Selection.IsEmpty)
        {
            int offset=SelectionStart;var link=Links(Document).FirstOrDefault(x=>new TextRange(Document.ContentStart,left?x.ContentEnd:x.ContentStart).Text.Length==offset);
            if(link!=null){adjustingSelection=true;try{CaretPosition=Outside(link,left);}finally{adjustingSelection=false;}e.Handled=true;return;}
        }
        if(IsReadOnly)return;
        bool backward=e.Command==EditingCommands.Backspace || e.Command==EditingCommands.DeletePreviousWord;
        bool forward=e.Command==EditingCommands.Delete || e.Command==EditingCommands.DeleteNextWord;
        if(!backward && !forward && e.Command!=ApplicationCommands.Cut && e.Command!=ApplicationCommands.Paste)return;
        NormalizeSelection();
        if((backward || forward) && Selection.IsEmpty)
        {
            int offset=SelectionStart;var link=Links(Document).FirstOrDefault(x=>new TextRange(Document.ContentStart,backward?x.ContentEnd:x.ContentStart).Text.Length==offset);
            if(link!=null){Selection.Select(link.ElementStart,link.ElementEnd);ReplaceSelection("");e.Handled=true;}
        }
        if(!e.Handled && Selection.IsEmpty && (e.Command==EditingCommands.DeletePreviousWord || e.Command==EditingCommands.DeleteNextWord))
        {
            (backward?EditingCommands.SelectLeftByWord:EditingCommands.SelectRightByWord).Execute(null,this);ReplaceSelection("");e.Handled=true;
        }
    }
    void StyleCitation(Hyperlink link)
    {
        link.Foreground=pageInk;link.Background=Brushes.Transparent;
        var decoration=new TextDecoration {Location=TextDecorationLocation.Underline,Pen=new Pen(citationInk,1),PenOffset=2,PenOffsetUnit=TextDecorationUnit.Pixel,PenThicknessUnit=TextDecorationUnit.Pixel};
        link.TextDecorations=new TextDecorationCollection {decoration};
        link.ToolTip="Quelle öffnen · Rücktaste / Entf löscht den gesamten Quellenverweis";
    }
    public void SetProofMarks(IEnumerable<ProofMark> marks){proofMarks=(marks??Enumerable.Empty<ProofMark>()).ToList();RefreshProofMarks();}
    void RefreshProofMarks(){if(proofAdorner!=null)proofAdorner.InvalidateVisual();}
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
        PresentationChanging=true;try{darkPaper=dark;Background=dark?new SolidColorBrush(Color.FromRgb(30,34,41)):Brushes.White;pageInk.Color=dark?Colors.WhiteSmoke:Colors.Black;citationInk.Color=dark?Color.FromRgb(127,153,183):Color.FromRgb(100,118,140);RefreshProofMarks();}finally{PresentationChanging=false;}
    }
    public string SaveRtf()
    {
        PresentationChanging=true;var foreground=pageInk.Color;var citationColor=citationInk.Color;
        try{pageInk.Color=Colors.Black;citationInk.Color=Color.FromRgb(100,118,140);using(var stream=new MemoryStream()){new TextRange(Document.ContentStart,Document.ContentEnd).Save(stream,DataFormats.Rtf);return Convert.ToBase64String(stream.ToArray());}}
        finally{pageInk.Color=foreground;citationInk.Color=citationColor;PresentationChanging=false;}
    }
    public void LoadRtf(string value)
    {
        using(var stream=new MemoryStream(Convert.FromBase64String(value)))new TextRange(Document.ContentStart,Document.ContentEnd).Load(stream,DataFormats.Rtf);
        Normalize(Document);Document.Foreground=pageInk;Document.Background=Brushes.Transparent;foreach(var link in Links(Document))StyleCitation(link);ApplyAppearance(darkPaper);ClearUndo();
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
    sealed class ProofAdorner : Adorner
    {
        readonly DocumentEditor editor;
        public ProofAdorner(DocumentEditor editor):base(editor){this.editor=editor;IsHitTestVisible=false;}
        protected override void OnRender(DrawingContext drawing)
        {
            var viewport=new Rect(editor.RenderSize);drawing.PushClip(new RectangleGeometry(viewport));
            int textLength=editor.Text.Length;
            foreach(var mark in editor.proofMarks)
            {
                if(mark.Start<0 || mark.Length<1 || mark.Start>textLength-mark.Length)continue;
                var start=editor.Pointer(mark.Start);var end=editor.Pointer(mark.Start+mark.Length);
                if(!start.HasValidLayout || !end.HasValidLayout)continue;
                bool spelling=mark.Kind=="spelling",grammar=mark.Kind=="grammar";
                var color=spelling?(editor.darkPaper?Color.FromRgb(255,116,116):Color.FromRgb(200,35,44)):grammar?(editor.darkPaper?Color.FromRgb(112,180,255):Color.FromRgb(36,103,201)):(editor.darkPaper?Color.FromRgb(247,203,73):Color.FromRgb(174,123,0));
                var pen=new Pen(new SolidColorBrush(color),1.5);if(!spelling)pen.DashStyle=grammar?DashStyles.Solid:DashStyles.Dot;
                while(start!=null && start.CompareTo(end)<0)
                {
                    var next=start.GetLineStartPosition(1);var stop=next==null || next.CompareTo(end)>0?end:next;
                    var first=start.GetCharacterRect(LogicalDirection.Forward);var last=stop.GetCharacterRect(LogicalDirection.Backward);
                    if(first.Top>viewport.Bottom)break;
                    if(first.Bottom>=0 && !first.IsEmpty && !last.IsEmpty)
                    {
                        double right=Math.Min(viewport.Right,last.Right),left=Math.Max(0,first.Left),y=first.Bottom+(spelling?0:grammar?2:4);
                        if(right>left)
                        {
                            if(spelling){var wave=new StreamGeometry();using(var path=wave.Open()){path.BeginFigure(new Point(left,y),false,false);for(double x=left+2;x<right;x+=2)path.LineTo(new Point(x,y+(((int)((x-left)/2)%2==0)?0:2)),true,false);path.LineTo(new Point(right,y),true,false);}drawing.DrawGeometry(null,pen,wave);}
                            else drawing.DrawLine(pen,new Point(left,y),new Point(right,y));
                        }
                    }
                    if(next==null || next.CompareTo(start)<=0)break;start=next;
                }
            }
            drawing.Pop();
        }
    }
}
