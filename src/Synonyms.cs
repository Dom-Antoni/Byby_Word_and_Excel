using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal sealed class SynonymGroup
{
    public string Explanation;
    public List<string> Words=new List<string>();
}

internal sealed class Synonyms
{
    readonly Dictionary<string,List<SynonymGroup>> german=new Dictionary<string,List<SynonymGroup>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,List<SynonymGroup>> english=new Dictionary<string,List<SynonymGroup>>(StringComparer.OrdinalIgnoreCase);
    public Synonyms(string directory)
    {
        var de=Path.Combine(directory,"openthesaurus.txt");
        if(File.Exists(de))ReadGerman(File.ReadLines(de));
        string dict=Path.Combine(directory,"dict");
        foreach(string part in new[]{"noun","verb","adj","adv"})
        { string path=Path.Combine(dict,"data."+part);if(File.Exists(path))ReadEnglish(File.ReadLines(path)); }
    }
    internal void ReadGerman(IEnumerable<string> lines)
    {
        foreach(string line in lines)
        {
            if(line.StartsWith("#") || string.IsNullOrWhiteSpace(line))continue;
            var group=new SynonymGroup {Explanation="OpenThesaurus · Bedeutung und Register im Kontext prüfen",Words=line.Split(';').Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Trim()).Distinct().ToList()};
            foreach(string word in group.Words)
            {
                string key=Regex.Replace(word,@"\s*\([^)]*\)","").Trim();
                if(!german.ContainsKey(key))german[key]=new List<SynonymGroup>();german[key].Add(group);
            }
        }
    }
    internal void ReadEnglish(IEnumerable<string> lines)
    {
        foreach(string line in lines)
        {
            if(line.Length==0 || !char.IsDigit(line[0]))continue;
            var halves=line.Split(new[]{'|'},2);var fields=halves[0].Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            int count;if(fields.Length<4 || !int.TryParse(fields[3],System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out count) || fields.Length<4+count*2)continue;
            var group=new SynonymGroup {Explanation=halves.Length>1?halves[1].Trim():"WordNet"};
            for(int i=0;i<count;i++)group.Words.Add(Regex.Replace(fields[4+i*2].Replace('_',' '),@"\([a-z]+\)$",""));
            foreach(string word in group.Words) {if(!english.ContainsKey(word))english[word]=new List<SynonymGroup>();english[word].Add(group);}
        }
    }
    public List<SynonymGroup> Find(string word,string language)
    {
        var index=language.StartsWith("de")?german:english;List<SynonymGroup> groups;
        if(index.TryGetValue(word,out groups))return groups;
        if(language.StartsWith("en"))foreach(string suffix in new[]{"s","es","ed","ing"})
        {if(word.EndsWith(suffix) && index.TryGetValue(word.Substring(0,word.Length-suffix.Length),out groups))return groups;}
        return new List<SynonymGroup>();
    }
    public static string Replacement(string word) { return Regex.Replace(word,@"\s*\([^)]*\)","").Trim(); }
}
