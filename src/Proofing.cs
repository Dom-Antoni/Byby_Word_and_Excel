using System;
using System.Collections.Generic;
using System.Linq;

// Presentation ranges never become document formatting or undo entries.
internal sealed class ProofMark
{
    public int Start,Length;
    public string Kind;
}

internal sealed class DismissedHint
{
    public int Start,Length;
    public string Rule,Original,Language;
    public DismissedHint Copy(){return (DismissedHint)MemberwiseClone();}
}

internal sealed class ProofingSession
{
    string text="";
    readonly List<DismissedHint> dismissed=new List<DismissedHint>();
    public List<DismissedHint> Snapshot(){return dismissed.Select(x=>x.Copy()).ToList();}
    public void Reset(string value,IEnumerable<DismissedHint> saved=null)
    {
        text=value??"";dismissed.Clear();
        if(saved!=null)foreach(var hint in saved)
            if(hint!=null && hint.Start>=0 && hint.Length>=0 && hint.Start<=text.Length-hint.Length && text.Substring(hint.Start,hint.Length)==hint.Original && !string.IsNullOrEmpty(hint.Rule))dismissed.Add(hint.Copy());
    }
    public void Update(string value)
    {
        value=value??"";if(value==text)return;
        int prefix=0;while(prefix<text.Length && prefix<value.Length && text[prefix]==value[prefix])prefix++;
        int suffix=0;while(suffix<text.Length-prefix && suffix<value.Length-prefix && text[text.Length-1-suffix]==value[value.Length-1-suffix])suffix++;
        int oldEnd=text.Length-suffix,delta=value.Length-text.Length;
        for(int i=dismissed.Count-1;i>=0;i--)
        {
            var hint=dismissed[i];
            if(hint.Length==0 && hint.Start>=prefix && hint.Start<=oldEnd){dismissed.RemoveAt(i);continue;}
            if(hint.Start>=oldEnd)hint.Start+=delta;
            else if(hint.Start+hint.Length>prefix){dismissed.RemoveAt(i);continue;}
            if(hint.Start<0 || hint.Start>value.Length-hint.Length || value.Substring(hint.Start,hint.Length)!=hint.Original)dismissed.RemoveAt(i);
        }
        text=value;
    }
    public void Ignore(DismissedHint hint)
    {
        if(!IsIgnored(hint.Rule,hint.Start,hint.Length,hint.Original,hint.Language))dismissed.Add(hint.Copy());
    }
    public bool IsIgnored(string rule,int start,int length,string original,string language)
    {return dismissed.Any(x=>x.Rule==rule && x.Start==start && x.Length==length && x.Original==original && x.Language==language);}
}
