using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;

internal sealed class Completion
{
    readonly DocumentEditor editor;readonly Popup popup=new Popup {StaysOpen=true,AllowsTransparency=true};readonly ListBox choices=new ListBox {MinWidth=220,MaxWidth=400};
    readonly HashSet<string> german=new HashSet<string>(StringComparer.OrdinalIgnoreCase),english=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    int start,length;string snapshot;
    public string Language="de-DE";
    public bool Enabled=true;
    public Completion(DocumentEditor editor)
    {
        this.editor=editor;
        foreach(string word in ("Abbildung allerdings beispielsweise beziehungsweise deshalb deutlich Ergebnisse Forschung gemeinsam grundsätzlich insbesondere Literatur möglicherweise Referenzen schreiben schließlich Sprache Untersuchung verbessern Vergleich wahrscheinlich wissenschaftlich Zusammenhang zusätzlich").Split(' '))german.Add(word);
        foreach(string word in ("although analysis because conclusion consequently discussion evidence example furthermore however important information literature methodology references research results significant therefore understanding writing").Split(' '))english.Add(word);
        string data=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"synonyms","openthesaurus.txt");if(File.Exists(data))foreach(string line in File.ReadLines(data)){if(line.StartsWith("#"))continue;foreach(string word in line.Split(';'))if(Regex.IsMatch(word,@"^\p{L}{3,35}$"))german.Add(word);}
        string dict=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"synonyms","dict","index.noun");if(File.Exists(dict))foreach(string line in File.ReadLines(dict)){string word=line.Split(' ')[0];if(Regex.IsMatch(word,@"^[a-z]{3,35}$"))english.Add(word);}
        var frame=new Border {Child=choices,BorderThickness=new Thickness(1),Padding=new Thickness(4)};Theme.Bind(frame,Border.BackgroundProperty,"App.Control");Theme.Bind(frame,Border.BorderBrushProperty,"App.Border");popup.Child=frame;
        popup.PlacementTarget=editor;popup.Placement=PlacementMode.Relative;
        editor.TextChanged+=delegate{if(!editor.PresentationChanging)editor.Dispatcher.BeginInvoke(new Action(Refresh));};editor.SelectionChanged+=delegate{Close();};
        editor.PreviewKeyDown+=Key;editor.LostKeyboardFocus+=delegate{if(!choices.IsKeyboardFocusWithin)Close();};
        choices.PreviewKeyDown+=Key;
    }
    public void Close(){popup.IsOpen=false;}
    public void Refresh()
    {
        Close();if(!Enabled || editor.SelectionLength!=0)return;
        string text=editor.Text;int caret=editor.SelectionStart;if(caret>text.Length)return;
        var prefix=Regex.Match(text.Substring(0,caret),@"\p{L}{2,}$");if(!prefix.Success || caret<text.Length && char.IsLetter(text[caret]))return;
        string fragment=prefix.Value;start=prefix.Index;length=fragment.Length;snapshot=text;
        var local=Writing.Words.Matches(text).Cast<Match>().Select(x=>x.Value);
        var phrases=Regex.Matches(text,@"\p{L}+(?: +\p{L}+){1,2}").Cast<Match>().Select(x=>x.Value);
        var common=Language.StartsWith("de")?new[]{"zum Beispiel","im Rahmen","in Bezug auf","darüber hinaus"}:new[]{"for example","in addition","as a result","according to"};
        var suggestions=local.Concat(phrases).Concat(common).Concat(Language.StartsWith("de")?german:english).Where(x=>x.Length>fragment.Length && x.StartsWith(fragment,StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
        if(suggestions.Length==0)return;
        choices.ItemsSource=suggestions;choices.SelectedIndex=0;
        var rect=editor.CaretPosition.GetCharacterRect(LogicalDirection.Forward);popup.HorizontalOffset=Math.Max(0,rect.Left);popup.VerticalOffset=Math.Max(0,rect.Bottom+4);popup.IsOpen=true;
    }
    void Key(object sender,KeyEventArgs e)
    {
        if(!popup.IsOpen || Keyboard.Modifiers!=ModifierKeys.None)return;
        if(e.Key==System.Windows.Input.Key.Escape){Close();e.Handled=true;}
        else if(e.Key==System.Windows.Input.Key.Down || e.Key==System.Windows.Input.Key.Up){choices.SelectedIndex=(choices.SelectedIndex+(e.Key==System.Windows.Input.Key.Down?1:choices.Items.Count-1))%choices.Items.Count;e.Handled=true;}
        else if(e.Key==System.Windows.Input.Key.Tab){string word=choices.SelectedItem as string;Close();if(snapshot==editor.Text && word!=null){editor.Select(start,length);editor.SelectedText=word;editor.Select(start+word.Length,0);}e.Handled=true;}
        else if(e.Key==System.Windows.Input.Key.Enter)Close();
    }
}
