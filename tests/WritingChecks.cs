using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static class WritingChecks
{
    static int count;
    static void Assert(bool condition,string name){if(!condition)throw new Exception(name);count++;}
    static bool Has(string text,WritingSettings settings,string rule){return Writing.Analyze(text,settings).Any(x=>x.Rule==rule);}
    public static void Main(string[] args)
    {
        var settings=new WritingSettings();
        settings.Terms="Email=>E-Mail";
        Assert(Has("Heute schreibe ich einen Text. Dieser Text bleibt.",settings,"repeat"),"Wiederholung");
        Assert(!Has("Das ist der Anfang und das ist der Schluss.",settings,"repeat"),"Funktionswörter ignorieren");
        Assert(Has("Das ist ist falsch.",settings,"repeat"),"Direkte Doppelung auch bei Funktionswörtern");
        Assert(Has("Wir schreiben heute. Gestern wurde geschrieben.",settings,"repeat"),"Bekannte Beugungen");
        settings.RepeatDistance=1;Assert(!Has("Der Text ist neu. Der Text bleibt.",settings,"repeat"),"Abstand respektiert");
        settings.RepeatDistance=7;settings.PersonalWords.Add("Text");Assert(!Has("Text und Text",settings,"repeat"),"Fachwort zulassen");settings.PersonalWords.Clear();
        Assert(Has("Das ist eigentlich klar.",settings,"filler"),"Füllwort Deutsch");
        settings.Disabled.Add("filler");Assert(!Has("eigentlich",settings,"filler"),"Regel deaktiviert");settings.Disabled.Clear();
        Assert(Has("Die Umsetzung wird geprüft.",settings,"passive"),"Passivhinweis Deutsch");
        Assert(Has("Die Umsetzung wird geprüft.",settings,"nominal"),"Nominalstil");
        Assert(Has("Das ist im Grunde genommen klar.",settings,"wordy"),"Umständliche Wendung");
        Assert(Has("ein weißer Schimmel",settings,"redundancy"),"Redundanz");
        Assert(Has("eine Email senden",settings,"terms"),"Bevorzugte Schreibweise");
        Assert(!Has("E-Mail",settings,"terms"),"Kanonische Schreibweise akzeptiert");
        var quote=Writing.Analyze("\"Hallo\"",settings).Single(x=>x.Rule=="quotes");Assert(quote.Replacements[0]=="„Hallo“","Deutsche Anführungszeichen");
        settings.Language="en-US";
        Assert(Has("This is basically fine.",settings,"filler"),"Füllwort Englisch");
        Assert(Has("This was written yesterday.",settings,"passive"),"Passivhinweis Englisch");
        Assert(Has("They write and have written.",settings,"repeat"),"Englische Beugung");
        Assert(Writing.Analyze("\"Hello\"",settings).Single(x=>x.Rule=="quotes").Replacements[0]=="“Hello”","Englische Anführungszeichen");
        Assert(Has("in order to finish",settings,"wordy"),"Englisch kürzen");
        settings.LongSentence=10;string longText=string.Join(" ",Enumerable.Repeat("word",12))+".";
        Assert(Has(longText,settings,"sentence"),"Langer Satz");
        settings.LongParagraph=30;Assert(Has(string.Join(" ",Enumerable.Repeat("word.",40)),settings,"paragraph"),"Langer Absatz");
        string shortParagraph=string.Join(" ",Enumerable.Repeat("word.",20));
        foreach(string newline in new[]{"\r\n","\n","\r","\u2029"})Assert(!Has(shortParagraph+newline+shortParagraph,settings,"paragraph"),"Enter trennt Absatzlängen: "+((int)newline[0]));
        string paragraphs=shortParagraph+"\r\n"+string.Join(" ",Enumerable.Repeat("word.",35));var longParagraph=Writing.Analyze(paragraphs,settings).Single(x=>x.Rule=="paragraph");
        Assert(longParagraph.Start==shortParagraph.Length+2 && longParagraph.Message.Contains("35 Wörter"),"Nur der tatsächlich lange Absatz markiert");
        Assert(Writing.Statistics("Erster Absatz.\r\nZweiter Absatz.").Contains("2 Absätze"),"Absatzzählung bei Enter");
        var review=new ProofingSession();review.Reset("eigentlich gut, eigentlich klar");
        review.Ignore(new DismissedHint {Rule="filler",Start=0,Length=10,Original="eigentlich",Language="de-DE"});
        Assert(review.IsIgnored("filler",0,10,"eigentlich","de-DE"),"Ignorierte Fundstelle bleibt bei erneuter Prüfung ignoriert");
        Assert(!review.IsIgnored("filler",16,10,"eigentlich","de-DE"),"Andere Fundstelle derselben Regel bleibt sichtbar");
        review.Update("Heute eigentlich gut, eigentlich klar");Assert(review.IsIgnored("filler",6,10,"eigentlich","de-DE"),"Ignorierte Stelle folgt einer Einfügung davor");
        review.Update("Heute eigentlich gut, eigentlich klar.");Assert(review.IsIgnored("filler",6,10,"eigentlich","de-DE"),"Änderung dahinter erhält Ignorieren");
        var saved=review.Snapshot();review.Reset("Heute eigentlich gut, eigentlich klar.",saved);Assert(review.IsIgnored("filler",6,10,"eigentlich","de-DE"),"Gespeicherte ignorierte Stelle wiederhergestellt");
        review.Update("Heute wirklich gut, eigentlich klar.");Assert(review.Snapshot().Count==0,"Geänderte Fundstelle gibt Ignorieren frei");
        review.Reset("eigentlich gut");Assert(review.Snapshot().Count==0,"Neues Dokument übernimmt keine fremden Ignorierungen");
        Assert(!Has("one  two",settings,"filler"),"Kein zufälliger Füllworttreffer");
        Assert(Has("one  two",settings,"spaces"),"Doppelte Leerzeichen");
        Assert(Writing.Statistics("A sentence. Another sentence.").Contains("4 Wörter"),"Wortzählung");
        var copy=settings.Copy();copy.Disabled.Add("new");Assert(!settings.Disabled.Contains("new"),"Einstellungen kopiert");
        string json="{\"matches\":[{\"offset\":3,\"length\":4,\"message\":\"Test\",\"rule\":{\"id\":\"SPELL\",\"issueType\":\"misspelling\"},\"replacements\":[{\"value\":\"word\"}]}]}";
        var parsed=GrammarClient.Parse(json,"😀 wrng",0,settings);Assert(parsed.Count==1 && parsed[0].Original=="wrng" && parsed[0].Fits("😀 wrng"),"UTF16-Position nach Emoji");
        Assert(!parsed[0].Fits("xxx😀 wrng"),"Veraltete Position abgewiesen");
        settings.PersonalWords.Add("wrng");Assert(GrammarClient.Parse(json,"😀 wrng",0,settings).Count==0,"Eigene Wörter in Grammatikfilter");settings.PersonalWords.Clear();
        settings.Disabled.Add("lt:SPELL");Assert(GrammarClient.Parse(json,"😀 wrng",0,settings).Count==0,"Grammatikregel abschalten");settings.Disabled.Clear();
        var chunks=GrammarClient.Chunks(new string('x',10)+"😀 "+new string('y',20),11).ToArray();Assert(string.Concat(chunks.Select(x=>x.Value))==new string('x',10)+"😀 "+new string('y',20),"Chunks ohne Textverlust");
        Assert(chunks.All(x=>x.Value.Length>0 && !char.IsHighSurrogate(x.Value.Last())),"Kein geteiltes Surrogat");
        var dictionary=new Synonyms(Path.Combine(Path.GetTempPath(),"missing-byby-dictionary"));
        dictionary.ReadGerman(new[]{"# Lizenz","schnell;rasch;flott (ugs.)"});Assert(dictionary.Find("schnell","de-DE")[0].Words.Contains("rasch"),"Deutsche Synonymgruppe");
        Assert(dictionary.Find("flott","de-DE").Count==1,"Register aus Suchschlüssel entfernt");
        dictionary.ReadEnglish(new[]{"00000001 00 a 02 fast 0 quick 0 000 | moving rapidly"});Assert(dictionary.Find("fast","en-US")[0].Words.Contains("quick"),"Englische Synonymgruppe");
        Assert(Synonyms.Replacement("flott (ugs.)")=="flott","Register nicht in Text übernehmen");
        if(args.Contains("--integration"))Integration().GetAwaiter().GetResult();
        Console.WriteLine(count+" Schreibprüfungen bestanden.");
    }
    static async Task Integration()
    {
        using(var client=new GrammarClient(AppDomain.CurrentDomain.BaseDirectory))
        {
            var de=new WritingSettings();string text="😀 Das ist ein gute Beispiel. Ich habe ein Fehler gemacht.";
            var result=await client.Check(text,de,CancellationToken.None);
            Assert(result.Issues.Any(x=>x.Category=="Grammatik"),"Echte deutsche Grammatikprüfung");
            Assert(result.Issues.All(x=>x.Fits(text)),"Alle echten UTF16-Positionen stimmen");
            Assert(result.Issues.Any(x=>x.Replacements.Count>0),"Echte Korrekturvorschläge");
            de.Language="en-US";string english="She go to school every day. This are wrong.";
            var en=await client.Check(english,de,CancellationToken.None);Assert(en.Issues.Any(x=>x.Category=="Grammatik"),"Echte englische Grammatikprüfung");
            var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{await client.Check("Cancelled",de,cancel.Token);}catch(OperationCanceledException){cancelled=true;}Assert(cancelled,"Abbruch unterstützt");
        }
        var synonyms=new Synonyms(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"synonyms"));
        Assert(synonyms.Find("schnell","de-DE").Any(x=>x.Words.Count>1),"Echter OpenThesaurus geladen");
        Assert(synonyms.Find("fast","en-US").Any(x=>x.Words.Count>1),"Echtes WordNet geladen");
    }
}
