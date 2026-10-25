using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Input;
using System.Web.Script.Serialization;
using Microsoft.Win32;

internal sealed class Schreib : Window
{
    readonly DocumentEditor editor = new DocumentEditor();
    readonly TextBlock status = new TextBlock();
    readonly WritingPanel assistant;
    readonly ReferencePanel references;
    readonly Completion completion;
    string path; bool dirty;
    readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
    [STAThread] public static void Main() { new Application().Run(new Schreib()); }
    public Schreib()
    {
        Ui.Window(this, "Schreib · Dokument"); Width = Math.Min(1440,SystemParameters.WorkArea.Width-24);Height=Math.Min(760,SystemParameters.WorkArea.Height-24);MinWidth=Math.Min(1050,Width);MinHeight=Math.Min(600,Height);Background=new SolidColorBrush(Color.FromRgb(232,235,240));
        var root = new DockPanel(); Content = root;
        var heading=new Border {Background=new SolidColorBrush(Color.FromRgb(35,66,110)),Padding=new Thickness(20,10,20,10),Child=new TextBlock {Text="Schreib  ·  Dein Dokument",Foreground=Brushes.White,FontSize=20,FontWeight=FontWeights.SemiBold}};
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var ribbon=new StackPanel {Background=Brushes.White,Margin=new Thickness(0,0,0,1)};DockPanel.SetDock(ribbon,Dock.Top);root.Children.Add(ribbon);
        var bar = new WrapPanel { Margin = new Thickness(16,10,16,8) };ribbon.Children.Add(bar);
        Ui.Button(bar, "Neu", delegate { if (CanLeave()) { editor.Clear();references.ResetDocument();path = null; dirty = false; UpdateTitle(); } });
        Ui.Button(bar, "Öffnen…", Open);
        Ui.Button(bar, "Speichern", delegate { Save(false); });
        Ui.Button(bar, "Speichern unter…", delegate { Save(true); });
        Ui.Button(bar,"RTF exportieren…",delegate{Export(false);});Ui.Button(bar,"EndNote-RTF…",delegate{Export(true);});
        var language = new ComboBox { Width = 130, Margin = new Thickness(12,0,0,0), ItemsSource = new[] { "Deutsch", "English (UK)", "English (US)" }, SelectedIndex = 0 };
        language.SelectionChanged += delegate { editor.Language = XmlLanguage.GetLanguage(new[] { "de-DE", "en-GB", "en-US" }[language.SelectedIndex]); if(assistant!=null)assistant.SetLanguage(language.SelectedIndex);if(completion!=null)completion.Language=new[] {"de-DE","en-GB","en-US"}[language.SelectedIndex]; };
        bar.Children.Add(language);
        status.Margin = new Thickness(20,8,20,8); DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var tabs=new TabControl {Width=390,Margin=new Thickness(0,14,14,0)};DockPanel.SetDock(tabs,Dock.Right);root.Children.Add(tabs);
        assistant = new WritingPanel(editor) {Margin=new Thickness(12)};tabs.Items.Add(new TabItem {Header="Schreibprüfung",Content=assistant});
        references=new ReferencePanel(editor){Margin=new Thickness(12)};var refTab=new TabItem {Header="Referenzen",Content=new ScrollViewer {Content=references,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};tabs.Items.Add(refTab);
        references.Reveal+=delegate{tabs.SelectedItem=refTab;};references.Changed+=delegate{dirty=true;UpdateTitle();};
        completion=new Completion(editor);
        language.SelectedIndex = assistant.LanguageIndex;
        Ui.Button(bar, "Prüfen", delegate { timer.Stop();assistant.Check(); });
        Ui.Button(bar, "Synonyme", delegate { assistant.FindSynonyms(); });
        Ui.Button(bar, "Einstellungen", delegate { assistant.Settings(); });
        var format=new WrapPanel {Margin=new Thickness(16,0,16,10)};ribbon.Children.Add(format);
        var font=new ComboBox {Width=130,ItemsSource=new[]{"Calibri","Arial","Cambria","Georgia","Times New Roman"},SelectedIndex=0,Margin=new Thickness(0,0,8,0)};format.Children.Add(font);font.SelectionChanged+=delegate{editor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty,new FontFamily((string)font.SelectedItem));editor.Focus();};
        var size=new ComboBox {Width=60,ItemsSource=new[]{12.0,14,16,18,20,24,28,32},SelectedIndex=2,Margin=new Thickness(0,0,8,0)};format.Children.Add(size);size.SelectionChanged+=delegate{editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,(double)size.SelectedItem);editor.Focus();};
        Ui.Button(format,"Fett",delegate{EditingCommands.ToggleBold.Execute(null,editor);});Ui.Button(format,"Kursiv",delegate{EditingCommands.ToggleItalic.Execute(null,editor);});Ui.Button(format,"Unterstrichen",delegate{EditingCommands.ToggleUnderline.Execute(null,editor);});
        Ui.Button(format,"Überschrift",delegate{editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,26.0);editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,FontWeights.Bold);editor.Focus();});
        Ui.Button(format,"Normal",delegate{editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,16.0);editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,FontWeights.Normal);editor.Focus();});
        Ui.Button(format,"Aufzählung",delegate{EditingCommands.ToggleBullets.Execute(null,editor);});
        Ui.Button(format,"Quellen",delegate{tabs.SelectedItem=refTab;});
        var suggest=new CheckBox {Content="Wortvorschläge · Tab übernimmt",IsChecked=true,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0)};format.Children.Add(suggest);suggest.Click+=delegate{completion.Enabled=suggest.IsChecked==true;completion.Close();};
        editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        editor.Background = Brushes.White; editor.BorderThickness=new Thickness(0);
        editor.Language = XmlLanguage.GetLanguage(new[] { "de-DE", "en-GB", "en-US" }[language.SelectedIndex]); SpellCheck.SetIsEnabled(editor, false);
        var page=new Border {Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(204,210,219)),BorderThickness=new Thickness(1),Margin=new Thickness(24,14,24,0),MaxWidth=860,Child=editor};root.Children.Add(page);
        editor.TextChanged += delegate { dirty = true; UpdateTitle(); assistant.Invalidate(); status.Text=Writing.Statistics(editor.Text); timer.Stop(); timer.Start(); };
        timer.Tick += delegate { timer.Stop(); Analyze(); };
        Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { e.Cancel = !CanLeave(); };
        Closed += delegate {timer.Stop();completion.Close();assistant.Dispose();};
        PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Z && Keyboard.Modifiers==ModifierKeys.Control && references.ReturnPosition.HasValue){references.Return();e.Handled=true;}};
        var saveCommand = new System.Windows.Input.CommandBinding(System.Windows.Input.ApplicationCommands.Save, delegate { Save(false); });
        CommandBindings.Add(saveCommand);
        Analyze();
    }
    void Analyze()
    {
        status.Text = Writing.Statistics(editor.Text); status.TextWrapping = TextWrapping.Wrap;
        assistant.Check();
    }
    void UpdateTitle() { Title = (dirty ? "● " : "") + (path == null ? "Unbenannt" : Path.GetFileName(path)) + " · Schreib"; }
    bool CanLeave() { return Ui.ConfirmSave(dirty, delegate { return Save(false); }); }
    void Open()
    {
        if (!CanLeave()) return;
        var dialog = new OpenFileDialog { Filter = "Dokumente (*.schreib;*.rtf;*.txt;*.md;*.docx)|*.schreib;*.rtf;*.txt;*.md;*.docx" };
        if (dialog.ShowDialog() != true) return;
        string extension=Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if(extension==".schreib")
        {
            var document=new JavaScriptSerializer {MaxJsonLength=50000000}.Deserialize<WritingDocument>(File.ReadAllText(dialog.FileName));
            if(document==null || document.Version!=1 || document.References==null || document.Citations==null || document.BibliographyOnly==null)throw new FormatException("Ungültiges Schreib-Dokument.");
            // Validate in a separate document before replacing the current text.
            var check=new DocumentEditor();check.LoadRtf(document.Rtf);
            if(document.References.Any(x=>x==null || string.IsNullOrWhiteSpace(x.Id) || x.Authors==null) || document.References.Select(x=>x.Id).Distinct().Count()!=document.References.Count || document.Citations.Any(x=>x==null || x.Start<0 || x.Length<1 || x.Start+ x.Length>check.Text.Length || !document.References.Any(r=>r.Id==x.ReferenceId)))throw new FormatException("Ungültige Quellen oder Zitierpositionen im Dokument.");
            check.RestoreCitations(document.Citations,document.References);
            references.ResetDocument();references.MergeDocument(document.References);references.BibliographyOnly=document.BibliographyOnly;editor.LoadRtf(document.Rtf);editor.RestoreCitations(document.Citations,references.References);references.SetStyle(document.Numbered);references.RefreshLibrary();path=dialog.FileName;dirty=false;UpdateTitle();return;
        }
        if(extension==".rtf"){var check=new DocumentEditor();string rtf=Convert.ToBase64String(File.ReadAllBytes(dialog.FileName));check.LoadRtf(rtf);editor.LoadRtf(rtf);references.ResetDocument();path=dialog.FileName;dirty=false;UpdateTitle();return;}
        bool docx = extension==".docx";
        if(docx) MessageBox.Show("DOCX wird als Text importiert. Layout, Bilder, Kommentare und andere Word-Funktionen werden nicht übernommen. Speichere den Text anschließend als TXT oder Markdown.", "Textimport");
        string text = docx ? Formats.ReadDocx(dialog.FileName) : File.ReadAllText(dialog.FileName, Encoding.UTF8);
        editor.Text = text;references.ResetDocument();path = docx ? null : dialog.FileName; dirty = docx; UpdateTitle();
    }
    bool Save(bool saveAs)
    {
        string target = path;
        if (target == null || saveAs)
        {
            var dialog = new SaveFileDialog { Filter = "Schreib-Dokument (*.schreib)|*.schreib|Rich Text (*.rtf)|*.rtf|Textdatei (*.txt)|*.txt|Markdown (*.md)|*.md", FileName = path == null ? "Dokument.schreib" : Path.GetFileName(path) };
            if (dialog.ShowDialog() != true) return false;
            target = dialog.FileName;
        }
        try
        {
            string extension=Path.GetExtension(target).ToLowerInvariant();
            if(extension!=".schreib" && (references.References.Count>0 || editor.Citations().Count>0) && MessageBox.Show("Quellverknüpfungen und importierte Referenzen bleiben nur im .schreib-Format erhalten. Trotzdem eine vereinfachte Datei speichern?","Dokumentformat",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return false;
            Ui.AtomicWrite(target,delegate(string temp){if(extension==".schreib"){var document=new WritingDocument {Rtf=editor.SaveRtf(),References=references.DocumentReferences(),Citations=editor.Citations(),BibliographyOnly=references.BibliographyOnly,Numbered=references.Numbered};File.WriteAllText(temp,new JavaScriptSerializer {MaxJsonLength=50000000}.Serialize(document),new UTF8Encoding(false));}else if(extension==".rtf")File.WriteAllBytes(temp,Convert.FromBase64String(editor.SaveRtf()));else File.WriteAllText(temp,editor.Text,new UTF8Encoding(true));});path=target;dirty=false;UpdateTitle();return true;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Speichern fehlgeschlagen"); return false; }
    }
    void Export(bool endnote)
    {
        var dialog=new SaveFileDialog {Filter="Rich Text (*.rtf)|*.rtf",FileName=endnote?"Dokument-EndNote.rtf":"Dokument.rtf"};if(dialog.ShowDialog()!=true)return;
        string rtf=references.ExportRtf(endnote);Ui.AtomicWrite(dialog.FileName,delegate(string temp){File.WriteAllBytes(temp,Convert.FromBase64String(rtf));});
        if(endnote)MessageBox.Show("In EndNote: Tools → Format Paper. Die RTF-Datei mit der passenden Bibliothek formatieren. EndNote erzeugt daraus eine separate formatierte Datei.","EndNote-RTF exportiert");
    }
}
