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
using Microsoft.Win32;

internal sealed class Schreib : Window
{
    readonly TextBox editor = new TextBox();
    readonly TextBlock status = new TextBlock();
    readonly WritingPanel assistant;
    string path; bool dirty;
    readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
    [STAThread] public static void Main() { new Application().Run(new Schreib()); }
    public Schreib()
    {
        Ui.Window(this, "Schreib · Lokal schreiben"); Width = 1280; MinWidth = 980; MinHeight = 650;
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var heading = new TextBlock { Text = "Schreib", FontSize = 30, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,16) };
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var bar = new WrapPanel { Margin = new Thickness(0,0,0,18) };
        DockPanel.SetDock(bar, Dock.Top); root.Children.Add(bar);
        Ui.Button(bar, "Neu", delegate { if (CanLeave()) { editor.Clear(); path = null; dirty = false; UpdateTitle(); } });
        Ui.Button(bar, "Öffnen…", Open);
        Ui.Button(bar, "Speichern", delegate { Save(false); });
        Ui.Button(bar, "Speichern unter…", delegate { Save(true); });
        var language = new ComboBox { Width = 130, Margin = new Thickness(12,0,0,0), ItemsSource = new[] { "Deutsch", "English (UK)", "English (US)" }, SelectedIndex = 0 };
        language.SelectionChanged += delegate { editor.Language = XmlLanguage.GetLanguage(new[] { "de-DE", "en-GB", "en-US" }[language.SelectedIndex]); if(assistant!=null)assistant.SetLanguage(language.SelectedIndex); };
        bar.Children.Add(language);
        status.Margin = new Thickness(0,14,0,0); DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        assistant = new WritingPanel(editor) { Width = 380, Margin = new Thickness(20,0,0,0) }; DockPanel.SetDock(assistant, Dock.Right); root.Children.Add(assistant);
        language.SelectedIndex = assistant.LanguageIndex;
        Ui.Button(bar, "Prüfen", delegate { timer.Stop();assistant.Check(); });
        Ui.Button(bar, "Synonyme", delegate { assistant.FindSynonyms(); });
        Ui.Button(bar, "Einstellungen", delegate { assistant.Settings(); });
        editor.AcceptsReturn = true; editor.AcceptsTab = true; editor.TextWrapping = TextWrapping.Wrap;
        editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; editor.FontSize = 19;
        editor.Padding = new Thickness(28); editor.Background = Brushes.White; editor.BorderBrush = Brushes.LightGray;
        editor.Language = XmlLanguage.GetLanguage(new[] { "de-DE", "en-GB", "en-US" }[language.SelectedIndex]); SpellCheck.SetIsEnabled(editor, false);
        root.Children.Add(editor);
        editor.TextChanged += delegate { dirty = true; UpdateTitle(); assistant.Invalidate(); status.Text=Writing.Statistics(editor.Text); timer.Stop(); timer.Start(); };
        timer.Tick += delegate { timer.Stop(); Analyze(); };
        Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { e.Cancel = !CanLeave(); };
        Closed += delegate {timer.Stop();assistant.Dispose();};
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
        var dialog = new OpenFileDialog { Filter = "Text / Word (*.txt;*.md;*.docx)|*.txt;*.md;*.docx" };
        if (dialog.ShowDialog() != true) return;
        bool docx = Path.GetExtension(dialog.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase);
        if(docx) MessageBox.Show("DOCX wird als Text importiert. Layout, Bilder, Kommentare und andere Word-Funktionen werden nicht übernommen. Speichere den Text anschließend als TXT oder Markdown.", "Textimport");
        string text = docx ? Formats.ReadDocx(dialog.FileName) : File.ReadAllText(dialog.FileName, Encoding.UTF8);
        editor.Text = text; path = docx ? null : dialog.FileName; dirty = docx; UpdateTitle();
    }
    bool Save(bool saveAs)
    {
        string target = path;
        if (target == null || saveAs)
        {
            var dialog = new SaveFileDialog { Filter = "Textdatei (*.txt)|*.txt|Markdown (*.md)|*.md", FileName = path == null ? "Text.txt" : Path.GetFileName(path) };
            if (dialog.ShowDialog() != true) return false;
            target = dialog.FileName;
        }
        try { Ui.AtomicWrite(target, delegate(string temp) { File.WriteAllText(temp, editor.Text, new UTF8Encoding(true)); }); path = target; dirty = false; UpdateTitle(); return true; }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Speichern fehlgeschlagen"); return false; }
    }
}
