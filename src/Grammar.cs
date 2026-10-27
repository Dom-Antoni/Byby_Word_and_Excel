using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal sealed class GrammarResult
{
    public List<WritingIssue> Issues=new List<WritingIssue>();
    public string Status;
}

internal sealed class GrammarClient : IDisposable
{
    readonly string directory;
    readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
    readonly HttpClient client;
    Process server; int port; bool disposed,ready;
    public GrammarClient(string directory)
    {
        this.directory=directory;
        client=new HttpClient(new HttpClientHandler { UseProxy=false,AllowAutoRedirect=false });
        client.Timeout=TimeSpan.FromSeconds(35);
    }
    public async Task<GrammarResult> Check(string text,WritingSettings settings,CancellationToken cancel)
    {
        await gate.WaitAsync(cancel).ConfigureAwait(false);
        try
        {
            if(disposed)throw new ObjectDisposedException("GrammarClient");
            await Start(cancel).ConfigureAwait(false);
            var result=new GrammarResult();
            // Paragraph chunks retain full UTF-16 offsets and avoid the server's per-request limit.
            foreach(var chunk in Chunks(text,12000))
            {
                cancel.ThrowIfCancellationRequested();
                using(var body=new FormUrlEncodedContent(new Dictionary<string,string> { {"text",chunk.Value},{"language",settings.Language},{"enabledOnly","false"},{"level","picky"} }))
                using(var response=await client.PostAsync("http://127.0.0.1:"+port+"/v2/check",body,cancel).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    string json=await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    result.Issues.AddRange(Parse(json,text,chunk.Offset,settings));
                }
            }
            result.Status="Grammatik lokal geprüft · LanguageTool 6.6";return result;
        }
        finally { gate.Release(); }
    }
    internal sealed class Chunk { public int Offset; public string Value; }
    internal static IEnumerable<Chunk> Chunks(string text,int maximum)
    {
        for(int offset=0;offset<text.Length;)
        {
            int length=Math.Min(maximum,text.Length-offset);
            if(offset+length<text.Length)
            {
                int split=text.LastIndexOf('\n',offset+length-1,length);
                if(split<=offset+length/2)split=text.LastIndexOf(' ',offset+length-1,length);
                if(split>offset+length/2)length=split-offset+1;
                if(length>0 && char.IsHighSurrogate(text[offset+length-1]))length--;
            }
            yield return new Chunk {Offset=offset,Value=text.Substring(offset,length)};offset+=length;
        }
    }
    async Task Start(CancellationToken cancel)
    {
        if(server!=null && !server.HasExited && ready)return;
        string java=Path.Combine(directory,"runtime","bin","java.exe");
        string lt=Path.Combine(directory,"languagetool");
        if(!File.Exists(java) || !File.Exists(Path.Combine(lt,"languagetool-server.jar")))throw new FileNotFoundException("Offline-Grammatik fehlt. setup-offline.ps1 und build.ps1 ausführen oder das vollständige Offline-Paket verwenden.");
        if(server==null || server.HasExited)
        {
        ready=false;
        if(server!=null)server.Dispose();
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        server=new Process {StartInfo=new ProcessStartInfo {FileName=java,Arguments="-Xms128m -Xmx768m -cp languagetool-server.jar org.languagetool.server.HTTPServer --port "+port,WorkingDirectory=lt,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        // Drain logs in memory-free handlers; never store checked text in a log file.
        server.OutputDataReceived+=delegate{};server.ErrorDataReceived+=delegate{};
        server.Start();server.BeginOutputReadLine();server.BeginErrorReadLine();
        }
        for(int attempt=0;attempt<160;attempt++)
        {
            cancel.ThrowIfCancellationRequested();if(server.HasExited)throw new IOException("Die lokale Grammatikprüfung konnte nicht starten.");
            try
            {
                using(var response=await client.GetAsync("http://127.0.0.1:"+port+"/v2/languages",cancel).ConfigureAwait(false))if(response.IsSuccessStatusCode){ready=true;return;}
            }
            catch(HttpRequestException){}
            await Task.Delay(250,cancel).ConfigureAwait(false);
        }
        throw new TimeoutException("Die lokale Grammatikprüfung braucht zu lange zum Starten. Erneut prüfen.");
    }
    internal static List<WritingIssue> Parse(string json,string text,int offset,WritingSettings settings)
    {
        var root=new JavaScriptSerializer {MaxJsonLength=10000000}.DeserializeObject(json) as Dictionary<string,object>;
        var result=new List<WritingIssue>();if(root==null || !root.ContainsKey("matches"))throw new FormatException("Ungültige Antwort der lokalen Sprachprüfung.");
        foreach(var item in (object[])root["matches"])
        {
            var match=(Dictionary<string,object>)item;var rule=(Dictionary<string,object>)match["rule"];
            string id="lt:"+Convert.ToString(rule["id"]);if(!settings.Enabled(id))continue;
            int start=Convert.ToInt32(match["offset"])+offset,length=Convert.ToInt32(match["length"]);
            if(start<0 || length<0 || start+length>text.Length)continue;
            string original=text.Substring(start,length);string type=rule.ContainsKey("issueType")?Convert.ToString(rule["issueType"]):"";
            if(type=="misspelling" && settings.PersonalWords.Contains(original,StringComparer.OrdinalIgnoreCase))continue;
            var issue=new WritingIssue {Rule=id,Start=start,Length=length,Original=original,Category=type=="misspelling"?"Rechtschreibung":type=="style"?"Stil / Grammatik":"Grammatik",Message=Convert.ToString(match["message"])};
            foreach(var replacement in (object[])match["replacements"])issue.Replacements.Add(Convert.ToString(((Dictionary<string,object>)replacement)["value"]));
            result.Add(issue);
        }
        return result;
    }
    public void Dispose()
    {
        disposed=true;
        if(server!=null) { try {if(!server.HasExited)server.Kill();}catch(InvalidOperationException){}finally{server.Dispose();} }
        client.Dispose();
    }
}
