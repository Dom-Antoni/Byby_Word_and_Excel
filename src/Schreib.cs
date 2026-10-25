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
    string documentNote="";
    string path; bool dirty;
    readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
    [STAThread] public static void Main(string[] args)
    {
        var app=new Application();var window=new Schreib();
        if(args.Length>0)window.Loaded+=delegate{window.SafeOpen(args[0]);};
        app.Run(window);
    }
    public Schreib()
    {
        Ui.Window(this, "Schreib · Dokument"); Width = Math.Min(1440,SystemParameters.WorkArea.Width-24);Height=Math.Min(760,SystemParameters.WorkArea.Height-24);MinWidth=Math.Min(1000,Width);MinHeight=Math.Min(560,Height);
        var root = new DockPanel(); Content = root;
        var heading=new Border {Background=new SolidColorBrush(Color.FromRgb(35,66,110)),Padding=new Thickness(20,10,20,10),Child=new TextBlock {Text="Schreib  ·  Dein Dokument",Foreground=Brushes.White,FontSize=20,FontWeight=FontWeights.SemiBold}};
        DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var ribbon=new StackPanel {Margin=new Thickness(0,0,0,1)};Theme.Bind(ribbon,Panel.BackgroundProperty,"App.Panel");DockPanel.SetDock(ribbon,Dock.Top);root.Children.Add(ribbon);
        var menus=new Menu {Padding=new Thickness(12,2,12,2)};ribbon.Children.Add(menus);
        var bar = new WrapPanel { Margin = new Thickness(16,8,16,10) };ribbon.Children.Add(bar);
        Ui.Button(bar, "Speichern", delegate { Save(false); });
        Ui.Button(bar,"Rückgängig",delegate{if(editor.CanUndo)editor.Undo();});Ui.Button(bar,"Wiederholen",delegate{if(editor.CanRedo)editor.Redo();});
        status.Margin = new Thickness(20,8,20,8); DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var tabs=new TabControl {Width=390,Margin=new Thickness(0,14,14,0)};DockPanel.SetDock(tabs,Dock.Right);root.Children.Add(tabs);
        assistant = new WritingPanel(editor) {Margin=new Thickness(12)};tabs.Items.Add(new TabItem {Header="Schreibprüfung",Content=assistant});
        references=new ReferencePanel(editor){Margin=new Thickness(12)};var refTab=new TabItem {Header="Referenzen",Content=new ScrollViewer {Content=references,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};tabs.Items.Add(refTab);
        references.Reveal+=delegate{tabs.SelectedItem=refTab;};references.Changed+=delegate{dirty=true;UpdateTitle();};
        completion=new Completion(editor);
        int languageIndex=assistant.LanguageIndex;editor.Language=XmlLanguage.GetLanguage(new[]{"de-DE","en-GB","en-US"}[languageIndex]);completion.Language=new[]{"de-DE","en-GB","en-US"}[languageIndex];
        var format=new WrapPanel();bar.Children.Add(format);
        var font=new ComboBox {Width=130,ItemsSource=new[]{"Calibri","Arial","Cambria","Georgia","Times New Roman"},SelectedIndex=0,Margin=new Thickness(0,0,8,0)};format.Children.Add(font);font.SelectionChanged+=delegate{editor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty,new FontFamily((string)font.SelectedItem));editor.Focus();};
        var size=new ComboBox {Width=60,ItemsSource=new[]{12.0,14,16,18,20,24,28,32},SelectedIndex=2,Margin=new Thickness(0,0,8,0)};format.Children.Add(size);size.SelectionChanged+=delegate{editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,(double)size.SelectedItem);editor.Focus();};
        Ui.Button(format,"Fett",delegate{EditingCommands.ToggleBold.Execute(null,editor);});Ui.Button(format,"Kursiv",delegate{EditingCommands.ToggleItalic.Execute(null,editor);});
        var file=Item(menus,"_Datei");Item(file,"_Neu",New,"Strg+N");Item(file,"Ö_ffnen…",Open,"Strg+O");Item(file,"_Speichern",delegate{Save(false);},"Strg+S");Item(file,"Speichern _unter…",delegate{Save(true);},"Strg+Umschalt+S");file.Items.Add(new Separator());var export=Item(file,"Exportieren");Item(export,"RTF mit Literaturverzeichnis…",delegate{Export(false);});Item(export,"RTF für EndNote…",delegate{Export(true);});file.Items.Add(new Separator());Item(file,"Schließen",Close,"Alt+F4");
        var edit=Item(menus,"_Bearbeiten");Command(edit,"Rückgängig",ApplicationCommands.Undo,"Strg+Z");Command(edit,"Wiederholen",ApplicationCommands.Redo,"Strg+Y");edit.Items.Add(new Separator());Command(edit,"Ausschneiden",ApplicationCommands.Cut,"Strg+X");Command(edit,"Kopieren",ApplicationCommands.Copy,"Strg+C");Command(edit,"Einfügen",ApplicationCommands.Paste,"Strg+V");Command(edit,"Alles auswählen",ApplicationCommands.SelectAll,"Strg+A");
        var formatting=Item(menus,"F_ormat");Command(formatting,"Fett",EditingCommands.ToggleBold,"Strg+B");Command(formatting,"Kursiv",EditingCommands.ToggleItalic,"Strg+I");Command(formatting,"Unterstrichen",EditingCommands.ToggleUnderline,"Strg+U");Item(formatting,"Überschrift",delegate{editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,26.0);editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,FontWeights.Bold);editor.Focus();});Item(formatting,"Normaler Text",delegate{editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty,16.0);editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,FontWeights.Normal);editor.Focus();});Command(formatting,"Aufzählung",EditingCommands.ToggleBullets);Command(formatting,"Linksbündig",EditingCommands.AlignLeft);Command(formatting,"Zentriert",EditingCommands.AlignCenter);Command(formatting,"Rechtsbündig",EditingCommands.AlignRight);
        var sources=Item(menus,"_Referenzen");Item(sources,"Referenz hinzufügen…",references.AddReference);Item(sources,"Quellenbibliothek anzeigen",delegate{tabs.Visibility=Visibility.Visible;tabs.SelectedItem=refTab;});Item(sources,"RTF für EndNote exportieren…",delegate{Export(true);});
        var proof=Item(menus,"_Prüfen");Item(proof,"Jetzt prüfen",delegate{timer.Stop();assistant.Check();});Item(proof,"Synonyme",assistant.FindSynonyms);Item(proof,"Prüfeinstellungen…",assistant.Settings);
        var languages=Item(proof,"Sprache");var languageItems=new System.Collections.Generic.List<MenuItem>();for(int i=0;i<3;i++){int index=i;var entry=Item(languages,new[]{"Deutsch","English (UK)","English (US)"}[i],delegate{languageIndex=index;editor.Language=XmlLanguage.GetLanguage(new[]{"de-DE","en-GB","en-US"}[index]);completion.Language=new[]{"de-DE","en-GB","en-US"}[index];assistant.SetLanguage(index);for(int n=0;n<languageItems.Count;n++)languageItems[n].IsChecked=n==index;});entry.IsCheckable=true;entry.IsChecked=i==languageIndex;languageItems.Add(entry);}
        var suggest=Item(proof,"Wortvorschläge (Tab übernimmt)");suggest.IsCheckable=true;suggest.IsChecked=true;suggest.Click+=delegate{completion.Enabled=suggest.IsChecked && !editor.IsReadOnly;completion.Close();};
        var view=Item(menus,"_Ansicht");var modes=Item(view,"Darstellung");var appearanceItems=new System.Collections.Generic.List<MenuItem>();foreach(string mode in new[]{"System","Light","Dark"}){string chosen=mode;var entry=Item(modes,mode=="System"?"Wie Windows":mode=="Light"?"Hell":"Dunkel",delegate{Theme.Set(chosen,Theme.Current.DarkDocument);});entry.IsCheckable=true;entry.IsChecked=Theme.Current.Mode==mode;entry.Tag=mode;appearanceItems.Add(entry);}
        var paper=Item(view,"Dunkle Dokumentfläche im Dunkelmodus");paper.IsCheckable=true;paper.IsChecked=Theme.Current.DarkDocument;paper.Click+=delegate{Theme.Set(Theme.Current.Mode,paper.IsChecked);};
        var sidebar=Item(view,"Seitenleiste anzeigen");sidebar.IsCheckable=true;sidebar.IsChecked=true;sidebar.Click+=delegate{tabs.Visibility=sidebar.IsChecked?Visibility.Visible:Visibility.Collapsed;};
        Item(view,"Schreibprüfung anzeigen",delegate{tabs.Visibility=Visibility.Visible;sidebar.IsChecked=true;tabs.SelectedIndex=0;});Item(view,"Referenzen anzeigen",delegate{tabs.Visibility=Visibility.Visible;sidebar.IsChecked=true;tabs.SelectedItem=refTab;});
        var reading=Item(view,"Lesemodus");reading.IsCheckable=true;reading.Click+=delegate{editor.IsReadOnly=reading.IsChecked;format.IsEnabled=!reading.IsChecked;formatting.IsEnabled=!reading.IsChecked;completion.Enabled=suggest.IsChecked && !reading.IsChecked;completion.Close();};
        editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        editor.BorderThickness=new Thickness(0);SpellCheck.SetIsEnabled(editor, false);
        var page=new Border {Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(204,210,219)),BorderThickness=new Thickness(1),Margin=new Thickness(24,14,24,0),MaxWidth=860,Child=editor};root.Children.Add(page);
        Action appearanceChanged=delegate{editor.ApplyAppearance(Theme.Dark && Theme.Current.DarkDocument);page.Background=editor.Background;Theme.Bind(page,Border.BorderBrushProperty,"App.Border");foreach(var item in appearanceItems)item.IsChecked=(string)item.Tag==Theme.Current.Mode;paper.IsChecked=Theme.Current.DarkDocument;};Theme.Changed+=appearanceChanged;appearanceChanged();
        editor.TextChanged += delegate {if(editor.PresentationChanging)return;dirty = true; UpdateTitle(); assistant.Invalidate();Statistics();timer.Stop(); timer.Start(); };
        timer.Tick += delegate { timer.Stop(); Analyze(); };
        Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { e.Cancel = !CanLeave(); };
        Closed += delegate {Theme.Changed-=appearanceChanged;timer.Stop();completion.Close();assistant.Dispose();};
        PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Z && Keyboard.Modifiers==ModifierKeys.Control && references.ReturnPosition.HasValue){references.Return();e.Handled=true;}};
        var saveCommand = new System.Windows.Input.CommandBinding(System.Windows.Input.ApplicationCommands.Save, delegate { Save(false); });
        CommandBindings.Add(saveCommand);
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Open,delegate{Open();}));CommandBindings.Add(new CommandBinding(ApplicationCommands.New,delegate{New();}));var saveAs=new RoutedCommand();InputBindings.Add(new KeyBinding(saveAs,new KeyGesture(Key.S,ModifierKeys.Control|ModifierKeys.Shift)));CommandBindings.Add(new CommandBinding(saveAs,delegate{Save(true);}));
        AllowDrop=true;PreviewDragOver+=delegate(object sender,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop)){e.Effects=DragDropEffects.Copy;e.Handled=true;}};
        PreviewDrop+=delegate(object sender,DragEventArgs e){if(!e.Data.GetDataPresent(DataFormats.FileDrop))return;e.Handled=true;var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files!=null && files.Length>0 && CanLeave())SafeOpen(files[0]);};
        Analyze();
    }
    MenuItem Item(ItemsControl parent,string title,Action action=null,string shortcut=null){var item=new MenuItem {Header=title,InputGestureText=shortcut??""};if(action!=null)item.Click+=delegate{try{action();}catch(Exception ex){MessageBox.Show(ex.Message,"Aktion fehlgeschlagen");}};parent.Items.Add(item);return item;}
    void Command(ItemsControl parent,string title,ICommand command,string shortcut=null){var item=Item(parent,title,null,shortcut);item.Command=command;item.CommandTarget=editor;}
    void New(){if(CanLeave()){editor.Clear();editor.ClearUndo();references.ResetDocument();documentNote="";path=null;dirty=false;UpdateTitle();Statistics();}}
    void SafeOpen(string fileName){try{OpenPath(Path.GetFullPath(fileName));}catch(Exception ex){MessageBox.Show(ex.Message,"Datei konnte nicht geöffnet werden",MessageBoxButton.OK,MessageBoxImage.Error);}}
    void Statistics(){status.Text=Writing.Statistics(editor.Text)+(string.IsNullOrWhiteSpace(documentNote)?"":"\n"+documentNote);status.TextWrapping=TextWrapping.Wrap;}
    void Analyze()
    {
        Statistics();
        assistant.Check();
    }
    void UpdateTitle() { Title = (dirty ? "● " : "") + (path == null ? "Unbenannt" : Path.GetFileName(path)) + " · Schreib"; }
    bool CanLeave() { return Ui.ConfirmSave(dirty, delegate { return Save(false); }); }
    void Open()
    {
        if (!CanLeave()) return;
        var dialog = new OpenFileDialog { Filter = DocumentFiles.Filter };
        if (dialog.ShowDialog() != true) return;
        OpenPath(dialog.FileName);
    }
    void OpenPath(string fileName)
    {
        string extension=Path.GetExtension(fileName).ToLowerInvariant();
        if(extension==".schreib")
        {
            var document=new JavaScriptSerializer {MaxJsonLength=50000000}.Deserialize<WritingDocument>(File.ReadAllText(fileName));
            if(document==null || document.Version!=1 || document.References==null || document.Citations==null || document.BibliographyOnly==null)throw new FormatException("Ungültiges Schreib-Dokument.");
            // Validate in a separate document before replacing the current text.
            var check=new DocumentEditor();check.LoadRtf(document.Rtf);
            if(document.References.Any(x=>x==null || string.IsNullOrWhiteSpace(x.Id) || x.Authors==null) || document.References.Select(x=>x.Id).Distinct().Count()!=document.References.Count || document.Citations.Any(x=>x==null || x.Start<0 || x.Length<1 || x.Start+ x.Length>check.Text.Length || !document.References.Any(r=>r.Id==x.ReferenceId)))throw new FormatException("Ungültige Quellen oder Zitierpositionen im Dokument.");
            check.RestoreCitations(document.Citations,document.References);
            references.ResetDocument();references.MergeDocument(document.References);references.BibliographyOnly=document.BibliographyOnly;editor.LoadRtf(document.Rtf);editor.RestoreCitations(document.Citations,references.References);references.SetStyle(document.Numbered);references.RefreshLibrary();documentNote="";path=fileName;dirty=false;UpdateTitle();Statistics();return;
        }
        if(extension==".rtf"){var check=new DocumentEditor();string rtf=Convert.ToBase64String(File.ReadAllBytes(fileName));check.LoadRtf(rtf);editor.LoadRtf(rtf);references.ResetDocument();documentNote="RTF-Dokument";path=fileName;dirty=false;UpdateTitle();Statistics();return;}
        var imported=DocumentFiles.Read(fileName);var scratch=new DocumentEditor {Document=imported.Document};string content=scratch.SaveRtf();references.ResetDocument();editor.LoadRtf(content);documentNote=imported.Note;path=imported.Converted?null:fileName;dirty=imported.Converted;UpdateTitle();Statistics();
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
