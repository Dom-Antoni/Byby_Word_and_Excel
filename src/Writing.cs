using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

internal sealed class WritingSettings
{
    public int RepeatDistance = 7;
    public int LongSentence = 28;
    public int LongParagraph = 120;
    public bool Automatic = true;
    public int PauseMilliseconds = 1400;
    public string Language = "de-DE";
    public List<string> Disabled = new List<string>();
    public List<string> PersonalWords = new List<string>();
    public string Terms = "";
    public static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Byby_Word_and_Excel","writing.json"); } }
    public static WritingSettings Load()
    {
        if(!File.Exists(FilePath))return new WritingSettings();
        var settings=new JavaScriptSerializer().Deserialize<WritingSettings>(File.ReadAllText(FilePath));
        if(settings==null || settings.Disabled==null || settings.PersonalWords==null)throw new FormatException("Ungültige Schreib-Einstellungen.");
        settings.RepeatDistance=Math.Max(1,Math.Min(100,settings.RepeatDistance));
        settings.LongSentence=Math.Max(10,Math.Min(100,settings.LongSentence));
        settings.LongParagraph=Math.Max(30,Math.Min(1000,settings.LongParagraph));
        settings.PauseMilliseconds=Math.Max(800,Math.Min(5000,settings.PauseMilliseconds));
        if(!new[]{"de-DE","en-GB","en-US"}.Contains(settings.Language))settings.Language="de-DE";
        return settings;
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        Ui.AtomicWrite(FilePath,delegate(string temp){File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));});
    }
    public WritingSettings Copy() { return new JavaScriptSerializer().Deserialize<WritingSettings>(new JavaScriptSerializer().Serialize(this)); }
    public bool Enabled(string id) { return !Disabled.Contains(id); }
}

internal sealed class WritingIssue
{
    public int Start, Length;
    public string Rule, Category, Message, Original;
    public List<string> Replacements = new List<string>();
    public override string ToString() { return Category+" · "+Message; }
    public bool Fits(string text) { return Start>=0 && Length>=0 && Start+Length<=text.Length && text.Substring(Start,Length)==Original; }
    public string Key { get { return Rule+":"+Start+":"+Length+":"+Original; } }
}

