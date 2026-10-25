using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

internal sealed class DocumentImport
{
    public FlowDocument Document;
    public bool Converted;
    public string Note;
}

internal static class DocumentFiles
{
    public const string Filter="Alle unterstützten Texte|*.schreib;*.rtf;*.txt;*.md;*.markdown;*.html;*.htm;*.xhtml;*.docx;*.docm;*.dotx;*.odt;*.ott;*.log;*.csv;*.tsv;*.tex;*.json;*.xml|Word-Dateien (*.docx;*.docm;*.dotx)|*.docx;*.docm;*.dotx|HTML (*.html;*.htm;*.xhtml)|*.html;*.htm;*.xhtml|Text und Markdown|*.txt;*.md;*.markdown;*.log;*.csv;*.tsv;*.tex;*.json;*.xml|Rich Text / OpenDocument|*.rtf;*.odt;*.ott|Schreib-Dokument|*.schreib|Alle Dateien|*.*";
    static FlowDocument Empty(){return new FlowDocument {FontFamily=new FontFamily("Calibri"),FontSize=16,PagePadding=new Thickness(64),ColumnWidth=10000};}
    public static DocumentImport Read(string path)
    {
        string extension=Path.GetExtension(path).ToLowerInvariant();
        if(new[]{".docx",".docm",".dotx"}.Contains(extension))return new DocumentImport {Document=Docx(path),Converted=true,Note="Word importiert · Grundformatierung; Seitenlayout, Grafiken und Felder können abweichen"};
        if(extension==".odt" || extension==".ott")return new DocumentImport {Document=Odt(path),Converted=true,Note="OpenDocument importiert · Grundformatierung"};
        if(extension==".doc")throw new FormatException("Das alte binäre Word-Format DOC wird nicht direkt unterstützt. Bitte eine DOCX- oder RTF-Version der Datei verwenden. Die Originaldatei wurde nicht verändert.");
        if(extension==".pdf")throw new FormatException("PDF-Dateien sind derzeit kein unterstütztes Textformat. Bitte TXT, HTML, RTF oder DOCX verwenden.");
        string text=ReadText(path,new[]{".html",".htm",".xhtml"}.Contains(extension));
        if(new[]{".html",".htm",".xhtml"}.Contains(extension))return new DocumentImport {Document=Html(text),Converted=true,Note="HTML importiert · Grundformatierung, ohne Skripte oder externe Inhalte"};
        var doc=Empty();foreach(string line in text.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))doc.Blocks.Add(new Paragraph(new Run(line)) {Margin=new Thickness(0)});
        return new DocumentImport {Document=doc,Note="Textdatei · Speicherung als UTF-8"};
    }
    public static string ReadText(string path,bool html=false)
    {
        if(new FileInfo(path).Length>30000000)throw new FormatException("Diese Textdatei ist für die aktuelle Version zu groß (maximal 30 MB).");
        var bytes=File.ReadAllBytes(path);Encoding encoding=null;int skip=0;
        if(bytes.Length>=4 && bytes[0]==0xff && bytes[1]==0xfe && bytes[2]==0 && bytes[3]==0){encoding=Encoding.UTF32;skip=4;}
        else if(bytes.Length>=4 && bytes[0]==0 && bytes[1]==0 && bytes[2]==0xfe && bytes[3]==0xff){encoding=new UTF32Encoding(true,true);skip=4;}
        else if(bytes.Length>=3 && bytes[0]==0xef && bytes[1]==0xbb && bytes[2]==0xbf){encoding=Encoding.UTF8;skip=3;}
        else if(bytes.Length>=2 && bytes[0]==0xff && bytes[1]==0xfe){encoding=Encoding.Unicode;skip=2;}
        else if(bytes.Length>=2 && bytes[0]==0xfe && bytes[1]==0xff){encoding=Encoding.BigEndianUnicode;skip=2;}
        if(encoding==null && html)
        {
            string head=Encoding.ASCII.GetString(bytes,0,Math.Min(bytes.Length,8192));var charset=Regex.Match(head,"charset\\s*=\\s*['\"\\s]*([a-zA-Z0-9_-]+)",RegexOptions.IgnoreCase);
            if(charset.Success)try{encoding=Encoding.GetEncoding(charset.Groups[1].Value);}catch(ArgumentException){}
        }
        if(encoding==null && bytes.Length>=8)
        {
            int sample=Math.Min(bytes.Length,2048),odd=0,even=0;for(int i=0;i<sample;i++)if(bytes[i]==0){if(i%2==0)even++;else odd++;}
            if(odd>sample/4 && even<sample/20)encoding=Encoding.Unicode;else if(even>sample/4 && odd<sample/20)encoding=Encoding.BigEndianUnicode;
        }
        string text;
        if(encoding!=null)text=encoding.GetString(bytes,skip,bytes.Length-skip);
        else try{text=new UTF8Encoding(false,true).GetString(bytes);}catch(DecoderFallbackException){text=Encoding.GetEncoding(1252).GetString(bytes);}
        if(text.Any(c=>c=='\0') || text.Count(c=>char.IsControl(c) && c!='\r' && c!='\n' && c!='\t' && c!='\f')>Math.Max(2,text.Length/50))throw new FormatException("Die Datei enthält binäre Daten und kann nicht als Text geöffnet werden.");
        return text;
    }
    static XDocument Xml(ZipArchive zip,string name)
    {
        var entry=zip.GetEntry(name);if(entry==null)throw new FormatException("Dokumentbestandteil fehlt: "+name);if(entry.Length>30000000)throw new FormatException("Dokumentbestandteil zu groß.");
        using(var stream=entry.Open())using(var reader=XmlReader.Create(stream,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=30000000}))return XDocument.Load(reader);
    }
    static XElement Child(XElement parent,string name){return parent==null?null:parent.Elements().FirstOrDefault(x=>x.Name.LocalName==name);}
    static string Attr(XElement element,string name){return element==null?"":(string)element.Attributes().FirstOrDefault(x=>x.Name.LocalName==name)??"";}
    static bool On(XElement properties,string name){var element=Child(properties,name);return element!=null && !new[]{"0","false","off"}.Contains(Attr(element,"val"));}
    static void WordFormat(TextElement element,XElement properties)
    {
        if(properties==null)return;
        if(On(properties,"b"))element.FontWeight=FontWeights.Bold;if(On(properties,"i"))element.FontStyle=FontStyles.Italic;
        if(Child(properties,"u")!=null && Attr(Child(properties,"u"),"val")!="none")element.TextDecorationsForInline();
        double size;if(double.TryParse(Attr(Child(properties,"sz"),"val"),out size) && size>0 && size<300)element.FontSize=size*2/3;
        string font=Attr(Child(properties,"rFonts"),"ascii");if(font!="")element.FontFamily=new FontFamily(font);
        string color=Attr(Child(properties,"color"),"val");if(Regex.IsMatch(color,@"^[a-fA-F0-9]{6}$") && color!="000000" && !color.Equals("FFFFFF",StringComparison.OrdinalIgnoreCase))element.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString("#"+color));
    }
    static void TextDecorationsForInline(this TextElement element){var inline=element as Inline;if(inline!=null)inline.TextDecorations=TextDecorations.Underline;}
    static void WordInlines(XElement source,InlineCollection output)
    {
        foreach(var child in source.Elements())switch(child.Name.LocalName)
        {
            case "r":var span=new Span();WordFormat(span,Child(child,"rPr"));WordInlines(child,span.Inlines);output.Add(span);break;
            case "t":case "delText":output.Add(new Run(child.Value));break;
            case "tab":output.Add(new Run("\t"));break;case "br":case "cr":output.Add(new LineBreak());break;
            case "drawing":case "pict":var alt=child.Descendants().Select(x=>Attr(x,"descr")).FirstOrDefault(x=>x!="");output.Add(new Run(string.IsNullOrWhiteSpace(alt)?"[Bild]":"[Bild: "+alt+"]"));break;
            case "rPr":case "pPr":case "instrText":case "del":break;
            default:WordInlines(child,output);break;
        }
    }
    static Paragraph WordParagraph(XElement source,Dictionary<string,XElement> styles)
    {
        var paragraph=new Paragraph {Margin=new Thickness(0,0,0,8)};var properties=Child(source,"pPr");string style=Attr(Child(properties,"pStyle"),"val");
        XElement definition;if(styles.TryGetValue(style,out definition))WordFormat(paragraph,Child(definition,"rPr"));
        var heading=Regex.Match(style,@"(?:Heading|berschrift)([1-6])$",RegexOptions.IgnoreCase);if(heading.Success){paragraph.FontWeight=FontWeights.Bold;paragraph.FontSize=28-int.Parse(heading.Groups[1].Value)*2;paragraph.Margin=new Thickness(0,10,0,8);}
        switch(Attr(Child(properties,"jc"),"val")){case "center":paragraph.TextAlignment=TextAlignment.Center;break;case "right":paragraph.TextAlignment=TextAlignment.Right;break;case "both":paragraph.TextAlignment=TextAlignment.Justify;break;}
        if(Child(properties,"numPr")!=null)paragraph.Inlines.Add(new Run("• "));
        WordFormat(paragraph,Child(properties,"rPr"));WordInlines(source,paragraph.Inlines);return paragraph;
    }
    static void WordBlocks(XElement source,BlockCollection output,Dictionary<string,XElement> styles)
    {
        foreach(var child in source.Elements())
        {
            if(child.Name.LocalName=="p")output.Add(WordParagraph(child,styles));
            else if(child.Name.LocalName=="tbl")
            {
                foreach(var row in child.Elements().Where(x=>x.Name.LocalName=="tr"))
                {
                    var paragraph=new Paragraph {Margin=new Thickness(0,0,0,4)};bool first=true;
                    foreach(var cell in row.Elements().Where(x=>x.Name.LocalName=="tc")){if(!first)paragraph.Inlines.Add(new Run("   |   "));first=false;WordInlines(cell,paragraph.Inlines);}output.Add(paragraph);
                }
            }
            else if(child.Name.LocalName!="sectPr")WordBlocks(child,output,styles);
        }
    }
    public static FlowDocument Docx(string path)
    {
        using(var zip=ZipFile.OpenRead(path))
        {
            var doc=Empty();var styles=new Dictionary<string,XElement>();if(zip.GetEntry("word/styles.xml")!=null)foreach(var style in Xml(zip,"word/styles.xml").Descendants().Where(x=>x.Name.LocalName=="style")){string id=Attr(style,"styleId");if(id!="")styles[id]=style;}
            var body=Xml(zip,"word/document.xml").Descendants().FirstOrDefault(x=>x.Name.LocalName=="body");if(body==null)throw new FormatException("Word-Datei enthält keinen Dokumenttext.");WordBlocks(body,doc.Blocks,styles);
            foreach(string name in new[]{"word/footnotes.xml","word/endnotes.xml"})if(zip.GetEntry(name)!=null)
            {
                var notes=Xml(zip,name).Root.Elements().Where(x=>Attr(x,"id")!="-1" && Attr(x,"id")!="0").ToList();if(notes.Count==0)continue;doc.Blocks.Add(new Paragraph(new Run(name.Contains("footnotes")?"Fußnoten":"Endnoten")){FontWeight=FontWeights.Bold});foreach(var note in notes)WordBlocks(note,doc.Blocks,styles);
            }
            if(doc.Blocks.Count==0)doc.Blocks.Add(new Paragraph());return doc;
        }
    }
    public static FlowDocument Odt(string path)
    {
        using(var zip=ZipFile.OpenRead(path))
        {
            var source=Xml(zip,"content.xml");var doc=Empty();var body=source.Descendants().FirstOrDefault(x=>x.Name.LocalName=="text" && x.Name.NamespaceName.Contains("office"));if(body==null)throw new FormatException("OpenDocument enthält keinen Text.");
            foreach(var p in body.Descendants().Where(x=>x.Name.LocalName=="p" || x.Name.LocalName=="h"))
            {
                var paragraph=new Paragraph();if(p.Name.LocalName=="h"){paragraph.FontSize=24;paragraph.FontWeight=FontWeights.Bold;}OdtInlines(p,paragraph.Inlines);doc.Blocks.Add(paragraph);
            }
            if(doc.Blocks.Count==0)doc.Blocks.Add(new Paragraph());return doc;
        }
    }
    static void OdtInlines(XElement source,InlineCollection output)
    {
        foreach(var node in source.Nodes())
        {
            var text=node as XText;if(text!=null){output.Add(new Run(text.Value));continue;}var element=node as XElement;if(element==null)continue;
            if(element.Name.LocalName=="tab")output.Add(new Run("\t"));else if(element.Name.LocalName=="line-break")output.Add(new LineBreak());else if(element.Name.LocalName=="s"){int count;if(!int.TryParse(Attr(element,"c"),out count))count=1;output.Add(new Run(new string(' ',Math.Max(1,Math.Min(1000,count)))));}else OdtInlines(element,output);
        }
    }
    public static FlowDocument Html(string html)
    {
        html=Regex.Replace(html,@"<!--[\s\S]*?-->","");html=Regex.Replace(html,@"<!DOCTYPE(?:[^>""']|""[^""]*""|'[^']*')*>","",RegexOptions.IgnoreCase);html=Regex.Replace(html,@"<\?[\s\S]*?\?>","");html=Regex.Replace(html,@"<(script|style|head|noscript|template)\b[^>]*>[\s\S]*?</\1\s*>","",RegexOptions.IgnoreCase);
        var doc=Empty();Paragraph paragraph=null;int bold=0,italic=0,underline=0,pre=0;bool space=false;
        Action newParagraph=delegate{paragraph=null;space=false;};
        foreach(Match token in Regex.Matches(html,@"<(?<close>/)?(?<name>[a-zA-Z][a-zA-Z0-9:-]*)\b(?:[^>""']|""[^""]*""|'[^']*')*>|(?<text>[^<]+)",RegexOptions.Singleline))
        {
            if(token.Groups["text"].Success)
            {
                string text=WebUtility.HtmlDecode(token.Value);if(pre==0){text=Regex.Replace(text,@"\s+"," ");if(paragraph==null || space)text=text.TrimStart(' ');}if(text.Length==0 || paragraph==null && string.IsNullOrWhiteSpace(text))continue;
                if(paragraph==null){paragraph=new Paragraph {Margin=new Thickness(0,0,0,8)};doc.Blocks.Add(paragraph);}var run=new Run(text);if(bold>0)run.FontWeight=FontWeights.Bold;if(italic>0)run.FontStyle=FontStyles.Italic;if(underline>0)run.TextDecorations=TextDecorations.Underline;if(pre>0)run.FontFamily=new FontFamily("Consolas");paragraph.Inlines.Add(run);space=text.EndsWith(" ");continue;
            }
            string name=token.Groups["name"].Value.ToLowerInvariant();bool close=token.Groups["close"].Success;
            if(name=="b" || name=="strong")bold=Math.Max(0,bold+(close?-1:1));else if(name=="i" || name=="em")italic=Math.Max(0,italic+(close?-1:1));else if(name=="u" || name=="a")underline=Math.Max(0,underline+(close?-1:1));
            else if(name=="br"){if(paragraph==null){paragraph=new Paragraph();doc.Blocks.Add(paragraph);}paragraph.Inlines.Add(new LineBreak());space=false;}
            else if(name=="pre"){pre=close?0:1;newParagraph();}
            else if(Regex.IsMatch(name,@"^h[1-6]$")){newParagraph();if(!close){paragraph=new Paragraph {FontWeight=FontWeights.Bold,FontSize=30-(name[1]-'0')*2,Margin=new Thickness(0,12,0,8)};doc.Blocks.Add(paragraph);}}
            else if(name=="li"){newParagraph();if(!close){paragraph=new Paragraph {Margin=new Thickness(16,0,0,4)};paragraph.Inlines.Add(new Run("• "));doc.Blocks.Add(paragraph);}}
            else if(new[]{"p","div","section","article","tr","ul","ol","blockquote","hr"}.Contains(name))newParagraph();
            else if(name=="td" || name=="th"){if(!close && paragraph!=null && paragraph.Inlines.Count>0)paragraph.Inlines.Add(new Run("   |   "));}
            else if(name=="img" && !close){var alt=Regex.Match(token.Value,"alt\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')",RegexOptions.IgnoreCase);if(alt.Success){if(paragraph==null){paragraph=new Paragraph();doc.Blocks.Add(paragraph);}paragraph.Inlines.Add(new Run("[Bild: "+WebUtility.HtmlDecode(alt.Groups[1].Success?alt.Groups[1].Value:alt.Groups[2].Value)+"]"));}}
        }
        if(doc.Blocks.Count==0)doc.Blocks.Add(new Paragraph());return doc;
    }
}
