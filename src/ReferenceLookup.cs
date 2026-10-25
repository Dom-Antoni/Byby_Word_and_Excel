using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal static class ReferenceLookup
{
    static ReferenceLookup(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;}
    public static string Doi(string input)
    {
        string value=ReferenceStore.CanonicalDoi(input);Uri link;
        if(Uri.TryCreate(input,UriKind.Absolute,out link) && new[]{"doi.org","dx.doi.org"}.Contains(link.Host.ToLowerInvariant()))value=Uri.UnescapeDataString(link.AbsolutePath.TrimStart('/')).ToLowerInvariant();
        return Regex.IsMatch(value,@"^10\.\d{4,9}/[^\s]+$",RegexOptions.IgnoreCase)?value:"";
    }
    public static Uri PublicUrl(string input)
    {
        Uri uri;if(!Uri.TryCreate(input,UriKind.Absolute,out uri) || uri.Scheme!="https" && uri.Scheme!="http" || !string.IsNullOrEmpty(uri.UserInfo))throw new FormatException("Bitte eine vollständige HTTP-/HTTPS-Adresse oder eine DOI eingeben.");
        IPAddress address;string host=uri.DnsSafeHost.TrimEnd('.').ToLowerInvariant();
        if(host=="localhost" || host.EndsWith(".localhost") || host.EndsWith(".local") || !host.Contains('.') && !IPAddress.TryParse(host,out address))throw new FormatException("Bitte einen öffentlichen Internet-Link verwenden.");
        if(IPAddress.TryParse(host,out address))
        {
            if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();var bytes=address.GetAddressBytes();
            if(IPAddress.IsLoopback(address) || bytes.Length==4 && (bytes[0]==10 || bytes[0]==127 || bytes[0]==0 || bytes[0]==169 && bytes[1]==254 || bytes[0]==172 && bytes[1]>=16 && bytes[1]<=31 || bytes[0]==192 && bytes[1]==168 || bytes[0]>=224) || bytes.Length==16 && (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || (bytes[0]&0xfe)==0xfc || address.Equals(IPAddress.IPv6Any)))throw new FormatException("Lokale Netzwerkadressen sind für den Linkimport nicht vorgesehen.");
        }
        return uri;
    }
    public static async Task<Reference> Fetch(string input,CancellationToken cancel)
    {
        using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancel))
        using(var client=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false,AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate}))
        {
            deadline.CancelAfter(TimeSpan.FromSeconds(35));client.DefaultRequestHeaders.UserAgent.ParseAdd("Schreib-ReferenceImporter/1.0");
            string doi=Doi(input);Reference reference;
            if(doi!="")reference=await FetchDoi(client,doi,deadline.Token);
            else
            {
                Uri uri=PublicUrl(input);var content=await Download(client,uri,"text/html, application/xhtml+xml, application/vnd.citationstyles.csl+json",deadline.Token);
                if(content.Type.Contains("json"))reference=ParseCsl(content.Text);
                else if(content.Type.Contains("html") || string.IsNullOrEmpty(content.Type))reference=ParseHtml(content.Text,content.Url);
                else throw new FormatException("Dieser Link liefert keine lesbare Webseite. Bitte die Artikelseite oder eine DOI verwenden; PDF-Dateien werden hier nicht heruntergeladen.");
                if(!string.IsNullOrWhiteSpace(reference.Doi))
                {
                    try{var registered=await FetchDoi(client,reference.Doi,deadline.Token);Fill(registered,reference);reference=registered;}
                    catch(HttpRequestException){}catch(FormatException){}
                }
                if(string.IsNullOrWhiteSpace(reference.Url))reference.Url=content.Url.AbsoluteUri;
            }
            reference.Accessed=DateTime.Now.ToString("yyyy-MM-dd");reference.Source=input;return reference;
        }
    }
    sealed class Content {public string Text,Type;public Uri Url;}
    static async Task<Content> Download(HttpClient client,Uri uri,string accept,CancellationToken cancel)
    {
        for(int redirect=0;redirect<=6;redirect++)
        {
            PublicUrl(uri.AbsoluteUri);
            using(var request=new HttpRequestMessage(HttpMethod.Get,uri))
            {
                request.Headers.TryAddWithoutValidation("Accept",accept);
                using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel))
                {
                    int code=(int)response.StatusCode;
                    if(code>=300 && code<400){if(response.Headers.Location==null)throw new HttpRequestException("Weiterleitung ohne Ziel.");uri=new Uri(uri,response.Headers.Location);continue;}
                    response.EnsureSuccessStatusCode();
                    string type=response.Content.Headers.ContentType==null?"":response.Content.Headers.ContentType.MediaType;
                    if(type.Contains("pdf"))throw new FormatException("Bitte die Artikelseite oder DOI statt eines direkten PDF-Links verwenden.");
                    if(response.Content.Headers.ContentLength>3000000)throw new FormatException("Die Seite ist für den Metadatenabruf zu groß.");
                    using(var stream=await response.Content.ReadAsStreamAsync())using(var buffer=new MemoryStream())
                    {
                        var bytes=new byte[8192];int read;while((read=await stream.ReadAsync(bytes,0,bytes.Length,cancel))>0){if(buffer.Length+read>3000000)throw new FormatException("Die Seite ist für den Metadatenabruf zu groß.");buffer.Write(bytes,0,read);}
                        Encoding encoding=Encoding.UTF8;try{if(response.Content.Headers.ContentType!=null && !string.IsNullOrWhiteSpace(response.Content.Headers.ContentType.CharSet))encoding=Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet.Trim('"'));}catch(ArgumentException){}
                        return new Content {Text=encoding.GetString(buffer.ToArray()),Type=type,Url=uri};
                    }
                }
            }
        }
        throw new HttpRequestException("Zu viele Weiterleitungen.");
    }
    static async Task<Reference> FetchDoi(HttpClient client,string doi,CancellationToken cancel)
    {
        doi=Doi(doi);if(doi=="")throw new FormatException("Ungültige DOI.");
        int slash=doi.IndexOf('/');var content=await Download(client,new Uri("https://doi.org/"+doi.Substring(0,slash)+"/"+Uri.EscapeDataString(doi.Substring(slash+1))),"application/vnd.citationstyles.csl+json",cancel);
        var reference=ParseCsl(content.Text);if(string.IsNullOrWhiteSpace(reference.Doi))reference.Doi=doi;
        if(string.IsNullOrWhiteSpace(reference.Url))reference.Url="https://doi.org/"+doi;
        reference.MetadataSource="DOI-Register (CSL-Metadaten)";return reference;
    }
    public static Reference ParseCsl(string json)
    {
        var serializer=new JavaScriptSerializer {MaxJsonLength=3000000};var raw=serializer.DeserializeObject(json) as Dictionary<string,object>;if(raw==null)throw new FormatException("Keine bibliografischen DOI-Daten gefunden.");
        var parsed=ReferenceImport.CslJson("["+json+"]").FirstOrDefault();if(parsed==null)throw new FormatException("Metadaten enthalten keinen Titel.");
        if(raw.ContainsKey("publisher"))parsed.Publisher=Convert.ToString(raw["publisher"]);
        if(raw.ContainsKey("type"))parsed.Kind=Convert.ToString(raw["type"]);
        if(raw.ContainsKey("issued")){var issued=raw["issued"] as Dictionary<string,object>;if(issued!=null && issued.ContainsKey("date-parts")){var dates=issued["date-parts"] as object[];if(dates!=null && dates.Length>0){var parts=dates[0] as object[];if(parts!=null)parsed.PublishedDate=string.Join("-",parts.Take(3).Select((x,i)=>Convert.ToInt32(x).ToString(i==0?"0000":"00")));}}}
        return parsed;
    }
    static string Clean(string value){return Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(value??"",@"<[^>]*>"," ")),@"\s+"," ").Trim();}
    static Dictionary<string,string> Attributes(string tag)
    {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(Match match in Regex.Matches(tag,"([\\w:-]+)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))"))result[match.Groups[1].Value]=WebUtility.HtmlDecode(match.Groups[2].Success?match.Groups[2].Value:match.Groups[3].Success?match.Groups[3].Value:match.Groups[4].Value);
        return result;
    }
    public static Reference ParseHtml(string html,Uri url)
    {
        var meta=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach(Match tag in Regex.Matches(html,@"<meta\b(?:[^>""']|""[^""]*""|'[^']*')*>",RegexOptions.IgnoreCase))
        {
            var attr=Attributes(tag.Value);string name=attr.ContainsKey("name")?attr["name"]:attr.ContainsKey("property")?attr["property"]:"";
            if(name=="" || !attr.ContainsKey("content"))continue;if(!meta.ContainsKey(name))meta[name]=new List<string>();meta[name].Add(Clean(attr["content"]));
        }
        Func<string,string> first=key=>meta.ContainsKey(key)?meta[key].FirstOrDefault():"";
        var reference=new Reference {Title=first("citation_title"),Year=Year(first("citation_publication_date")),PublishedDate=first("citation_publication_date"),Journal=first("citation_journal_title"),Volume=first("citation_volume"),Issue=first("citation_issue"),Pages=first("citation_firstpage"),Doi=first("citation_doi"),Publisher=first("citation_publisher"),Url=url.AbsoluteUri,Kind=string.IsNullOrWhiteSpace(first("citation_journal_title"))?"":"article-journal",MetadataSource="Webseiten-Metadaten"};
        if(!string.IsNullOrWhiteSpace(first("citation_lastpage")))reference.Pages+=(string.IsNullOrWhiteSpace(reference.Pages)?"":"–")+first("citation_lastpage");
        foreach(string key in new[]{"citation_author","dc.creator","dcterms.creator","author"})if(meta.ContainsKey(key)){reference.Authors=meta[key].Where(x=>x!="").Distinct().ToList();break;}
        var structured=new List<Dictionary<string,object>>();
        foreach(Match script in Regex.Matches(html,@"<script\b(?<attributes>[^>]*)>(?<json>[\s\S]*?)</script\s*>",RegexOptions.IgnoreCase))
        {
            var attr=Attributes(script.Groups["attributes"].Value);if(!attr.ContainsKey("type") || !attr["type"].Equals("application/ld+json",StringComparison.OrdinalIgnoreCase))continue;
            try
            {
                var json=new JavaScriptSerializer {MaxJsonLength=3000000}.DeserializeObject(script.Groups["json"].Value);
                structured.AddRange(Nodes(json).Where(x=>IsWork(x)));
            }
            catch(ArgumentException){}catch(InvalidOperationException){}catch(FormatException){}
        }
        var chosen=structured.FirstOrDefault(x=>Kind(x).IndexOf("article",StringComparison.OrdinalIgnoreCase)>=0)??structured.FirstOrDefault();if(chosen!=null)Fill(reference,JsonLd(chosen));
        if(string.IsNullOrWhiteSpace(reference.Title))reference.Title=first("og:title");
        if(string.IsNullOrWhiteSpace(reference.Title))reference.Title=Clean(Regex.Match(html,@"<title\b[^>]*>([\s\S]*?)</title>",RegexOptions.IgnoreCase).Groups[1].Value);
        if(string.IsNullOrWhiteSpace(reference.PublishedDate))reference.PublishedDate=first("article:published_time");
        if(string.IsNullOrWhiteSpace(reference.PublishedDate))reference.PublishedDate=first("dc.date");
        if(string.IsNullOrWhiteSpace(reference.Year))reference.Year=Year(reference.PublishedDate);
        if(string.IsNullOrWhiteSpace(reference.Publisher))reference.Publisher=first("og:site_name");
        if(string.IsNullOrWhiteSpace(reference.Doi))reference.Doi=Doi(first("dc.identifier"));
        foreach(Match tag in Regex.Matches(html,@"<link\b[^>]*>",RegexOptions.IgnoreCase)){var attrs=Attributes(tag.Value);Uri canonical;if(attrs.ContainsKey("rel") && attrs["rel"].Equals("canonical",StringComparison.OrdinalIgnoreCase) && attrs.ContainsKey("href") && Uri.TryCreate(url,attrs["href"],out canonical) && (canonical.Scheme=="https"||canonical.Scheme=="http")){reference.Url=canonical.AbsoluteUri;break;}}
        reference.Doi=Doi(reference.Doi);if(string.IsNullOrWhiteSpace(reference.Kind))reference.Kind="webpage";return reference;
    }
    static string Year(string value){return Regex.Match(value??"",@"\b(?:18|19|20|21)\d{2}\b").Value;}
    static string Get(Dictionary<string,object> item,string key){return item.ContainsKey(key)?Clean(Convert.ToString(item[key])):"";}
    static string Kind(Dictionary<string,object> item){object type;if(!item.TryGetValue("@type",out type))return "";var array=type as object[];return array==null?Convert.ToString(type):string.Join(" ",array.Select(Convert.ToString));}
    static bool IsWork(Dictionary<string,object> item){string type=Kind(item);return new[]{"Article","ScholarlyArticle","NewsArticle","BlogPosting","Book","WebPage","Report","CreativeWork"}.Any(x=>type.IndexOf(x,StringComparison.OrdinalIgnoreCase)>=0);}
    static IEnumerable<Dictionary<string,object>> Nodes(object value)
    {
        var array=value as object[];if(array!=null){foreach(var element in array)foreach(var node in Nodes(element))yield return node;yield break;}
        var item=value as Dictionary<string,object>;if(item==null)yield break;yield return item;
        if(item.ContainsKey("@graph"))foreach(var node in Nodes(item["@graph"]))yield return node;
    }
    static Reference JsonLd(Dictionary<string,object> item)
    {
        var result=new Reference {Title=Get(item,"headline"),PublishedDate=Get(item,"datePublished"),Kind=Kind(item)};if(result.Title=="")result.Title=Get(item,"name");result.Year=Year(result.PublishedDate);
        object authors;if(item.TryGetValue("author",out authors))foreach(var author in authors as object[]??new[]{authors}){var person=author as Dictionary<string,object>;string name=person==null?Clean(Convert.ToString(author)):Get(person,"name");if(person!=null && name=="")name=Get(person,"familyName")+(person.ContainsKey("givenName")?", "+Get(person,"givenName"):"");if(name!="")result.Authors.Add(name);}
        object publisher;if(item.TryGetValue("publisher",out publisher)){var org=publisher as Dictionary<string,object>;result.Publisher=org==null?Clean(Convert.ToString(publisher)):Get(org,"name");}
        object identifier;if(item.TryGetValue("identifier",out identifier)){var property=identifier as Dictionary<string,object>;result.Doi=Doi(property==null?Convert.ToString(identifier):Get(property,"value"));}
        return result;
    }
    static void Fill(Reference target,Reference fallback)
    {
        if(string.IsNullOrWhiteSpace(target.Title))target.Title=fallback.Title;if(target.Authors.Count==0)target.Authors=fallback.Authors;
        if(string.IsNullOrWhiteSpace(target.Year))target.Year=fallback.Year;if(string.IsNullOrWhiteSpace(target.PublishedDate))target.PublishedDate=fallback.PublishedDate;
        if(string.IsNullOrWhiteSpace(target.Journal))target.Journal=fallback.Journal;if(string.IsNullOrWhiteSpace(target.Publisher))target.Publisher=fallback.Publisher;
        if(string.IsNullOrWhiteSpace(target.Volume))target.Volume=fallback.Volume;if(string.IsNullOrWhiteSpace(target.Issue))target.Issue=fallback.Issue;if(string.IsNullOrWhiteSpace(target.Pages))target.Pages=fallback.Pages;
        if(string.IsNullOrWhiteSpace(target.Url))target.Url=fallback.Url;if(string.IsNullOrWhiteSpace(target.Doi))target.Doi=fallback.Doi;
        if(string.IsNullOrWhiteSpace(target.Kind))target.Kind=fallback.Kind;
    }
}
