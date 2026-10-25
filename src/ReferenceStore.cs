using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

internal sealed class ReferenceStore
{
    public static string DefaultPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Byby_Word_and_Excel","references.json");}}
    public readonly string PathName;
    readonly string mutexName;
    sealed class Database {public int Version=1;public List<Reference> Items=new List<Reference>();}
    public ReferenceStore(string path=null)
    {
        PathName=Path.GetFullPath(path??DefaultPath);
        using(var sha=SHA256.Create())mutexName="Local\\BybyReferences"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(PathName.ToLowerInvariant()))).Replace("-","");
    }
    public List<Reference> Load()
    {
        if(!File.Exists(PathName))return new List<Reference>();
        var data=new JavaScriptSerializer {MaxJsonLength=30000000}.Deserialize<Database>(File.ReadAllText(PathName,Encoding.UTF8));
        if(data==null || data.Version!=1 || data.Items==null || data.Items.Any(x=>x==null || string.IsNullOrWhiteSpace(x.Id) || string.IsNullOrWhiteSpace(x.Title) || x.Authors==null))throw new FormatException("Quellenbibliothek ist ungültig. Die Datei wurde nicht verändert: "+PathName);
        return data.Items;
    }
    public static string CanonicalDoi(string doi)
    {
        string value=(doi??"").Trim();Uri url;if(Uri.TryCreate(value,UriKind.Absolute,out url) && new[]{"doi.org","dx.doi.org"}.Contains(url.Host.ToLowerInvariant()))return Uri.UnescapeDataString(url.AbsolutePath.TrimStart('/')).ToLowerInvariant();foreach(string prefix in new[]{"https://doi.org/","http://doi.org/","https://dx.doi.org/","http://dx.doi.org/","doi:"})if(value.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){value=value.Substring(prefix.Length);break;}return value.Trim().ToLowerInvariant();
    }
    public static string CanonicalUrl(string url)
    {
        Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri))return (url??"").Trim();
        var builder=new UriBuilder(uri){Fragment=""};return builder.Uri.AbsoluteUri.TrimEnd('/');
    }
    public static bool Same(Reference a,Reference b)
    {
        if(!string.IsNullOrWhiteSpace(a.Doi) && !string.IsNullOrWhiteSpace(b.Doi))return CanonicalDoi(a.Doi)==CanonicalDoi(b.Doi);
        if(!string.IsNullOrWhiteSpace(a.Url) && !string.IsNullOrWhiteSpace(b.Url) && CanonicalUrl(a.Url)==CanonicalUrl(b.Url))return true;
        return !string.IsNullOrWhiteSpace(a.Id) && a.Id==b.Id || string.Equals(a.Title,b.Title,StringComparison.OrdinalIgnoreCase) && a.Year==b.Year && string.Equals(string.Join(";",a.Authors),string.Join(";",b.Authors),StringComparison.OrdinalIgnoreCase);
    }
    public Reference Upsert(Reference reference)
    {
        if(string.IsNullOrWhiteSpace(reference.Title))throw new FormatException("Bitte einen Titel eintragen.");
        reference.Identify();
        using(var mutex=new Mutex(false,mutexName))
        {
            bool held=false;try
            {
                try{held=mutex.WaitOne(TimeSpan.FromSeconds(5));}catch(AbandonedMutexException){held=true;}
                if(!held)throw new IOException("Die Quellenbibliothek wird gerade gespeichert. Bitte erneut versuchen.");
                var items=Load();var existing=items.FirstOrDefault(x=>Same(x,reference));
                if(existing!=null)
                {
                    reference.Id=existing.Id;
                    if(!reference.EndNoteRecord && existing.EndNoteRecord){reference.EndNoteRecord=true;reference.RecordNumber=existing.RecordNumber;}
                    items[items.IndexOf(existing)]=reference;
                }
                else items.Add(reference);
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                string json=new JavaScriptSerializer {MaxJsonLength=30000000}.Serialize(new Database {Items=items});
                // Backup remains readable if a later file is damaged; a failed write preserves the original.
                if(File.Exists(PathName))File.Copy(PathName,PathName+".bak",true);
                Ui.AtomicWrite(PathName,delegate(string temp){File.WriteAllText(temp,json,new UTF8Encoding(false));});return reference;
            }
            finally{if(held)mutex.ReleaseMutex();}
        }
    }
    static string OneLine(string value){return (value??"").Replace("\r"," ").Replace("\n"," ");}
    public static string Ris(IEnumerable<Reference> references)
    {
        var text=new StringBuilder();foreach(var item in references)
        {
            text.AppendLine("TY  - "+(!string.IsNullOrWhiteSpace(item.Journal) || (item.Kind??"").IndexOf("article",StringComparison.OrdinalIgnoreCase)>=0?"JOUR":(item.Kind??"").IndexOf("book",StringComparison.OrdinalIgnoreCase)>=0?"BOOK":"ELEC"));
            text.AppendLine("TI  - "+OneLine(item.Title));foreach(string author in item.Authors)text.AppendLine("AU  - "+OneLine(author));
            foreach(var field in new[]{new[]{"PY",item.Year},new[]{"DA",item.PublishedDate},new[]{"JO",item.Journal},new[]{"PB",item.Publisher},new[]{"VL",item.Volume},new[]{"IS",item.Issue},new[]{"SP",item.Pages},new[]{"DO",item.Doi},new[]{"UR",item.Url},new[]{"Y2",item.Accessed}})if(!string.IsNullOrWhiteSpace(field[1]))text.AppendLine(field[0]+"  - "+OneLine(field[1]));
            text.AppendLine("ER  - ");text.AppendLine();
        }
        return text.ToString();
    }
}
