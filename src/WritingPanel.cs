using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

internal sealed class WritingPanel : DockPanel,IDisposable
{
    readonly DocumentEditor editor;
    readonly TextBlock state=new TextBlock(),detail=new TextBlock(),title=new TextBlock();
    readonly ListBox hints=new ListBox(),replacements=new ListBox();
    readonly ComboBox category=new ComboBox();
    readonly GrammarClient grammar=new GrammarClient(AppDomain.CurrentDomain.BaseDirectory);
    readonly ProofingSession review=new ProofingSession();
    public event Action ReviewChanged;
    public List<DismissedHint> DismissedHints {get{return review.Snapshot();}}
    readonly Task<Synonyms> synonyms=Task.Run(()=>new Synonyms(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"synonyms")));
    WritingSettings settings;
    List<WritingIssue> issues=new List<WritingIssue>();
    CancellationTokenSource checking;
    string analyzedText="";bool closed;int generation;
    public int LanguageIndex {get{return Array.IndexOf(new[]{"de-DE","en-GB","en-US"},settings.Language);}}
    public WritingPanel(DocumentEditor editor,WritingSettings initialSettings=null)
    {
        this.editor=editor;
        try {settings=initialSettings??WritingSettings.Load();}
        catch(Exception ex){settings=new WritingSettings();MessageBox.Show("Einstellungen konnten nicht geladen werden. Die Datei bleibt erhalten.\n"+ex.Message,"Schreib");}
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);Children.Add(top);
        title.FontSize=18;title.FontWeight=FontWeights.SemiBold;top.Children.Add(title);
        state.TextWrapping=TextWrapping.Wrap;Theme.Bind(state,TextBlock.ForegroundProperty,"App.Muted");state.FontSize=12;state.Margin=new Thickness(0,8,0,8);top.Children.Add(state);
        category.ItemsSource=new[]{"Alle Hinweise","Rechtschreibung","Grammatik","Wiederholung","Stil","Lesbarkeit","Einheitlichkeit"};category.SelectedIndex=0;category.Margin=new Thickness(0,0,0,12);top.Children.Add(category);category.SelectionChanged+=delegate{Render();};
        var legend=new TextBlock {Text="Im Text: Rot = Rechtschreibung · Blau = Grammatik · Gelb = Stil und weitere Hinweise",TextWrapping=TextWrapping.Wrap,FontSize=12,Margin=new Thickness(0,0,0,8)};Theme.Bind(legend,TextBlock.ForegroundProperty,"App.Muted");top.Children.Add(legend);
        var bottom=new StackPanel {Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(bottom,Dock.Bottom);Children.Add(bottom);
        detail.TextWrapping=TextWrapping.Wrap;detail.MaxHeight=110;bottom.Children.Add(detail);
        replacements.MaxHeight=100;replacements.Margin=new Thickness(0,8,0,8);bottom.Children.Add(replacements);
        var actions=new WrapPanel();bottom.Children.Add(actions);Ui.Button(actions,"Übernehmen",Apply);Ui.Button(actions,"Ignorieren",Ignore);
        var more=new WrapPanel {Margin=new Thickness(0,8,0,0)};bottom.Children.Add(more);Ui.Button(more,"Regel aus",Disable);Ui.Button(more,"Eigenes Wort",AddWord);
        hints.BorderThickness=new Thickness(0);hints.Background=Ui.Paper;hints.HorizontalContentAlignment=HorizontalAlignment.Stretch;ScrollViewer.SetHorizontalScrollBarVisibility(hints,ScrollBarVisibility.Disabled);Children.Add(hints);hints.SelectionChanged+=delegate{ShowSelected();};
        review.Reset(editor.Text);state.Text="Bereit · Prüfung vollständig auf diesem Computer";Render();
    }
    void Persist(){try{settings.Save();}catch(Exception ex){MessageBox.Show("Einstellungen konnten nicht gespeichert werden: "+ex.Message,"Schreib");}}
    public void SetLanguage(int index){settings.Language=new[]{"de-DE","en-GB","en-US"}[index];Persist();Invalidate();Check();}
    public void ResetDocument(IEnumerable<DismissedHint> saved=null){review.Reset(editor.Text,saved);Invalidate();}
    public void Invalidate(){generation++;if(checking!=null)checking.Cancel();review.Update(editor.Text);issues.Clear();Render();state.Text="Änderungen noch nicht geprüft …";}
    public async void Check()
    {
        if(closed)return;if(checking!=null)checking.Cancel();checking=new CancellationTokenSource();var cancellation=checking;
        int version=++generation;string text=editor.Text;string proof=editor.ProofText;var snapshot=settings.Copy();analyzedText=text;review.Update(text);
        issues=Writing.Analyze(proof,snapshot);foreach(var issue in issues)issue.Original=text.Substring(issue.Start,issue.Length);Render();
        if(string.IsNullOrWhiteSpace(text)){state.Text="Bereit · Prüfung vollständig auf diesem Computer";cancellation.Dispose();checking=null;return;}
        state.Text="Grammatik wird lokal geprüft … Der erste Start kann einige Sekunden dauern.";
        try
        {
            var result=await grammar.Check(proof,snapshot,cancellation.Token);if(closed || version!=generation || text!=editor.Text)return;
            issues=issues.Concat(result.Issues.Where(x=>x.Fits(text))).GroupBy(x=>x.Key).Select(x=>x.First()).OrderBy(x=>x.Start).ToList();Render();state.Text=result.Status;
        }
        catch(OperationCanceledException){if(!closed && version==generation)state.Text="Zeitlimit erreicht oder Prüfung abgebrochen. Erneut prüfen.";}
        catch(Exception ex){if(!closed && version==generation)state.Text="Nur Stilregeln geprüft · Grammatik nicht verfügbar: "+ex.Message;}
        finally{cancellation.Dispose();if(checking==cancellation)checking=null;}
    }
    void Render()
    {
        string filter=category.SelectedIndex<=0?null:Convert.ToString(category.SelectedItem);
        var active=issues.Where(x=>!review.IsIgnored(x.Rule,x.Start,x.Length,x.Original,settings.Language) && settings.Enabled(x.Rule)).GroupBy(x=>x.Key).Select(x=>x.First()).ToList();
        int textLength=editor.Text.Length;
        editor.SetProofMarks(active.Where(x=>x.Rule!="synonym" && textLength>0).Select(x=>new ProofMark {Start=Math.Min(x.Start,textLength-1),Length=Math.Max(1,x.Length),Kind=x.Category=="Rechtschreibung"?"spelling":x.Category=="Grammatik"?"grammar":"style"}));
        var visible=active.Where(x=>filter==null || x.Category.StartsWith(filter)).ToList();
        hints.Items.Clear();replacements.Items.Clear();detail.Text="Hinweis auswählen, Erklärung lesen und Vorschlag übernehmen oder bewusst ignorieren.";title.Text=visible.Count+" Schreibhinweise";
        foreach(var issue in visible)
        {
            var label=new TextBlock {Text=issue.Category+"\n"+issue.Message,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8),MaxWidth=325};
            Theme.Bind(label,TextBlock.ForegroundProperty,issue.Category=="Rechtschreibung"?"App.Error":issue.Category.StartsWith("Grammatik")?"App.Accent":"App.Text");
            hints.Items.Add(new ListBoxItem {Content=label,Tag=issue,HorizontalContentAlignment=HorizontalAlignment.Stretch});
        }
    }
    WritingIssue Selected {get {var item=hints.SelectedItem as ListBoxItem;return item==null?null:item.Tag as WritingIssue;}}
    void ShowSelected()
    {
        var issue=Selected;replacements.Items.Clear();if(issue==null)return;detail.Text=issue.Message+"\nRegel: "+issue.Rule;
        foreach(string word in issue.Replacements)replacements.Items.Add(new ListBoxItem {Content=new TextBlock {Text=word==""?"[Entfernen]":word,TextWrapping=TextWrapping.Wrap,MaxWidth=320},Tag=issue.Rule=="synonym"?Synonyms.Replacement(word):word});if(replacements.Items.Count>0)replacements.SelectedIndex=0;
        if(analyzedText==editor.Text && issue.Fits(editor.Text)){editor.Focus();editor.Select(issue.Start,issue.Length);editor.ScrollToLine(editor.GetLineIndexFromCharacterIndex(issue.Start));}
    }
    void Apply()
    {
        if(editor.IsReadOnly){MessageBox.Show("Bitte den Lesemodus unter Ansicht ausschalten, um Änderungen zu übernehmen.","Lesemodus");return;}
        var issue=Selected;var replacement=replacements.SelectedItem as ListBoxItem;if(issue==null || replacement==null)return;
        if(analyzedText!=editor.Text || !issue.Fits(editor.Text)){Check();return;}
        editor.Select(issue.Start,issue.Length);editor.SelectedText=(string)replacement.Tag;editor.Focus();
    }
    void Ignore(){var issue=Selected;if(issue==null || analyzedText!=editor.Text || !issue.Fits(editor.Text))return;review.Ignore(new DismissedHint {Rule=issue.Rule,Start=issue.Start,Length=issue.Length,Original=issue.Original,Language=settings.Language});Render();if(ReviewChanged!=null)ReviewChanged();}
    void Disable(){var issue=Selected;if(issue==null || issue.Rule=="synonym")return;if(!settings.Disabled.Contains(issue.Rule))settings.Disabled.Add(issue.Rule);Persist();Render();}
    void AddWord()
    {
        string word=Selected==null?editor.SelectedText:Selected.Original;
        if(!Writing.Words.IsMatch(word) || Writing.Words.Match(word).Value!=word){MessageBox.Show("Bitte ein einzelnes Wort markieren oder einen Rechtschreibhinweis auswählen.","Eigenes Wörterbuch");return;}
        if(!settings.PersonalWords.Contains(word,StringComparer.OrdinalIgnoreCase))settings.PersonalWords.Add(word);Persist();Check();
    }
    public async void FindSynonyms()
    {
        int start=editor.SelectionStart,length=editor.SelectionLength;string text=editor.Text;string lang=settings.Language;
        if(length==0){var match=Writing.Words.Matches(text).Cast<System.Text.RegularExpressions.Match>().FirstOrDefault(x=>x.Index<=start && x.Index+x.Length>=start);if(match==null)return;start=match.Index;length=match.Length;}
        string word=text.Substring(start,length);int version=++generation;if(checking!=null)checking.Cancel();state.Text="Synonyme werden lokal gesucht …";
        try
        {
            var dictionary=await synonyms;if(closed || version!=generation || editor.Text!=text)return;var groups=dictionary.Find(word,lang);var found=new List<WritingIssue>();
            foreach(var group in groups){var options=group.Words.Where(x=>!string.Equals(Synonyms.Replacement(x),word,StringComparison.OrdinalIgnoreCase)).Distinct().ToList();if(options.Count>0)found.Add(new WritingIssue {Start=start,Length=length,Original=word,Rule="synonym",Category="Synonyme",Message=group.Explanation,Replacements=options});}
            analyzedText=text;issues=found;category.SelectedIndex=0;Render();state.Text=found.Count==0?"Keine lokalen Synonyme gefunden. Grundform prüfen.":"Synonyme lokal · Bedeutung und Beugung vor dem Übernehmen prüfen";if(hints.Items.Count>0)hints.SelectedIndex=0;
        }
        catch(Exception ex){if(!closed && version==generation)state.Text="Synonymwörterbuch nicht verfügbar: "+ex.Message;}
    }
    public void Settings(){var dialog=new WritingSettingsWindow(settings){Owner=Window.GetWindow(this)};if(dialog.ShowDialog()!=true)return;settings=dialog.Value;Persist();Check();}
    public void Dispose(){closed=true;if(checking!=null)checking.Cancel();grammar.Dispose();}
}
