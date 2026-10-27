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
using System.Threading.Tasks;
using System.Windows.Threading;

internal sealed class CompletionIndex
{
    readonly string[] words;
    public CompletionIndex(IEnumerable<string> values){words=values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();}
    public IEnumerable<string> Find(string prefix,int count)
    {
        int lo=0,hi=words.Length;while(lo<hi){int mid=(lo+hi)/2;if(StringComparer.OrdinalIgnoreCase.Compare(words[mid],prefix)<0)lo=mid+1;else hi=mid;}
        for(int i=lo;i<words.Length && count>0 && words[i].StartsWith(prefix,StringComparison.OrdinalIgnoreCase);i++)if(words[i].Length>prefix.Length){yield return words[i];count--;}
    }
}

internal sealed class Completion : IDisposable
{
    readonly DocumentEditor editor;readonly Popup popup=new Popup {StaysOpen=true,AllowsTransparency=true};readonly ListBox choices=new ListBox {MinWidth=220,MaxWidth=400};
    readonly Task<CompletionIndex[]> dictionaries;
    readonly DispatcherTimer timer=new DispatcherTimer(DispatcherPriority.Background) {Interval=TimeSpan.FromMilliseconds(180)};
    int start,length,generation;string snapshot;bool disposed;
    public string Language="de-DE";
    public bool Enabled=true;
    public Completion(DocumentEditor editor)
    {
        this.editor=editor;
        dictionaries=Task.Run(delegate{
        var german=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var english=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string word in ("Abbildung allerdings beispielsweise beziehungsweise deshalb deutlich Ergebnisse Forschung gemeinsam grundsätzlich insbesondere Literatur möglicherweise Referenzen schreiben schließlich Sprache Untersuchung verbessern Vergleich wahrscheinlich wissenschaftlich Zusammenhang zusätzlich").Split(' '))german.Add(word);
        foreach(string word in ("although analysis because conclusion consequently discussion evidence example furthermore however important information literature methodology references research results significant therefore understanding writing").Split(' '))english.Add(word);
        string data=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"synonyms","openthesaurus.txt");if(File.Exists(data))foreach(string line in File.ReadLines(data)){if(line.StartsWith("#"))continue;foreach(string word in line.Split(';'))if(Regex.IsMatch(word,@"^\p{L}{3,35}$"))german.Add(word);}
        string dict=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"synonyms","dict","index.noun");if(File.Exists(dict))foreach(string line in File.ReadLines(dict)){string word=line.Split(' ')[0];if(Regex.IsMatch(word,@"^[a-z]{3,35}$"))english.Add(word);}
        return new[]{new CompletionIndex(german),new CompletionIndex(english)};
        });
        var frame=new Border {Child=choices,BorderThickness=new Thickness(1),Padding=new Thickness(4)};Theme.Bind(frame,Border.BackgroundProperty,"App.Control");Theme.Bind(frame,Border.BorderBrushProperty,"App.Border");popup.Child=frame;
        popup.PlacementTarget=editor;popup.Placement=PlacementMode.Relative;
        timer.Tick+=delegate{timer.Stop();Refresh();};
        editor.TextChanged+=delegate{if(editor.PresentationChanging || disposed)return;Hide();timer.Stop();timer.Start();};editor.SelectionChanged+=delegate{Hide();};
        editor.PreviewKeyDown+=Key;editor.LostKeyboardFocus+=delegate{if(!choices.IsKeyboardFocusWithin)Close();};
        choices.PreviewKeyDown+=Key;
    }
    void Hide(){generation++;popup.IsOpen=false;}
    public void Close(){Hide();timer.Stop();}
    public async void Refresh()
    {
        Close();if(disposed || !Enabled || editor.SelectionLength!=0)return;
        string text=editor.Text;int caret=editor.SelectionStart;if(caret>text.Length)return;
        int begin=caret;while(begin>0 && char.IsLetter(text[begin-1]))begin--;if(caret-begin<2 || caret<text.Length && char.IsLetter(text[caret]))return;
        string fragment=text.Substring(begin,caret-begin),language=Language;int version=generation;
        try {
        var dictionary=await dictionaries;
        var suggestions=await Task.Run(delegate{
            var local=Writing.Words.Matches(text).Cast<Match>().Select(x=>x.Value).Concat(Regex.Matches(text,@"\p{L}+(?: +\p{L}+){1,2}").Cast<Match>().Select(x=>x.Value));
            var common=language.StartsWith("de")?new[]{"zum Beispiel","im Rahmen","in Bezug auf","darüber hinaus"}:new[]{"for example","in addition","as a result","according to"};
            return new CompletionIndex(local.Concat(common)).Find(fragment,3).Concat(dictionary[language.StartsWith("de")?0:1].Find(fragment,3)).Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
        });
        if(disposed || !Enabled || version!=generation || language!=Language || text!=editor.Text || caret!=editor.SelectionStart || suggestions.Length==0 || !editor.IsKeyboardFocusWithin)return;
        start=begin;length=fragment.Length;snapshot=text;
        choices.ItemsSource=suggestions;choices.SelectedIndex=0;
        var rect=editor.CaretPosition.GetCharacterRect(LogicalDirection.Forward);popup.HorizontalOffset=Math.Max(0,rect.Left);popup.VerticalOffset=Math.Max(0,rect.Bottom+4);popup.IsOpen=true;
        }catch(Exception ex){System.Diagnostics.Debug.WriteLine("Wortvorschläge: "+ex.GetType().Name);}
    }
    void Key(object sender,KeyEventArgs e)
    {
        if(e.Handled || !popup.IsOpen || Keyboard.Modifiers!=ModifierKeys.None)return;
        if(e.Key==System.Windows.Input.Key.Escape){Close();e.Handled=true;}
        else if(e.Key==System.Windows.Input.Key.Down || e.Key==System.Windows.Input.Key.Up){choices.SelectedIndex=(choices.SelectedIndex+(e.Key==System.Windows.Input.Key.Down?1:choices.Items.Count-1))%choices.Items.Count;e.Handled=true;}
        else if(e.Key==System.Windows.Input.Key.Tab){string word=choices.SelectedItem as string;Close();if(snapshot==editor.Text && word!=null){editor.Select(start,length);editor.SelectedText=word;editor.Select(start+word.Length,0);}e.Handled=true;}
        else if(e.Key==System.Windows.Input.Key.Enter)Close();
    }
    public void Dispose(){disposed=true;Close();}
}
