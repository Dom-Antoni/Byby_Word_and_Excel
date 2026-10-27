using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

internal sealed class ReferencePanel : DockPanel
{
    readonly DocumentEditor editor;
    readonly TextBox search=new TextBox {Padding=new Thickness(8),Margin=new Thickness(0,8,0,8)};
    readonly ListBox library=new ListBox {MaxHeight=220};
    readonly ListBox bibliography=new ListBox();
    readonly TextBlock description=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,8)};
    readonly TextBlock count=new TextBlock {TextWrapping=TextWrapping.Wrap};
    public List<Reference> References=new List<Reference>();
    public List<string> BibliographyOnly=new List<string>();
    public event Action Changed;
    public event Action Reveal;
    public int? ReturnPosition;
    public bool Numbered;
    readonly ComboBox style;
    readonly ReferenceStore store;
    readonly System.Windows.Threading.DispatcherTimer refreshTimer=new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background) {Interval=TimeSpan.FromMilliseconds(650)};
    public ReferencePanel(DocumentEditor editor,ReferenceStore sourceStore=null)
    {
        this.editor=editor;
        store=sourceStore??new ReferenceStore();
        try{References=store.Load();}catch(Exception ex){MessageBox.Show(ex.Message,"Quellenbibliothek");}
        var top=new StackPanel();SetDock(top,Dock.Top);Children.Add(top);
        var buttons=new WrapPanel();top.Children.Add(buttons);
        Ui.Button(buttons,"Referenz hinzufügen…",AddReference);Ui.Button(buttons,"Importieren…",Import);Ui.Button(buttons,"RIS exportieren…",ExportRis);Ui.Button(buttons,"EndNote öffnen",OpenEndNote);
        var libraryHint=new TextBlock {Text="Deine lokale Quellenbibliothek · Link / DOI oder vorhandene Quelle",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};Theme.Bind(libraryHint,TextBlock.ForegroundProperty,"App.Muted");top.Children.Add(libraryHint);
        top.Children.Add(search);search.ToolTip="Autor, Titel, Jahr oder DOI suchen";search.TextChanged+=delegate{RefreshLibrary();};
        top.Children.Add(library);library.SelectionChanged+=delegate{var item=Selected;if(item!=null)description.Text=item.Bibliography;};
        var actions=new WrapPanel {Margin=new Thickness(0,8,0,8)};top.Children.Add(actions);Ui.Button(actions,"Zitieren",Insert);Ui.Button(actions,"Alle hinzufügen",AddAll);
        Ui.Button(top,"EndNote-Platzhalter verbinden",ConnectTemporary);
        top.Children.Add(description);
        style=new ComboBox {ItemsSource=new[]{"Autor–Jahr (einfach)","Nummeriert (einfach)"},SelectedIndex=0,Margin=new Thickness(0,4,0,8)};top.Children.Add(style);
        style.SelectionChanged+=delegate{Numbered=style.SelectedIndex==1;UpdateCitationLabels();RefreshBibliography();Notify();};
        top.Children.Add(new TextBlock {Text="Literaturverzeichnis",FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,6)});top.Children.Add(count);
        bibliography.HorizontalContentAlignment=HorizontalAlignment.Stretch;ScrollViewer.SetHorizontalScrollBarVisibility(bibliography,ScrollBarVisibility.Disabled);Children.Add(bibliography);
        refreshTimer.Tick+=delegate{refreshTimer.Stop();RefreshBibliography();};Unloaded+=delegate{refreshTimer.Stop();};
        editor.CitationClicked+=Jump;editor.TextChanged+=delegate{if(editor.PresentationChanging)return;ReturnPosition=null;refreshTimer.Stop();refreshTimer.Start();};
        editor.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Z && Keyboard.Modifiers==ModifierKeys.Control && ReturnPosition.HasValue){Return();e.Handled=true;}};
        PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Z && Keyboard.Modifiers==ModifierKeys.Control && ReturnPosition.HasValue){Return();e.Handled=true;}};
        RefreshLibrary();
        var menu=new ContextMenu();menu.Items.Add(new MenuItem {Header="Ausschneiden",Command=ApplicationCommands.Cut,CommandTarget=editor});menu.Items.Add(new MenuItem {Header="Kopieren",Command=ApplicationCommands.Copy,CommandTarget=editor});menu.Items.Add(new MenuItem {Header="Einfügen",Command=ApplicationCommands.Paste,CommandTarget=editor});menu.Items.Add(new Separator());var add=new MenuItem {Header="Referenz hinzufügen…"};add.Click+=delegate{AddReference();};menu.Items.Add(add);editor.ContextMenu=menu;
        editor.PreviewMouseRightButtonDown+=delegate(object sender,MouseButtonEventArgs e){var at=editor.GetPositionFromPoint(e.GetPosition(editor),true);if(at!=null && (editor.Selection.IsEmpty || at.CompareTo(editor.Selection.Start)<0 || at.CompareTo(editor.Selection.End)>0))editor.Selection.Select(at,at);};
    }
    Reference Selected{get{var item=library.SelectedItem as ListBoxItem;return item==null?null:item.Tag as Reference;}}
    public void SetStyle(bool numbered){Numbered=numbered;style.SelectedIndex=numbered?1:0;}
    void UpdateCitationLabels()
    {
        editor.BeginChange();try{foreach(var mark in editor.Citations().OrderByDescending(x=>x.Start)){var reference=References.FirstOrDefault(x=>x.Id==mark.ReferenceId);if(reference==null)continue;string label=Numbered?"["+(References.IndexOf(reference)+1)+"]":"("+reference.Cite+")";if(editor.Text.Substring(mark.Start,mark.Length)==label)continue;editor.Select(mark.Start,mark.Length);editor.InsertCitation(reference,label);}}finally{editor.EndChange();}
    }
    void Notify(){if(Changed!=null)Changed();}
    public void AddReference()
    {
        foreach(var reference in store.Load())if(!References.Any(x=>ReferenceStore.Same(x,reference)))References.Add(reference);
        int position=editor.SelectionStart+editor.SelectionLength;var dialog=new ReferencePickerWindow(store,References){Owner=Window.GetWindow(this)};if(dialog.ShowDialog()!=true)return;
        UpsertDocument(dialog.Result);RefreshLibrary();
        if(dialog.Insert){editor.Select(position,0);Insert(dialog.Result);}else description.Text="Quelle in der lokalen Bibliothek gespeichert. Zum Zitieren auswählen und „Zitieren“ verwenden.";
        Notify();
    }
    void UpsertDocument(Reference reference){int index=References.FindIndex(x=>x.Id==reference.Id);if(index<0)References.Add(reference);else References[index]=reference;}
    public void MergeDocument(IEnumerable<Reference> sources)
    {
        foreach(var source in sources)
        {
            // Document IDs remain intact even when another computer's library uses a different ID.
            var clone=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Reference>(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(source));
            var saved=store.Upsert(clone);if(saved.Id!=source.Id)References.RemoveAll(x=>x.Id==saved.Id);UpsertDocument(source);
        }
        RefreshLibrary();
    }
    public void ResetDocument(){BibliographyOnly.Clear();References=store.Load();RefreshLibrary();}
    public List<Reference> DocumentReferences(){return Used();}
    void ExportRis()
    {
        var dialog=new SaveFileDialog {Filter="RIS für EndNote / Zotero / Mendeley (*.ris)|*.ris",FileName="Quellen.ris"};if(dialog.ShowDialog()!=true)return;
        string ris=ReferenceStore.Ris(References);Ui.AtomicWrite(dialog.FileName,delegate(string temp){File.WriteAllText(temp,ris,new UTF8Encoding(true));});description.Text="RIS gespeichert. In EndNote über File → Import → Reference Manager (RIS) öffnen; bei eingerichteter Dateizuordnung genügt ein Doppelklick auf die RIS-Datei.";
    }
    public void RefreshLibrary()
    {
        library.Items.Clear();string query=search.Text;
        foreach(var reference in References.Where(x=>x.Bibliography.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0))library.Items.Add(new ListBoxItem {Content=new TextBlock {Text=reference.ToString(),TextWrapping=TextWrapping.Wrap,MaxWidth=320},Tag=reference});
        RefreshBibliography();
    }
    public List<Reference> Used()
    {
        var ids=editor.Citations().Select(x=>x.ReferenceId).Concat(BibliographyOnly).Distinct().ToList();
        var result=ids.Select(id=>References.FirstOrDefault(x=>x.Id==id)).Where(x=>x!=null).ToList();return Numbered?result.OrderBy(x=>References.IndexOf(x)).ToList():result.OrderBy(x=>x.Author).ThenBy(x=>x.Year).ToList();
    }
    public void RefreshBibliography()
    {
        bibliography.Items.Clear();var used=Used();count.Text=References.Count+" Quellen importiert · "+used.Count+" im Literaturverzeichnis";
        for(int i=0;i<used.Count;i++)bibliography.Items.Add(new ListBoxItem {Content=new TextBlock {Text=(Numbered?"["+(References.IndexOf(used[i])+1)+"] ":"")+used[i].Bibliography,TextWrapping=TextWrapping.Wrap,MaxWidth=320,Margin=new Thickness(4,8,4,8)},Tag=used[i]});
    }
    void Import()
    {
        var dialog=new OpenFileDialog {Filter="Referenzen (*.xml;*.ris;*.json;*.txt)|*.xml;*.ris;*.json;*.txt"};if(dialog.ShowDialog()!=true)return;
        var imported=ReferenceImport.Read(dialog.FileName);foreach(var reference in imported){var saved=store.Upsert(reference);UpsertDocument(saved);}
        RefreshLibrary();Notify();description.Text=imported.Count+" Quellen eingelesen. Erneuter Import aktualisiert dieselben Quellen; die Originalbibliothek bleibt unverändert.";
    }
    void Insert()
    {
        var reference=Selected;if(reference==null)return;Insert(reference);
    }
    void Insert(Reference reference)
    {
        if(editor.IsReadOnly){MessageBox.Show("Die Quelle ist verfügbar. Zum Einfügen bitte den Lesemodus unter Ansicht ausschalten.","Lesemodus");return;}
        int number=References.IndexOf(reference)+1;
        int position=editor.SelectionStart+editor.SelectionLength;editor.Select(position,0);
        if(position>0 && !char.IsWhiteSpace(editor.Text[position-1]) && editor.Text[position-1]!='(' && editor.Text[position-1]!='['){editor.SelectedText=" ";editor.Select(position+1,0);}
        editor.InsertCitation(reference,Numbered?"["+number+"]":"("+reference.Cite+")");RefreshBibliography();Notify();
    }
    void AddAll(){foreach(var reference in References)if(!BibliographyOnly.Contains(reference.Id))BibliographyOnly.Add(reference.Id);RefreshBibliography();Notify();}
    public void Jump(string id)
    {
        var source=References.FirstOrDefault(x=>x.Id==id);if(source==null)return;ReturnPosition=editor.SelectionStart;
        if(Reveal!=null)Reveal();RefreshBibliography();
        foreach(ListBoxItem item in bibliography.Items)if(((Reference)item.Tag).Id==id){bibliography.SelectedItem=item;bibliography.ScrollIntoView(item);item.BringIntoView();item.Focus();break;}
        description.Text=source.Bibliography+"\nStrg+Z: zurück zur Textstelle";
    }
    public void Return(){if(!ReturnPosition.HasValue)return;int position=ReturnPosition.Value;ReturnPosition=null;editor.Focus();editor.Select(position,0);editor.ScrollToLine(position);}
    void ConnectTemporary()
    {
        if(editor.IsReadOnly)throw new InvalidOperationException("Zum Verknüpfen bitte den Lesemodus unter Ansicht ausschalten.");
        var matches=Regex.Matches(editor.Text,@"\{[^{}]*#(?<record>\d+)[^{}]*\}").Cast<Match>().Reverse().ToArray();int linked=0,unresolved=0;
        foreach(var match in matches){var candidates=References.Where(x=>x.RecordNumber==match.Groups["record"].Value).ToArray();if(candidates.Length!=1 || match.Value.Contains(";")){unresolved++;continue;}editor.Select(match.Index,match.Length);editor.InsertCitation(candidates[0],"("+candidates[0].Cite+")");linked++;}
        description.Text=linked+" EndNote-Platzhalter verbunden. "+unresolved+" mehrdeutige oder mehrteilige Platzhalter unverändert belassen.";RefreshBibliography();Notify();
    }
    static void OpenEndNote()
    {
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"EndNote 2025","EndNote.exe");
        if(!File.Exists(path)){var dialog=new OpenFileDialog {Filter="EndNote-Programm (*.exe)|*.exe",Title="Installiertes EndNote auswählen"};if(dialog.ShowDialog()!=true)return;path=dialog.FileName;}
        Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
    }
    public string ExportRtf(bool endnote)
    {
        var clone=new DocumentEditor();clone.LoadRtf(editor.SaveRtf());
        if(endnote)
        {
            foreach(var citation in editor.Citations().OrderByDescending(x=>x.Start))
            {
                var reference=References.FirstOrDefault(x=>x.Id==citation.ReferenceId);if(reference==null || !reference.EndNoteRecord || !Regex.IsMatch(reference.RecordNumber??"",@"^\d+$"))throw new InvalidOperationException("EndNote-RTF braucht EndNote-Datensatznummern. Bitte aus der passenden EndNote-Bibliothek XML importieren.");
                clone.Select(citation.Start,citation.Length);clone.SelectedText="{"+reference.Author+", "+reference.Year+" #"+reference.RecordNumber+"}";
            }
        }
        else
        {
            clone.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Literaturverzeichnis")){FontWeight=FontWeights.Bold});
            var used=Used();for(int i=0;i<used.Count;i++)clone.Document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run((Numbered?"["+(References.IndexOf(used[i])+1)+"] ":"")+used[i].Bibliography)));
        }
        return clone.SaveRtf();
    }
}
