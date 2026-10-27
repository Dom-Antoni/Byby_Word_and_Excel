using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Threading;

internal sealed class WritingPanel : DockPanel,IDisposable
{
    readonly DocumentEditor editor;
    readonly TextBlock state=new TextBlock(),detail=new TextBlock(),title=new TextBlock();
    readonly ListBox hints=new ListBox(),replacements=new ListBox();
    readonly ComboBox category=new ComboBox();
    readonly GrammarClient grammar=new GrammarClient(AppDomain.CurrentDomain.BaseDirectory);
    readonly Func<string,WritingSettings,CancellationToken,Task<GrammarResult>> grammarCheck;
    readonly ProofingSession review=new ProofingSession();
    public event Action ReviewChanged;
    public event Action RevealRequested;
    public List<DismissedHint> DismissedHints {get{review.Update(editor.Text);return review.Snapshot();}}
    readonly Task<Synonyms> synonyms=Task.Run(()=>new Synonyms(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"synonyms")));
    WritingSettings settings;
    List<WritingIssue> issues=new List<WritingIssue>();
    CancellationTokenSource checking;
    string analyzedText="";bool closed;int generation;
    bool busy,recheckRequested,manualQueued,stale;
    DateTime editedAt=DateTime.MinValue;
    string lastCompletedText;
    Task currentCheck=Task.FromResult(0);
    ContextMenu attachedMenu;
    readonly object proofMenuTag=new object();
    int contextOffset;string contextText;
    public Task CurrentCheck {get{return currentCheck;}}
    public bool Automatic {get{return settings.Automatic;}}
    public int PauseMilliseconds {get{return settings.PauseMilliseconds;}}
    public int ChecksStarted {get;private set;}
    public int ChecksCompleted {get;private set;}
    public long LastCheckMilliseconds {get;private set;}
    public string Diagnostics {get{return "Automatik: "+(Automatic?"nach "+PauseMilliseconds+" ms Schreibpause":"aus")+"\nGestartet / abgeschlossen: "+ChecksStarted+" / "+ChecksCompleted+"\nLetzte vollständige Prüfung: "+LastCheckMilliseconds+" ms\nText-Snapshots / Positionsindizes: "+editor.TextSnapshotBuilds+" / "+editor.PointerIndexBuilds+"\nEs läuft höchstens eine Prüfung gleichzeitig. Veraltete Ergebnisse werden verworfen. Messwerte bleiben im Arbeitsspeicher; Text wird nicht protokolliert.";}}
    public int LanguageIndex {get{return Array.IndexOf(new[]{"de-DE","en-GB","en-US"},settings.Language);}}
    public WritingPanel(DocumentEditor editor,WritingSettings initialSettings=null,Func<string,WritingSettings,CancellationToken,Task<GrammarResult>> checker=null)
    {
        this.editor=editor;
        grammarCheck=checker??grammar.Check;
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
    public void Invalidate(){generation++;editedAt=DateTime.UtcNow;review.Update(editor.Text);if(!stale){stale=true;issues.Clear();Render();state.Text=Automatic?"Prüfung nach der nächsten Schreibpause …":"Automatik aus · „Jetzt prüfen“ oder Rechtsklick verwenden.";}}
    public void SetAutomatic(bool enabled){settings.Automatic=enabled;Persist();Invalidate();if(enabled)Check();else state.Text="Automatik aus · „Jetzt prüfen“ oder Rechtsklick verwenden.";}
    public async void Check(){await CheckAsync();}
    public Task CheckAsync(bool automatic=false)
    {
        if(closed || automatic && !Automatic)return Task.FromResult(0);
        if(busy){recheckRequested=true;manualQueued|=!automatic;return currentCheck;}
        string text=editor.Text;if(automatic && text==lastCompletedText && !stale)return Task.FromResult(0);
        var previous=SynchronizationContext.Current;if(previous==null)SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher));
        try{busy=true;currentCheck=RunCheck(++generation,text,editor.ProofText,settings.Copy());return currentCheck;}
        finally{if(previous==null)SynchronizationContext.SetSynchronizationContext(null);}
    }
    async Task RunCheck(int version,string text,string proof,WritingSettings snapshot)
    {
        checking=new CancellationTokenSource();var cancellation=checking;var watch=Stopwatch.StartNew();ChecksStarted++;analyzedText=text;review.Update(text);
        try
        {
            if(string.IsNullOrWhiteSpace(text)){issues.Clear();stale=false;Render();state.Text="Bereit · Prüfung vollständig auf diesem Computer";lastCompletedText=text;return;}
            state.Text="Stil und Grammatik werden im Hintergrund geprüft …";
            var local=await Task.Run(()=>Writing.Analyze(proof,snapshot),cancellation.Token);
            if(closed || version!=generation || text!=editor.Text)return;
            issues=local;foreach(var issue in issues)issue.Original=text.Substring(issue.Start,issue.Length);stale=false;Render();
            var result=await Task.Run(()=>grammarCheck(proof,snapshot,cancellation.Token),cancellation.Token);if(closed || version!=generation || text!=editor.Text)return;
            issues=local.Concat(result.Issues.Where(x=>x.Fits(text))).GroupBy(x=>x.Key).Select(x=>x.First()).OrderBy(x=>x.Start).ToList();Render();LastCheckMilliseconds=watch.ElapsedMilliseconds;ChecksCompleted++;lastCompletedText=text;state.Text=result.Status+" · "+LastCheckMilliseconds+" ms";
        }
        catch(OperationCanceledException){if(!closed && version==generation)state.Text="Zeitlimit erreicht oder Prüfung abgebrochen. Erneut prüfen.";}
        catch(Exception ex){if(!closed && version==generation)state.Text="Nur Stilregeln geprüft · Grammatik nicht verfügbar: "+ex.Message;}
        finally
        {
            cancellation.Dispose();if(checking==cancellation)checking=null;busy=false;
            bool again=recheckRequested,manual=manualQueued;recheckRequested=false;manualQueued=false;
            if(!closed && again && (manual || Automatic && (DateTime.UtcNow-editedAt).TotalMilliseconds>=PauseMilliseconds))QueueCheck(!manual);
        }
    }
    void QueueCheck(bool automatic){Dispatcher.BeginInvoke(new Action(async delegate{await CheckAsync(automatic);}),DispatcherPriority.Background);}
    void Render()
    {
        string filter=category.SelectedIndex<=0?null:Convert.ToString(category.SelectedItem);
        var active=issues.Where(x=>!review.IsIgnored(x.Rule,x.Start,x.Length,x.Original,settings.Language) && settings.Enabled(x.Rule)).GroupBy(x=>x.Key).Select(x=>x.First()).ToList();
        int textLength=editor.Text.Length;
        editor.SetProofMarks(active.Where(x=>x.Rule!="synonym" && textLength>0).Select(x=>new ProofMark {Start=Math.Min(x.Start,textLength-1),Length=Math.Max(1,x.Length),Kind=x.Category=="Rechtschreibung"?"spelling":x.Category=="Grammatik"?"grammar":"style"}));
        var visible=active.Where(x=>filter==null || x.Category.StartsWith(filter)).ToList();
        hints.Items.Clear();replacements.Items.Clear();detail.Text="Hinweis auswählen, Erklärung lesen und Vorschlag übernehmen oder bewusst ignorieren.";title.Text=visible.Count+" Schreibhinweise";
        if(visible.Count>200)title.Text+=" · erste 200 in der Liste";
        foreach(var issue in visible.Take(200))
        {
            var label=new TextBlock {Text=issue.Category+"\n"+issue.Message,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8),MaxWidth=325};
            Theme.Bind(label,TextBlock.ForegroundProperty,issue.Category=="Rechtschreibung"?"App.Error":issue.Category.StartsWith("Grammatik")?"App.Accent":"App.Text");
            hints.Items.Add(new ListBoxItem {Content=label,Tag=issue,HorizontalContentAlignment=HorizontalAlignment.Stretch});
        }
        if(attachedMenu!=null && attachedMenu.IsOpen && contextText==editor.Text)PopulateContext(contextOffset,attachedMenu);
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
        ApplyIssue(issue,(string)replacement.Tag);
    }
    internal bool ApplyIssue(WritingIssue issue,string replacement)
    {if(editor.IsReadOnly || !Current(issue))return false;editor.Select(issue.Start,issue.Length);editor.SelectedText=replacement;editor.Focus();return true;}
    bool Current(WritingIssue issue){return issue!=null && analyzedText==editor.Text && issue.Fits(editor.Text) && settings.Enabled(issue.Rule) && !review.IsIgnored(issue.Rule,issue.Start,issue.Length,issue.Original,settings.Language);}
    void Ignore(){IgnoreIssue(Selected);}
    internal void IgnoreIssue(WritingIssue issue){if(!Current(issue))return;review.Ignore(new DismissedHint {Rule=issue.Rule,Start=issue.Start,Length=issue.Length,Original=issue.Original,Language=settings.Language});Render();if(ReviewChanged!=null)ReviewChanged();}
    public void AttachContextMenu(ContextMenu menu)
    {
        attachedMenu=menu;
        editor.ContextMenuOpening+=delegate(object sender,ContextMenuEventArgs e)
        {
            contextOffset=e.CursorLeft<0?editor.SelectionStart:editor.OffsetAt(new Point(e.CursorLeft,e.CursorTop));contextText=editor.Text;PopulateContext(contextOffset,menu);
            if(analyzedText!=editor.Text || stale)Check();
        };
    }
    internal void PopulateContext(int offset,ContextMenu menu)
    {
        for(int i=menu.Items.Count-1;i>=0;i--){var element=menu.Items[i] as FrameworkElement;if(element!=null && element.Tag==proofMenuTag)menu.Items.RemoveAt(i);}
        var found=issues.Where(x=>x.Rule!="synonym" && Current(x) && offset>=x.Start && offset<=x.Start+Math.Max(1,x.Length)).OrderBy(x=>x.Category=="Rechtschreibung"?0:x.Category=="Grammatik"?1:2).ThenBy(x=>x.Length).Take(5).ToList();
        var additions=new ContextMenu();
        foreach(var issue in found)
        {
            ItemsControl parent=additions;
            if(found.Count>1){var group=new MenuItem {Header=issue.Category+": "+Short(issue.Original),Tag=proofMenuTag};additions.Items.Add(group);parent=group;}
            parent.Items.Add(new MenuItem {Header=new TextBlock {Text=issue.Category+" · "+issue.Message,TextWrapping=TextWrapping.Wrap,MaxWidth=410},IsEnabled=false,Tag=proofMenuTag});
            foreach(string value in issue.Replacements.Take(8))
            {string replacement=value;var item=new MenuItem {Header="Vorschlag: "+(replacement.Length==0?"[Entfernen]":replacement),IsEnabled=!editor.IsReadOnly,Tag=proofMenuTag};item.Click+=delegate{ApplyIssue(issue,replacement);};parent.Items.Add(item);}
            if(issue.Replacements.Count==0)parent.Items.Add(new MenuItem {Header="Bewusst überarbeiten oder Hinweis ignorieren",IsEnabled=false,Tag=proofMenuTag});
            var ignore=new MenuItem {Header="Diesen Hinweis ignorieren",Tag=proofMenuTag};ignore.Click+=delegate{IgnoreIssue(issue);};parent.Items.Add(ignore);
            var show=new MenuItem {Header="In der Schreibprüfung anzeigen",Tag=proofMenuTag};show.Click+=delegate{if(RevealRequested!=null)RevealRequested();var item=hints.Items.Cast<ListBoxItem>().FirstOrDefault(x=>((WritingIssue)x.Tag).Key==issue.Key);if(item==null){item=new ListBoxItem {Tag=issue,Content=issue.Message};hints.Items.Add(item);}hints.SelectedItem=item;hints.ScrollIntoView(item);};parent.Items.Add(show);
        }
        if(found.Count==0)additions.Items.Add(new MenuItem {Header=busy?"Text wird im Hintergrund geprüft …":"Kein aktiver Hinweis an dieser Stelle",IsEnabled=false,Tag=proofMenuTag});
        var check=new MenuItem {Header="Text jetzt prüfen",Tag=proofMenuTag};check.Click+=delegate{if(RevealRequested!=null)RevealRequested();Check();};additions.Items.Add(check);additions.Items.Add(new Separator {Tag=proofMenuTag});
        int insert=0;while(additions.Items.Count>0){var item=additions.Items[0];additions.Items.RemoveAt(0);menu.Items.Insert(insert++,item);}
    }
    static string Short(string text){text=(text??"").Replace("\r"," ").Replace("\n"," ");return text.Length<=55?text:text.Substring(0,52)+"…";}
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
            analyzedText=text;issues=found;stale=false;category.SelectedIndex=0;Render();state.Text=found.Count==0?"Keine lokalen Synonyme gefunden. Grundform prüfen.":"Synonyme lokal · Bedeutung und Beugung vor dem Übernehmen prüfen";if(hints.Items.Count>0)hints.SelectedIndex=0;
        }
        catch(Exception ex){if(!closed && version==generation)state.Text="Synonymwörterbuch nicht verfügbar: "+ex.Message;}
    }
    public void Settings(){var dialog=new WritingSettingsWindow(settings){Owner=Window.GetWindow(this)};if(dialog.ShowDialog()!=true)return;settings=dialog.Value;Persist();Invalidate();Check();}
    public void Dispose(){closed=true;if(checking!=null)checking.Cancel();grammar.Dispose();}
}
