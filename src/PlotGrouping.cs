using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

internal static class PlotPalette
{
    static readonly string[] Colors={"#0072B2","#D55E00","#009E73","#CC79A7","#B88700","#7654A3","#2B9BC2","#8C613C","#528B2D","#BE5078","#536FA3","#817D27"};
    public static string Color(int index)
    {
        if(index<Colors.Length)return Colors[Math.Max(0,index)];
        // Golden-angle hue spacing extends the qualitative palette without recycling it.
        double hue=(index*137.507764)%360,s=.62+(index%2)*.1,l=.43+(index%3)*.045;
        double chroma=(1-Math.Abs(2*l-1))*s,x=chroma*(1-Math.Abs(hue/60%2-1)),m=l-chroma/2;
        double r=0,g=0,b=0;if(hue<60){r=chroma;g=x;}else if(hue<120){r=x;g=chroma;}else if(hue<180){g=chroma;b=x;}else if(hue<240){g=x;b=chroma;}else if(hue<300){r=x;b=chroma;}else{r=chroma;b=x;}
        return "#"+((int)Math.Round((r+m)*255)).ToString("X2")+((int)Math.Round((g+m)*255)).ToString("X2")+((int)Math.Round((b+m)*255)).ToString("X2");
    }
}
internal static partial class Plots
{
    sealed class GroupPart {public string Name;public int Color;public PlotData Data;}
    static string GroupValue(PlotSource source,int row,int col){return (source.Cells[row,col]??"").Trim();}
    static PlotSource Subset(PlotSource source,int[] rows)
    {return new PlotSource {Identity=source.Identity,Name=source.Name,Cells=source.Cells,Headings=source.Headings,SelectedRows=rows};}
    static PlotData Frame(PlotData data)
    {return new PlotData {Kind=data.Kind,Title=data.Title,XLabel=data.XLabel,YLabel=data.YLabel,NumericX=data.NumericX,DateX=data.DateX,Dark=data.Dark,Zero=data.Zero,BinWidth=data.BinWidth};}
    static PlotData Grouped(PlotSource source,PlotOptions options)
    {
        if(options.Group>=source.Cells.GetLength(1))throw new FormatException("Die Gruppenspalte ist in diesem Blatt nicht vorhanden.");
        var rows=Rows(source,options).ToArray();
        var allNames=Enumerable.Range(source.Start,Math.Max(0,source.Cells.GetLength(0)-source.Start)).Select(r=>GroupValue(source,r,options.Group)).Where(s=>s!="").Distinct(StringComparer.Ordinal).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
        var colorIndices=allNames.Select((name,index)=>new {name,index}).ToDictionary(x=>x.name,x=>x.index,StringComparer.Ordinal);
        var candidates=rows.Where(r=>GroupValue(source,r,options.Group)!="").GroupBy(r=>GroupValue(source,r,options.Group),StringComparer.Ordinal).OrderBy(g=>g.Key,StringComparer.Ordinal).ToList();
        int groupLimit=Math.Max(1,Math.Min(24,options.GroupLimit));
        var displayed=candidates.Take(groupLimit).ToList();
        if(displayed.Count==0)throw new FormatException("Keine Gruppenangaben in der gewählten Gruppenspalte gefunden.");
        var inner=options.Copy();inner.Group=-1;inner.SelectedOnly=true;inner.Limit=Math.Max(100,source.Cells.GetLength(0));inner.SortValue=false;
        PlotData result;int used=0;
        if(options.Kind==PlotKind.Histogram)
        {
            var combined=Build(Subset(source,displayed.SelectMany(g=>g).ToArray()),inner);result=Frame(combined);
            var template=combined.Series[0].Points;double min=template[0].X-combined.BinWidth/2;
            foreach(var group in displayed)
            {
                var counts=new int[template.Count];int valid=0;
                foreach(int row in group){double number;if(!Value(source,row,options.Y,options.Numbers,out number))continue;int index=Math.Max(0,Math.Min(counts.Length-1,(int)((number-min)/combined.BinWidth)));counts[index]++;valid++;}
                if(valid==0)continue;used+=valid;var series=new PlotSeries {Name=group.Key,Group=group.Key,ColorIndex=colorIndices[group.Key]};
                for(int i=0;i<counts.Length;i++)series.Points.Add(new PlotPoint {X=template[i].X,Y=counts[i],Label=template[i].Label});
                result.Series.Add(series);result.Groups.Add(new PlotGroup {Name=group.Key,ColorIndex=colorIndices[group.Key]});
            }
            result.Measures.Add("Häufigkeit");
        }
        else
        {
            var parts=new List<GroupPart>();
            foreach(var group in displayed)
            {
                try{var data=Build(Subset(source,group.ToArray()),inner);used+=data.UsedRows;parts.Add(new GroupPart {Name=group.Key,Color=colorIndices[group.Key],Data=data});}
                catch(FormatException ex){if(!ex.Message.Contains("keine gültigen Daten"))throw;}
            }
            if(parts.Count==0)throw new FormatException("Keine gültigen Werte in den angezeigten Gruppen. Achsen, Gruppenlimit und Zahlenformat prüfen.");
            result=Frame(parts[0].Data);
            if(options.Kind==PlotKind.Box)result.Measures.Add(source.Column(options.Y));else result.Measures=parts[0].Data.Series.Select(s=>s.Name).ToList();
            foreach(var part in parts)result.Groups.Add(new PlotGroup {Name=part.Name,ColorIndex=part.Color});
            bool bars=options.Kind==PlotKind.Columns || options.Kind==PlotKind.Bars,box=options.Kind==PlotKind.Box;
            var categories=new List<string>();var labels=new Dictionary<string,string>(StringComparer.Ordinal);
            Func<PlotPoint,string> key=point=>bars && options.Aggregate==PlotAggregate.Rows?"row:"+point.Row.ToString(CultureInfo.InvariantCulture):point.Label;
            if(!result.NumericX)
            {
                foreach(var part in parts)
                {
                    foreach(var point in part.Data.Series.SelectMany(s=>s.Points)){string id=key(point);if(!labels.ContainsKey(id))labels[id]=point.Label;}
                    foreach(var item in part.Data.Boxes)if(!labels.ContainsKey(item.Label))labels[item.Label]=item.Label;
                }
                foreach(int row in rows)
                {
                    string id=bars && options.Aggregate==PlotAggregate.Rows?"row:"+row.ToString(CultureInfo.InvariantCulture):options.X<0?(box?"Alle Werte":(row+1).ToString()):(source.Cells[row,options.X]??"").Trim();
                    if(labels.ContainsKey(id) && !categories.Contains(id))categories.Add(id);
                }
                if(bars && options.SortValue)
                {
                    var magnitudes=parts.SelectMany(part=>part.Data.Series.Take(1)).SelectMany(s=>s.Points).GroupBy(key).ToDictionary(g=>g.Key,g=>g.Sum(p=>Math.Abs(p.Y)));
                    categories=categories.OrderByDescending(id=>magnitudes.ContainsKey(id)?magnitudes[id]:0).ToList();
                }
                int count=categories.Count;
                if(bars || box)categories=categories.Take(Math.Max(1,options.Limit)).ToList();
                if(categories.Count<count)result.Notice=categories.Count+" von "+count+" X-Kategorien angezeigt. ";
                result.Categories=categories.Select(id=>labels[id]).ToList();
            }
            var positions=categories.Select((id,index)=>new {id,index}).ToDictionary(x=>x.id,x=>x.index,StringComparer.Ordinal);
            for(int g=0;g<parts.Count;g++)
            {
                var part=parts[g];
                for(int m=0;m<part.Data.Series.Count;m++)
                {
                    var original=part.Data.Series[m];var series=new PlotSeries {Name=part.Name+" · "+original.Name,Group=part.Name,ColorIndex=part.Color,MeasureIndex=m};
                    foreach(var point in original.Points)
                    {
                        double x=point.X;if(!result.NumericX){int position;if(!positions.TryGetValue(key(point),out position))continue;x=position;}
                        series.Points.Add(new PlotPoint {X=x,Y=point.Y,Label=point.Label,Row=point.Row});
                    }result.Series.Add(series);
                }
                foreach(var item in part.Data.Boxes)
                {
                    int position;if(!positions.TryGetValue(item.Label,out position))continue;
                    item.CategoryIndex=position;item.GroupSlot=g;item.ColorIndex=part.Color;item.Group=part.Name;result.Boxes.Add(item);
                }
            }
            if(parts.Any(p=>(p.Data.Notice??"").Contains("gleichmäßig")))result.Notice=(result.Notice??"")+"Große Reihen/Ausreißermengen werden je Gruppe wie im Einzelplot gleichmäßig begrenzt. ";
        }
        result.GroupLabel=source.Column(options.Group);result.UsedRows=used;
        int noGroup=rows.Count(r=>GroupValue(source,r,options.Group)=="");
        result.Notice=(result.Notice??"")+"Farbe: "+result.GroupLabel+" · "+result.Groups.Count+" von "+candidates.Count+" Gruppen · "+used+" von "+rows.Length+" Zeilen verwendet.";
        if(candidates.Count>groupLimit)result.Notice+=" Erste "+groupLimit+" Gruppen alphabetisch gewählt; Gruppenlimit bis 24 einstellbar.";
        if(noGroup>0)result.Notice+=" "+noGroup+" Zeilen ohne Gruppenangabe ausgelassen.";
        if(result.Measures.Count>1)result.Notice+=(options.Kind==PlotKind.Columns || options.Kind==PlotKind.Bars?" Messreihe: kräftig = "+result.Measures[0]+", heller/gestrichelter Rahmen = "+result.Measures[1]:" Messreihe: durchgezogen/Kreis = "+result.Measures[0]+", gestrichelt/Quadrat = "+result.Measures[1])+". Gemeinsame Y-Skala.";
        if(options.Kind==PlotKind.Histogram)result.Notice+=" Alle Gruppen verwenden dieselben Histogrammklassen.";
        return result;
    }
}