internal static class Writing
{
    public static readonly Regex Words = new Regex(@"\p{L}+(?:['’\-]\p{L}+)*",RegexOptions.Compiled);
    static readonly HashSet<string> StopDe=new HashSet<string>(("der die das den dem des ein eine einen einem einer eines und oder aber auch als am an auf aus bei bis durch für gegen im in ins ist sind war waren wird werden wurde wurden mit nach nicht noch nur ob ohne seit so über um unter vom von vor wie zu zum zur ich du er sie es wir ihr mir mich sich man mein meine dein seine ihre unser haben hat hatte sein sei dass wenn dann doch denn weil kann können muss sollte würde hätte diese dieser dieses diesen diesem welche welcher welches sehr mehr wenig schon immer alle alles").Split(' '),StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> StopEn=new HashSet<string>(("a an the and or but as at by for from in into is are was were be been being it its of on onto to with without I you he she we they me my your his her our their them this that these those not no yes do does did have has had will would can could should may might must if then than there here which who what when where how also so very more most less much some any all both each only just about over under through up down out").Split(' '),StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string,string> IrregularDe = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { {"schreiben","schreib"},{"schreibt","schreib"},{"schrieb","schreib"},{"geschrieben","schreib"},{"gehen","geh"},{"geht","geh"},{"ging","geh"},{"gegangen","geh"},{"lesen","les"},{"liest","les"},{"las","les"},{"gelesen","les"},{"finden","find"},{"fand","find"},{"gefunden","find"} };
    static readonly Dictionary<string,string> IrregularEn = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { {"write","writ"},{"writes","writ"},{"writing","writ"},{"wrote","writ"},{"written","writ"},{"go","go"},{"goes","go"},{"went","go"},{"gone","go"},{"children","child"},{"mice","mouse"},{"better","good"},{"best","good"} };
    public static string Stem(string word,bool german)
    {
        string value=word.ToLowerInvariant(), stem;
        if((german?IrregularDe:IrregularEn).TryGetValue(value,out stem))return stem;
        if(german)
        {
            value=value.Replace("ä","a").Replace("ö","o").Replace("ü","u").Replace("ß","ss");
            if(value.StartsWith("ge") && value.Length>7)value=value.Substring(2);
            foreach(string suffix in new[]{"ern","est","en","er","es","em","et","e","n","s","t"})
                if(value.EndsWith(suffix) && value.Length-suffix.Length>=4)return value.Substring(0,value.Length-suffix.Length);
        }
        else
        {
            if(value.EndsWith("ies") && value.Length>5)return value.Substring(0,value.Length-3)+"y";
            foreach(string suffix in new[]{"ing","ed","es","s"})if(value.EndsWith(suffix) && value.Length-suffix.Length>=4)
            { value=value.Substring(0,value.Length-suffix.Length);if(value.Length>4 && value[value.Length-1]==value[value.Length-2])value=value.Substring(0,value.Length-1);return value; }
        }
        return value;
    }
    public static WritingIssue Issue(string text,Match match,string id,string category,string message,params string[] replacements)
    { return new WritingIssue {Start=match.Index,Length=match.Length,Original=match.Value,Rule=id,Category=category,Message=message,Replacements=replacements.ToList()}; }
    static void Pattern(List<WritingIssue> issues,string text,WritingSettings settings,string id,string pattern,string message,params string[] replacements)
    {
        if(settings.Enabled(id))foreach(Match match in Regex.Matches(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))issues.Add(Issue(text,match,id,"Stil",message,replacements));
    }
    public static List<WritingIssue> Analyze(string text,WritingSettings settings)
    {
        var issues=new List<WritingIssue>();bool german=settings.Language.StartsWith("de");
        var words=Words.Matches(text).Cast<Match>().ToArray();var stop=german?StopDe:StopEn;
        for(int i=0;i<words.Length;i++)
        {
            if(settings.Enabled("repeat") && (words[i].Length>=3 || i>0 && string.Equals(words[i].Value,words[i-1].Value,StringComparison.OrdinalIgnoreCase)))
            {
                for(int j=i-1;j>=Math.Max(0,i-settings.RepeatDistance);j--)
                {
                    bool exact=string.Equals(words[i].Value,words[j].Value,StringComparison.OrdinalIgnoreCase);
                    bool family=!exact && words[i].Length>=4 && words[j].Length>=4 && Stem(words[i].Value,german)==Stem(words[j].Value,german);
                    if(!exact && !family)continue;
                    if(j!=i-1 && (stop.Contains(words[i].Value) || stop.Contains(words[j].Value)))continue;
                    if(settings.PersonalWords.Contains(words[i].Value,StringComparer.OrdinalIgnoreCase))continue;
                    issues.Add(Issue(text,words[i],"repeat","Wiederholung",family?"Mögliche gleiche Wortfamilie wie „"+words[j].Value+"“. Wortstammerkennung ist eine Näherung.":"„"+words[i].Value+"“ steht innerhalb von "+(i-j)+" Wortpositionen erneut. Bewusst wiederholt?"));break;
                }
            }
        }
        if(german)
        {
            Pattern(issues,text,settings,"filler",@"\b(eigentlich|gewissermaßen|sozusagen|quasi|irgendwie|letztendlich)\b","Mögliches Füllwort. Prüfe, ob die Aussage ohne dieses Wort gleich bleibt.","");
            Pattern(issues,text,settings,"wordy",@"\bim Grunde genommen\b","Kürzer formulieren?","im Grunde","");
            Pattern(issues,text,settings,"because",@"\baufgrund der Tatsache,? dass\b","Diese Wendung lässt sich oft kürzen.","weil");
            Pattern(issues,text,settings,"redundancy",@"\b(weißer Schimmel|runde Kugel|persönliche Meinung|zukünftige Planung)\b","Möglicherweise doppelte Information. Prüfe den Kontext.");
            Pattern(issues,text,settings,"passive",@"\b(wird|werden|wurde|wurden)\b[^.!?\r\n]{0,90}\b(?:ge\p{L}+(?:t|en)|\p{L}+iert)\b","Mögliche Passivkonstruktion. Ist eine aktive Form mit handelnder Person klarer?");
            Pattern(issues,text,settings,"nominal",@"\b(?:Durchführung|Umsetzung|Bearbeitung|Überprüfung|Bereitstellung|Inanspruchnahme)\b","Möglicher Nominalstil. Prüfe, ob ein Verb die Handlung direkter ausdrückt.");
        }
        else
        {
            Pattern(issues,text,settings,"filler",@"\b(basically|actually|literally|very|really|somewhat)\b","Possible filler or intensifier. Does it add useful meaning?","");
            Pattern(issues,text,settings,"wordy",@"\bin order to\b","A shorter expression may be enough.","to");
            Pattern(issues,text,settings,"because",@"\bdue to the fact that\b","A shorter expression may be enough.","because");
            Pattern(issues,text,settings,"redundancy",@"\b(free gift|past history|future plans|completely unanimous)\b","Possibly redundant. Check whether both words add meaning.");
            Pattern(issues,text,settings,"passive",@"\b(is|are|was|were|been|being)\s+(?:\p{L}+ly\s+)?(?:\p{L}+ed|written|given|taken|made|seen|known|found)\b","Possible passive voice. Would naming the person acting make the sentence clearer?");
            Pattern(issues,text,settings,"nominal",@"\b(?:implementation|utilization|provision|consideration|examination)\b","Possible nominalization. Consider expressing the action with a verb.");
        }
        if(settings.Enabled("sentence"))foreach(Match sentence in Regex.Matches(text,@"[^.!?\r\n]+[.!?]?"))
        { int count=Words.Matches(sentence.Value).Count;if(count>settings.LongSentence)issues.Add(Issue(text,sentence,"sentence","Lesbarkeit",count+" Wörter in diesem Satz. Lässt er sich sinnvoll teilen?")); }
        if(settings.Enabled("paragraph"))foreach(Match paragraph in Regex.Matches(text,@"[^\r\n\u2028\u2029]+"))
        { int count=Words.Matches(paragraph.Value).Count;if(count>settings.LongParagraph)issues.Add(Issue(text,paragraph,"paragraph","Lesbarkeit",count+" Wörter in diesem Absatz. Ein zusätzlicher Absatz könnte helfen.")); }
        Pattern(issues,text,settings,"spaces",@"(?<=\S) {2,}(?=\S)","Mehrere Leerzeichen zwischen Wörtern."," ");
        if(settings.Enabled("quotes"))foreach(Match quote in Regex.Matches(text,"\"([^\"\r\n]+)\""))
            issues.Add(Issue(text,quote,"quotes","Einheitlichkeit","Typografische Anführungszeichen verwenden?",(german?"„":"“")+quote.Groups[1].Value+(german?"“":"”")));
        if(settings.Enabled("terms"))foreach(string line in (settings.Terms??"").Split('\n'))
        {
            var pair=line.Trim().Split(new[]{"=>"},StringSplitOptions.None);if(pair.Length!=2 || pair[0].Length==0 || pair[1].Length==0 || pair[0]==pair[1])continue;
            foreach(Match match in Regex.Matches(text,@"(?<!\p{L})"+Regex.Escape(pair[0])+@"(?!\p{L})"))issues.Add(Issue(text,match,"terms","Einheitlichkeit","Bevorzugte Schreibweise: „"+pair[1]+"“.",pair[1]));
        }
        return issues.OrderBy(x=>x.Start).ToList();
    }
    public static string Statistics(string text)
    {
        var words=Words.Matches(text).Cast<Match>().ToArray();
        if(words.Length==0)return "0 Wörter · 0 Zeichen";
        int sentences=Math.Max(1,Regex.Matches(text,@"[^.!?\r\n]*\p{L}[^.!?\r\n]*[.!?]?").Count);
        double average=(double)words.Length/sentences;
        double lix=average+100.0*words.Count(x=>x.Length>6)/words.Length;
        int paragraphs=Regex.Split(text,@"\r\n|[\r\n\u2028\u2029]").Count(x=>Words.IsMatch(x));
        return words.Length+" Wörter · "+paragraphs+" Absätze · "+text.Length+" Zeichen · Ø "+average.ToString("0.0",CultureInfo.GetCultureInfo("de-DE"))+" Wörter/Satz · LIX "+lix.ToString("0",CultureInfo.InvariantCulture)+" (grobe Orientierung)";
    }
}
