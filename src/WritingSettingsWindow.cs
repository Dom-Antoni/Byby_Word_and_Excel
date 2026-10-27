using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

internal sealed class WritingSettingsWindow : Window
{
    public WritingSettings Value;
    public WritingSettingsWindow(WritingSettings original)
    {
        Value=original.Copy();Ui.Window(this,"Schreib · Einstellungen");Width=700;Height=Math.Min(740,SystemParameters.WorkArea.Height-32);MinWidth=600;MinHeight=Math.Min(500,SystemParameters.WorkArea.Height-32);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel {Margin=new Thickness(22)};Content=root;
        var actions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,14,0,0)};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        var scroll=new ScrollViewer {VerticalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(scroll);var fields=new StackPanel();scroll.Content=fields;
        Label(fields,"Prüfung an deinen Schreibstil anpassen",22);
        Label(fields,"Stilhinweise sind Vorschläge. Fehlerkorrekturen und Stilregeln lassen sich einzeln abschalten.",14);
        var automatic=new CheckBox {Content="Nach einer Schreibpause automatisch prüfen",IsChecked=Value.Automatic,Margin=new Thickness(0,10,0,6)};fields.Children.Add(automatic);
        var pause=Number(fields,"Schreibpause bis zur Prüfung (800–5000 Millisekunden)",Value.PauseMilliseconds);
        var distance=Number(fields,"Wiederholungsabstand in Wörtern (1–100)",Value.RepeatDistance);
        var sentence=Number(fields,"Hinweis ab dieser Satzlänge (10–100 Wörter)",Value.LongSentence);
        var paragraph=Number(fields,"Hinweis ab dieser Absatzlänge (30–1000 Wörter)",Value.LongParagraph);
        Label(fields,"Stilregeln",18);var toggles=new Dictionary<string,CheckBox>();
        string[,] rules={{"repeat","Wiederholungen und mögliche Wortfamilien"},{"filler","Füllwörter"},{"wordy","Umständliche Wendungen"},{"because","Kürzere Begründungen"},{"redundancy","Mögliche doppelte Informationen"},{"passive","Mögliche Passivformen"},{"nominal","Möglicher Nominalstil"},{"sentence","Lange Sätze"},{"paragraph","Lange Absätze"},{"spaces","Mehrfache Leerzeichen"},{"quotes","Typografische Anführungszeichen"},{"terms","Bevorzugte Schreibweisen"}};
        for(int i=0;i<rules.GetLength(0);i++){var check=new CheckBox {Content=rules[i,1],IsChecked=Value.Enabled(rules[i,0]),Margin=new Thickness(0,4,0,4)};toggles[rules[i,0]]=check;fields.Children.Add(check);}
        Label(fields,"Eigene Wörter – ein Wort pro Zeile",18);var words=Multiline(fields,string.Join(Environment.NewLine,Value.PersonalWords));
        Label(fields,"Bevorzugte Schreibweisen – z. B. Email=>E-Mail (eine pro Zeile)",18);var terms=Multiline(fields,Value.Terms);
        Label(fields,"Deaktivierte Grammatikregeln (Regel-IDs, eine pro Zeile)",18);var disabled=Multiline(fields,string.Join(Environment.NewLine,Value.Disabled.Where(x=>x.StartsWith("lt:"))));
        Label(fields,"Zum Reaktivieren eine Grammatikregel aus dieser Liste entfernen. Eigene Wörter werden bei Rechtschreib- und Wiederholungshinweisen berücksichtigt.",13);
        Ui.Button(actions,"Speichern",delegate
        {
            int d,s,p,pauseMs;if(!int.TryParse(pause.Text,out pauseMs)||pauseMs<800||pauseMs>5000||!int.TryParse(distance.Text,out d)||d<1||d>100||!int.TryParse(sentence.Text,out s)||s<10||s>100||!int.TryParse(paragraph.Text,out p)||p<30||p>1000){MessageBox.Show("Bitte Zahlen innerhalb der angegebenen Bereiche eingeben.","Einstellungen");return;}
            foreach(string line in terms.Text.Split('\n')){if(string.IsNullOrWhiteSpace(line))continue;var pair=line.Trim().Split(new[]{"=>"},StringSplitOptions.None);if(pair.Length!=2 || string.IsNullOrWhiteSpace(pair[0]) || string.IsNullOrWhiteSpace(pair[1])){MessageBox.Show("Schreibweisen bitte als Variante=>Bevorzugt eingeben.");return;}}
            Value.RepeatDistance=d;Value.LongSentence=s;Value.LongParagraph=p;Value.PersonalWords=Lines(words.Text);Value.Terms=terms.Text.Replace("\r","");
            Value.Automatic=automatic.IsChecked==true;Value.PauseMilliseconds=pauseMs;
            Value.Disabled=Lines(disabled.Text).Where(x=>x.StartsWith("lt:")).ToList();foreach(var pair in toggles)if(pair.Value.IsChecked!=true)Value.Disabled.Add(pair.Key);
            DialogResult=true;
        });
        Ui.Button(actions,"Abbrechen",delegate{DialogResult=false;}).IsCancel=true;
    }
    static List<string> Lines(string text){return text.Split('\n').Select(x=>x.Trim()).Where(x=>x.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();}
    static void Label(Panel panel,string text,int size){panel.Children.Add(new TextBlock {Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,6)});}
    static TextBox Number(Panel panel,string label,int number){Label(panel,label,14);var field=new TextBox {Text=number.ToString(),Width=90,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(6)};panel.Children.Add(field);return field;}
    static TextBox Multiline(Panel panel,string value){var field=new TextBox {Text=value,AcceptsReturn=true,Height=85,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(6)};panel.Children.Add(field);return field;}
}
