using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.Linq;

internal sealed class Reference
{
    public string Id,Title,Year,Journal,Volume,Issue,Pages,Doi,Url,RecordNumber,Source;
    public string Publisher,PublishedDate,Accessed,Kind,MetadataSource;
    public List<string> Authors=new List<string>();
    public bool EndNoteRecord;
    public string Author {get {string name=Authors.FirstOrDefault()??"Ohne Autor";if(name.Contains(","))return name.Split(',')[0].Trim();return name.Trim();}}
    public string Cite {get{return Author+(Authors.Count>1?" et al.":"")+", "+(string.IsNullOrEmpty(Year)?"o. J.":Year);}}
    public string Bibliography {get{return string.Join("; ",Authors)+" ("+(string.IsNullOrEmpty(Year)?"o. J.":Year)+"). "+Title+". "+Journal+ (string.IsNullOrEmpty(Volume)?"":" "+Volume)+(string.IsNullOrEmpty(Issue)?"":"("+Issue+")")+(string.IsNullOrEmpty(Pages)?"":", "+Pages)+(string.IsNullOrEmpty(Publisher)?"":". "+Publisher)+(string.IsNullOrEmpty(Doi)?"":". DOI: "+Doi)+(string.IsNullOrEmpty(Url)?"":". "+Url)+(string.IsNullOrEmpty(Accessed)?"":" (abgerufen "+Accessed+")");}}
    public void Identify()
    {
        string key=!string.IsNullOrWhiteSpace(Doi)?"doi:"+ReferenceStore.CanonicalDoi(Doi):!string.IsNullOrWhiteSpace(Url)?"url:"+ReferenceStore.CanonicalUrl(Url):"text:"+string.Join("|",new[]{Title,Year,string.Join(";",Authors)}).ToLowerInvariant();
        using(var sha=SHA256.Create())Id="r"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-","").Substring(0,20).ToLowerInvariant();
    }
    public override string ToString(){return Cite+" · "+Title;}
}

