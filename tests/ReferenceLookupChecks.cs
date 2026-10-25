using System;
using System.IO;
using System.Linq;
using System.Threading;

internal static class ReferenceLookupChecks
{
    static int count;
    static void Assert(bool condition,string name){if(!condition)throw new Exception(name);count++;}
    public static int Main(string[] args)
    {
        try{Run();if(args.Contains("--online"))Online();Console.WriteLine(count+" Link- und Quellenbibliotheksprüfungen bestanden.");return 0;}catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
    }
    static void Run()
    {
        Assert(ReferenceLookup.Doi("https://doi.org/10.1234/TEST")=="10.1234/test","DOI-Link erkannt");Assert(ReferenceLookup.Doi("doi:10.1234/test")=="10.1234/test","DOI-Präfix erkannt");Assert(ReferenceLookup.Doi("https://example.org/article")=="","Normale URL bleibt URL");
        Assert(ReferenceLookup.Doi("https://doi.org/10.1234/TEST?utm_source=test#part")=="10.1234/test","DOI-Link ohne Tracking und Fragment");
        foreach(string address in new[]{"file:///C:/private.txt","javascript:alert(1)","http://127.0.0.1/test","http://192.168.1.2/","http://localhost/","https://user:pass@example.org/"}){bool rejected=false;try{ReferenceLookup.PublicUrl(address);}catch(FormatException){rejected=true;}Assert(rejected,"Adresse abweisen: "+address);}
        Assert(ReferenceLookup.PublicUrl("https://example.org/path").Host=="example.org","Internet-URL akzeptiert");
        var meta=ReferenceLookup.ParseHtml("<html><head><title>Fallback</title><meta content='Paper &amp; Data' name='citation_title'><meta name=\"citation_author\" content=\"Müller, Eva\"><meta name='citation_author' content='Smith, Jane'><meta name='citation_publication_date' content='2024/06/12'><meta name='citation_doi' content='10.1234/test'><meta name='citation_journal_title' content='Journal'><meta name='citation_volume' content='2'><meta name='citation_firstpage' content='10'><meta name='citation_lastpage' content='20'><link href='/canonical' rel='canonical'></head></html>",new Uri("https://example.org/article"));
        Assert(meta.Title=="Paper & Data" && meta.Authors.Count==2,"Citation-Metatags, Entitäten und Autoren");Assert(meta.Year=="2024" && meta.Pages=="10–20","Datum und Seiten");Assert(meta.Url=="https://example.org/canonical" && meta.Doi=="10.1234/test","Kanonische URL und DOI");
        var linked=ReferenceLookup.ParseHtml("<script type='application/ld+json'>{\"@graph\":[{\"@type\":\"Organization\",\"name\":\"Wrong title\"},{\"@type\":\"ScholarlyArticle\",\"headline\":\"Research title\",\"author\":[{\"@type\":\"Person\",\"name\":\"Jane Smith\"}],\"datePublished\":\"2023-02-01\",\"publisher\":{\"name\":\"Press\"},\"identifier\":{\"value\":\"10.1234/test\"}}]}</script>",new Uri("https://example.org/article"));
        Assert(linked.Title=="Research title" && linked.Authors.Single()=="Jane Smith","JSON-LD wählt Artikel statt Organisation");Assert(linked.Year=="2023" && linked.Publisher=="Press","JSON-LD Datum und Verlag");
        var fallback=ReferenceLookup.ParseHtml("<title>A simple &amp; useful page</title>",new Uri("https://example.org/page"));Assert(fallback.Title=="A simple & useful page" && fallback.Authors.Count==0 && fallback.Year=="","Fehlende Angaben nicht erfinden");
        var malformed=ReferenceLookup.ParseHtml("<script type='application/ld+json'>{bad json</script><meta property='og:title' content='Still works'>",new Uri("https://example.org/"));Assert(malformed.Title=="Still works","Ungültiges JSON-LD verhindert Fallback nicht");
        Assert(ReferenceLookup.ParseHtml("<meta name='citation_title' content='A > B'>",new Uri("https://example.org/")).Title=="A > B","Größerzeichen innerhalb von Metatags");
        Assert(ReferenceLookup.ParseHtml("<script type='application/ld+json'>{\"@type\":\"WebPage\",\"name\":\"Generic page\"}</script><script type='application/ld+json'>{\"@type\":\"ScholarlyArticle\",\"headline\":\"Actual article\"}</script>",new Uri("https://example.org/")).Title=="Actual article","Artikel hat Vorrang über mehrere JSON-LD-Blöcke");
        var csl=ReferenceLookup.ParseCsl("{\"title\":\"Title\",\"author\":[{\"family\":\"Doe\",\"given\":\"Jane\"}],\"issued\":{\"date-parts\":[[2024,2,3]]},\"publisher\":\"Press\",\"type\":\"article-journal\",\"DOI\":\"10.1234/test\"}");Assert(csl.Year=="2024" && csl.PublishedDate=="2024-02-03" && csl.Publisher=="Press","DOI-CSL vollständig lesen");
        string path=Path.Combine(Path.GetTempPath(),"byby-reference-checks-"+Guid.NewGuid().ToString("N"),"references.json");var store=new ReferenceStore(path);
        var first=store.Upsert(new Reference {Title="Test title",Doi="10.1234/TEST",Year="2024",Authors={"Doe, Jane"}});var id=first.Id;
        Assert(store.Load().Count==1,"Bibliothek dauerhaft gespeichert");
        var updated=new ReferenceStore(path).Upsert(new Reference {Title="Corrected title",Doi="https://doi.org/10.1234/test",Year="2024",Authors={"Doe, Jane"}});Assert(updated.Id==id && store.Load().Count==1,"Duplikat aktualisiert, ID bleibt stabil");Assert(File.Exists(path+".bak"),"Vorheriger Stand gesichert");
        var url=store.Upsert(new Reference {Title="Page",Url="https://example.org/page#part"});var url2=store.Upsert(new Reference {Title="Page revised",Url="https://example.org/page"});Assert(url.Id==url2.Id && store.Load().Count==2,"URL ohne Fragment dedupliziert");
        var upper=new Reference{Title="A",Url="https://example.org/Case"};var lower=new Reference{Title="B",Url="https://example.org/case"};upper.Identify();lower.Identify();Assert(upper.Id!=lower.Id && !ReferenceStore.Same(upper,lower),"Groß-/Kleinschreibung im URL-Pfad erhalten");
        var ris=ReferenceStore.Ris(store.Load());var parsed=ReferenceImport.Ris(ris);Assert(parsed.Count==2 && parsed[0].Title=="Corrected title","RIS-Austausch mit Referenzmanagern");
        var full=new Reference {Title="Full record",Publisher="Press",PublishedDate="2024-02-03",Accessed="2026-10-25",Kind="book"};var roundtrip=ReferenceImport.Ris(ReferenceStore.Ris(new[]{full})).Single();Assert(roundtrip.Publisher==full.Publisher && roundtrip.Accessed==full.Accessed && roundtrip.Kind=="book","RIS erhält Verlag, Abrufdatum und Typ");
        File.WriteAllText(path,"invalid json");bool failed=false;try{store.Upsert(new Reference{Title="Must not overwrite"});}catch(ArgumentException){failed=true;}catch(FormatException){failed=true;}Assert(failed && File.ReadAllText(path)=="invalid json","Beschädigte Bibliothek nicht überschreiben");
    }
    static void Online()
    {
        var result=ReferenceLookup.Fetch("10.1038/s41586-020-2649-2",CancellationToken.None).GetAwaiter().GetResult();
        Assert(!string.IsNullOrWhiteSpace(result.Title) && result.Authors.Count>0 && !string.IsNullOrWhiteSpace(result.Year),"Echter DOI-Abruf");
        Assert(ReferenceStore.CanonicalDoi(result.Doi)=="10.1038/s41586-020-2649-2","Echter Abruf gehört zur angefragten DOI");
        Console.WriteLine("Online-Probe: "+result.Title);
        var page=ReferenceLookup.Fetch("https://numpy.org/",CancellationToken.None).GetAwaiter().GetResult();Assert(page.Title.IndexOf("NumPy",StringComparison.OrdinalIgnoreCase)>=0,"Echter Webseiten-Abruf");Assert(page.Url.StartsWith("https://numpy.org/") && !string.IsNullOrWhiteSpace(page.Accessed),"Webseiten-URL und Abrufdatum gespeichert");Console.WriteLine("Webseiten-Probe: "+page.Title);
    }
}
