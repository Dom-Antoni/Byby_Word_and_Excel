using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Documents;

internal static class DocumentChecks
{
    static int count;
    static void Assert(bool value,string name){if(!value)throw new Exception(name);count++;}
    [STAThread] public static int Main()
    {
        try{Run();Console.WriteLine(count+" Dokument- und Referenzprüfungen bestanden.");return 0;}
        catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
    }
    static void Run()
    {
        var ris=ReferenceImport.Ris("TY  - JOUR\nAU  - Müller, Eva\nTI  - Ein Titel\nPY  - 2024/01/01\nDO  - 10.1/test\nER  -\n");Assert(ris.Count==1 && ris[0].Year=="2024" && ris[0].Authors[0]=="Müller, Eva","RIS-Grunddaten");
        bool error=false;try{ReferenceImport.Ris("TY  - JOUR\nTI  - Unvollständig");}catch(FormatException){error=true;}Assert(error,"Abgeschnittene RIS-Datei ablehnen");
        var endnote=ReferenceImport.EndNoteXml("<xml><records><record><rec-number>42</rec-number><contributors><authors><author><style>Müller, Eva</style></author></authors></contributors><titles><title><style>Ein Titel</style></title><secondary-title>Journal</secondary-title></titles><dates><year>2024</year></dates></record></records></xml>");Assert(endnote.Count==1 && endnote[0].RecordNumber=="42" && endnote[0].EndNoteRecord,"EndNote-XML");
        error=false;try{ReferenceImport.EndNoteXml("<!DOCTYPE xml [<!ENTITY secret SYSTEM 'file:///not-read'>]><xml/>");}catch(System.Xml.XmlException){error=true;}Assert(error,"Keine externen XML-Entitäten");
        var csl=ReferenceImport.CslJson("[{\"id\":\"item1\",\"title\":\"English title\",\"author\":[{\"family\":\"Smith\",\"given\":\"Jane\"}],\"issued\":{\"date-parts\":[[2023,1,2]]}}]");Assert(csl.Count==1 && csl[0].Year=="2023" && csl[0].Author=="Smith","CSL-JSON");
        ris[0].Identify();var same=new Reference {Title="Other title",Doi="https://doi.org/10.1/test"};same.Identify();Assert(same.Id==ris[0].Id,"DOI dedupliziert");
        var source=endnote[0];source.Identify();
        var editor=new DocumentEditor();editor.Text="Erster Absatz.\r\nZweiter Absatz mit 😀 Emoji.";Assert(editor.Text=="Erster Absatz.\r\nZweiter Absatz mit 😀 Emoji.","RichText-Klartext");
        for(int i=0;i<editor.Text.Length;i++){if(char.IsSurrogate(editor.Text[i]) || editor.Text[i]=='\n' || editor.Text[i]=='\r')continue;editor.Select(i,1);Assert(editor.SelectedText==editor.Text.Substring(i,1),"Zeichenposition "+i);}
        editor.Select(editor.Text.IndexOf("😀",StringComparison.Ordinal),2);Assert(editor.SelectedText=="😀","Emoji vollständig auswählen");
        editor.Select(0,6);editor.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,FontWeights.Bold);Assert(editor.Text.StartsWith("Erster"),"Formatierung ändert keinen Text");
        editor.Select(editor.Text.Length,0);editor.InsertCitation(source,"("+source.Cite+")");var marks=editor.Citations();Assert(marks.Count==1,"Zitierverknüpfung vorhanden");Assert(editor.Text.Substring(marks[0].Start,marks[0].Length)=="("+source.Cite+")","Zitierposition korrekt");
        Assert(editor.ProofText.Length==editor.Text.Length && !editor.ProofText.Contains(source.Author),"Zitate aus Sprachprüfung ausgespart, Positionen erhalten");
        string rtf=editor.SaveRtf();var restored=new DocumentEditor();restored.LoadRtf(rtf);restored.RestoreCitations(marks,new[]{source});Assert(restored.Text==editor.Text,"RTF-Rundlauf");Assert(restored.Citations().Count==1,"Zitierverknüpfung nach Öffnen");
        var document=new WritingDocument {Rtf=rtf,References=new List<Reference>{source},Citations=marks};var serializer=new JavaScriptSerializer();var loaded=serializer.Deserialize<WritingDocument>(serializer.Serialize(document));Assert(loaded.References[0].Id==source.Id && loaded.Citations.Count==1,"Dokument mit Literatur serialisiert");
        var panel=new ReferencePanel(editor,new ReferenceStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"byby-empty-"+Guid.NewGuid().ToString("N"),"references.json")));panel.References.Add(source);Assert(panel.Used().Count==1,"Nur zitierte Quelle im Literaturverzeichnis");
        var export=new DocumentEditor();export.LoadRtf(panel.ExportRtf(false));Assert(export.Text.Contains("Literaturverzeichnis") && export.Text.Contains("Ein Titel"),"Literatur im RTF-Export");
        export.LoadRtf(panel.ExportRtf(true));Assert(export.Text.Contains("{Müller, 2024 #42}"),"EndNote-RTF mit Datensatznummer");
        panel.SetStyle(true);Assert(editor.Text.Contains("[1]") && editor.Citations().Count==1,"Stilwechsel erhält Verknüpfung");
        panel.Jump(source.Id);Assert(panel.ReturnPosition.HasValue,"Sprungrückkehr gespeichert");panel.Return();Assert(!panel.ReturnPosition.HasValue,"Sprungrückkehr verbraucht");
    }
}