internal static class ReferenceImport
{
    public static List<Reference> Read(string path)
    {
        string text=File.ReadAllText(path,Encoding.UTF8);List<Reference> items;
        if(Path.GetExtension(path).Equals(".xml",StringComparison.OrdinalIgnoreCase))items=EndNoteXml(text);
        else if(Path.GetExtension(path).Equals(".json",StringComparison.OrdinalIgnoreCase))items=CslJson(text);
        else items=Ris(text);
        foreach(var item in items){item.Source=Path.GetFullPath(path);item.Identify();}
        if(items.Count==0)throw new FormatException("Keine Referenzen gefunden. Bitte EndNote-XML, RIS oder CSL-JSON exportieren; ENL-Datenbanken werden nicht direkt geöffnet.");
        return items.GroupBy(x=>x.Id).Select(x=>x.First()).ToList();
    }
    public static List<Reference> Ris(string text)
    {
        var items=new List<Reference>();Reference current=null;string previous=null;
        foreach(string raw in text.Replace("\r","").Split('\n'))
        {
            var match=Regex.Match(raw,@"^([A-Z0-9]{2})\s{2}-\s?(.*)$");
            if(!match.Success){if(current!=null && previous=="TI" && !string.IsNullOrWhiteSpace(raw))current.Title+=" "+raw.Trim();continue;}
            string tag=match.Groups[1].Value,value=match.Groups[2].Value.Trim();
            if(tag=="TY"){if(current!=null)throw new FormatException("RIS-Eintrag ohne ER-Abschluss.");current=new Reference {Kind=value=="JOUR"?"article-journal":value=="BOOK"?"book":"webpage"};}
            else if(tag=="ER"){if(current!=null){if(!string.IsNullOrWhiteSpace(current.Title))items.Add(current);current=null;}}
            else if(current!=null)switch(tag)
            {
                case "TI":case "T1":current.Title=value;break;
                case "AU":case "A1":current.Authors.Add(value);break;
                case "PY":case "Y1":current.Year=Regex.Match(value,@"\d{4}").Value;break;
                case "JO":case "JF":case "T2":current.Journal=value;break;
                case "VL":current.Volume=value;break;case "IS":current.Issue=value;break;
                case "SP":current.Pages=value;break;case "EP":current.Pages=(current.Pages??"")+"–"+value;break;
                case "DO":current.Doi=value;break;case "UR":current.Url=value;break;case "ID":current.RecordNumber=value;break;
                case "PB":current.Publisher=value;break;case "DA":current.PublishedDate=value;break;case "Y2":current.Accessed=value;break;
            }
            previous=tag;
        }
        if(current!=null)throw new FormatException("RIS-Datei endet vor dem ER-Abschluss.");return items;
    }
    static string Field(XElement element,string name){var child=element.Descendants().FirstOrDefault(x=>x.Name.LocalName==name);return child==null?"":child.Value;}
    public static List<Reference> EndNoteXml(string text)
    {
        XDocument doc;using(var input=new StringReader(text))using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=30000000}))doc=XDocument.Load(reader);
        return doc.Descendants().Where(x=>x.Name.LocalName=="record").Select(x=>new Reference {Title=Field(x,"title"),Year=Field(x,"year"),Journal=Field(x,"secondary-title"),Volume=Field(x,"volume"),Issue=Field(x,"number"),Pages=Field(x,"pages"),Doi=Field(x,"electronic-resource-num"),Url=Field(x,"url"),RecordNumber=Field(x,"rec-number"),EndNoteRecord=true,Authors=x.Descendants().Where(e=>e.Name.LocalName=="authors").Elements().Where(e=>e.Name.LocalName=="author").Select(e=>e.Value).ToList()}).Where(x=>!string.IsNullOrWhiteSpace(x.Title)).ToList();
    }
    static string Get(Dictionary<string,object> item,string key){return item.ContainsKey(key)?Convert.ToString(item[key],CultureInfo.InvariantCulture):"";}
    public static List<Reference> CslJson(string text)
    {
        var array=new JavaScriptSerializer{MaxJsonLength=30000000}.DeserializeObject(text) as object[];if(array==null)throw new FormatException("CSL-JSON muss eine Liste von Referenzen enthalten.");
        var result=new List<Reference>();
        foreach(var value in array)
        {
            var item=value as Dictionary<string,object>;if(item==null)continue;
            var reference=new Reference {Title=Get(item,"title"),Journal=Get(item,"container-title"),Volume=Get(item,"volume"),Issue=Get(item,"issue"),Pages=Get(item,"page"),Doi=Get(item,"DOI"),Url=Get(item,"URL"),RecordNumber=Get(item,"id"),Publisher=Get(item,"publisher"),Kind=Get(item,"type")};
            if(item.ContainsKey("author"))foreach(var author in (object[])item["author"]){var person=(Dictionary<string,object>)author;reference.Authors.Add(person.ContainsKey("literal")?Get(person,"literal"):Get(person,"family")+(person.ContainsKey("given")?", "+Get(person,"given"):""));}
            if(item.ContainsKey("issued")){var issued=(Dictionary<string,object>)item["issued"];if(issued.ContainsKey("date-parts")){var dates=(object[])issued["date-parts"];if(dates.Length>0){var parts=(object[])dates[0];if(parts.Length>0)reference.Year=Convert.ToString(parts[0],CultureInfo.InvariantCulture);}}}
            if(!string.IsNullOrWhiteSpace(reference.Title))result.Add(reference);
        }
        return result;
    }
}

internal sealed class CitationMark {public int Start,Length;public string ReferenceId;}
internal sealed class WritingDocument
{
    public int Version=1;
    public string Rtf;
    public DocumentMargins Margins;
    public List<Reference> References=new List<Reference>();
    public List<CitationMark> Citations=new List<CitationMark>();
    public List<DismissedHint> IgnoredHints=new List<DismissedHint>();
    public List<string> BibliographyOnly=new List<string>();
    public bool Numbered;
}
