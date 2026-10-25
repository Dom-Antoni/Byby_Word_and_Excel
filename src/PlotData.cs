using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

internal enum PlotKind { Columns, Bars, Line, Scatter, Histogram, Box }
internal enum PlotAggregate { Rows, Sum, Mean, Count }
internal sealed class PlotSource
{
    public object Identity;
    public string Name;
    public string[,] Cells;
    public bool Headings;
    public int[] SelectedRows;
    public int Start {get{return Headings?1:0;}}
    public string Column(int index){if(index<0)return "Zeilennummer";string value=Headings?Cells[0,index]:"";return string.IsNullOrWhiteSpace(value)?Sheet.Column(index):value.Replace("\r"," ").Replace("\n"," ");}
}
internal sealed class PlotOptions
{
    public PlotKind Kind=PlotKind.Columns;
    public PlotAggregate Aggregate=PlotAggregate.Sum;
    public int X=-1,Y=-1,Y2=-1,Bins=0,Limit=20;
    public bool SelectedOnly,SortValue,Dark,Zero;
    public string Title="",XLabel="",YLabel="",Numbers="Auto";
    public PlotOptions Copy(){return (PlotOptions)MemberwiseClone();}
}
internal sealed class PlotColumn {public int Index,Numeric,Dates,Text,Missing;public string Name;public bool IsNumber {get{return Numeric>0 && Numeric>=0.65*(Numeric+Dates+Text);}}public bool IsDate {get{return Dates>0 && Dates>=0.65*(Numeric+Dates+Text);}}}
internal sealed class PlotSuggestion {public string Label,Reason;public PlotOptions Options;}
internal sealed class PlotPoint {public double X,Y;public string Label;}
internal sealed class PlotSeries {public string Name;public List<PlotPoint> Points=new List<PlotPoint>();}
internal sealed class PlotBox {public string Label;public double Low,Q1,Median,Q3,High;public List<double> Outliers=new List<double>();}
internal sealed class PlotData
{
    public PlotKind Kind;
    public string Title,XLabel,YLabel,Notice;
    public bool NumericX,DateX,Dark,Zero;
    public double BinWidth;
    public List<string> Categories=new List<string>();
    public List<PlotSeries> Series=new List<PlotSeries>();
    public List<PlotBox> Boxes=new List<PlotBox>();
}

internal static class Plots
{
    public static readonly string[] KindNames={"Säulen","Balken","Linie","Punktdiagramm","Histogramm","Boxplot"};
    public static bool Number(string input,string mode,out double value)
    {
        value=0;if(string.IsNullOrWhiteSpace(input))return false;string text=input.Trim().Replace("\u2212","-").Replace("\u00a0","").Replace("\u202f","").Replace(" ","");
        text=Regex.Replace(text,@"^[€$£]|[€$£%]$","");if(text.StartsWith("(") && text.EndsWith(")"))text="-"+text.Substring(1,text.Length-2);
        if(mode=="Deutsch" || mode=="English")
        {
            if(!double.TryParse(text,NumberStyles.Float|NumberStyles.AllowThousands,CultureInfo.GetCultureInfo(mode=="Deutsch"?"de-DE":"en-US"),out value))return false;
        }
        else
        {
            int comma=text.LastIndexOf(','),dot=text.LastIndexOf('.');
            if(comma>=0 && dot>=0)text=comma>dot?text.Replace(".","").Replace(',','.'):text.Replace(",","");
            else if(comma>=0)text=text.Replace(',','.');
            if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value))return false;
        }
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
    public static bool Date(string text,out double value)
    {
        value=0;if(string.IsNullOrWhiteSpace(text) || !Regex.IsMatch(text.Trim(),@"^(?:\d{4}-\d{2}-\d{2}|\d{1,2}[./]\d{1,2}[./]\d{2,4})(?:$|[ T])"))return false;
        DateTime date;if(!DateTime.TryParse(text,CultureInfo.GetCultureInfo("de-DE"),DateTimeStyles.AllowWhiteSpaces,out date) && !DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out date))return false;
        try{value=date.ToOADate();return true;}catch(OverflowException){return false;}
    }
    static bool Value(PlotSource source,int row,int col,string mode,out double value)
    {
        if(col<0){value=1;return true;}string raw=source.Cells[row,col]??"";
        if(raw.StartsWith("=")){try{value=Sheet.Value(source.Cells,row,col,new HashSet<string>());return !double.IsNaN(value) && !double.IsInfinity(value);}catch{value=0;return false;}}
        return Number(raw,mode,out value);
    }
    public static List<PlotColumn> Profile(PlotSource source,string mode="Auto")
    {
        var result=new List<PlotColumn>();
        for(int col=0;col<source.Cells.GetLength(1);col++)
        {
            var profile=new PlotColumn {Index=col,Name=source.Column(col)};int examined=0;
            for(int row=source.Start;row<source.Cells.GetLength(0) && examined<300;row++,examined++)
            {
                string text=source.Cells[row,col];double value;
                if(string.IsNullOrWhiteSpace(text)){profile.Missing++;continue;}
                if(Value(source,row,col,mode,out value))profile.Numeric++;else if(Date(text,out value))profile.Dates++;else profile.Text++;
            }result.Add(profile);
        }return result;
    }
    public static List<PlotSuggestion> Suggest(PlotSource source,string mode="Auto")
    {
        var columns=Profile(source,mode);var numeric=columns.Where(x=>x.IsNumber).ToList();var date=columns.FirstOrDefault(x=>x.IsDate);var category=columns.FirstOrDefault(x=>!x.IsNumber && !x.IsDate && x.Text>0);
        var result=new List<PlotSuggestion>();
        if(date!=null && numeric.Count>0)result.Add(new PlotSuggestion {Label="Zeitverlauf",Reason="Datum und Zahlen erkannt; die X-Achse wird zeitlich sortiert.",Options=new PlotOptions {Kind=PlotKind.Line,X=date.Index,Y=numeric[0].Index}});
        if(category!=null && numeric.Count>0)result.Add(new PlotSuggestion {Label="Kategorien vergleichen",Reason="Textkategorien und Zahlen erkannt; gleiche Kategorien werden summiert.",Options=new PlotOptions {Kind=PlotKind.Columns,X=category.Index,Y=numeric[0].Index,Aggregate=PlotAggregate.Sum}});
        if(numeric.Count>=2)result.Add(new PlotSuggestion {Label="Zusammenhang ansehen",Reason="Zwei Zahlenspalten erkannt; jede gültige Zeile wird ein Punkt.",Options=new PlotOptions {Kind=PlotKind.Scatter,X=numeric[0].Index,Y=numeric[1].Index}});
        if(numeric.Count>0)
        {
            result.Add(new PlotSuggestion {Label="Verteilung ansehen",Reason="Häufigkeit der Zahlenwerte mit automatisch gewählten Klassen.",Options=new PlotOptions {Kind=PlotKind.Histogram,Y=numeric[0].Index}});
            result.Add(new PlotSuggestion {Label="Verteilungen vergleichen",Reason="Median, Quartile, Whisker und Ausreißer der Werte.",Options=new PlotOptions {Kind=PlotKind.Box,X=category==null?-1:category.Index,Y=numeric[0].Index}});
        }
        if(result.Count==0 && category!=null)result.Add(new PlotSuggestion {Label="Kategorien zählen",Reason="Keine Zahlenspalte nötig; gezählt werden Zeilen pro Kategorie.",Options=new PlotOptions {Kind=PlotKind.Bars,X=category.Index,Y=-1,Aggregate=PlotAggregate.Count}});
        foreach(var suggestion in result)suggestion.Options.Numbers=mode;
        return result;
    }
    static IEnumerable<int> Rows(PlotSource source,PlotOptions options)
    {
        if(options.SelectedOnly)
        {
            if(source.SelectedRows==null || source.SelectedRows.Length==0)throw new FormatException("Bitte zuerst Zellen oder Zeilen in der Tabelle markieren.");
            return source.SelectedRows.Distinct().Where(r=>r>=source.Start && r<source.Cells.GetLength(0)).OrderBy(r=>r);
        }
        return Enumerable.Range(source.Start,Math.Max(0,source.Cells.GetLength(0)-source.Start));
    }
    sealed class Sample {public int Row;public string Label;public double X;public double?[] Y;}
    public static PlotData Build(PlotSource source,PlotOptions options)
    {
        if(options.Y>=source.Cells.GetLength(1) || options.Y2>=source.Cells.GetLength(1) || options.X>=source.Cells.GetLength(1))throw new FormatException("Die gewählte Spalte ist in diesem Blatt nicht vorhanden.");
        bool histogram=options.Kind==PlotKind.Histogram,box=options.Kind==PlotKind.Box;
        if((histogram || box || options.Kind==PlotKind.Scatter || options.Kind==PlotKind.Line) && options.Y<0)throw new FormatException("Bitte eine Zahlenspalte als Y-Achse wählen.");
        if(options.Kind==PlotKind.Scatter && options.X<0)throw new FormatException("Für das Punktdiagramm bitte eine Zahlenspalte als X-Achse wählen.");
        var result=new PlotData {Kind=options.Kind,Dark=options.Dark,Zero=options.Zero};
        var profile=Profile(source,options.Numbers);
        result.DateX=!histogram && !box && options.X>=0 && profile[options.X].IsDate;
        result.NumericX=histogram || !box && (options.Kind==PlotKind.Scatter || options.Kind==PlotKind.Line && (options.X<0 || profile[options.X].IsNumber || result.DateX));
        bool counting=(options.Kind==PlotKind.Columns || options.Kind==PlotKind.Bars) && options.Aggregate==PlotAggregate.Count;
        var ys=new List<int>{options.Y};if(!histogram && !box && !counting && options.Y2>=0 && options.Y2!=options.Y)ys.Add(options.Y2);
        int skipped=0,total=0;var samples=new List<Sample>();
        foreach(int row in Rows(source,options))
        {
            total++;if(!Enumerable.Range(0,source.Cells.GetLength(1)).Any(c=>!string.IsNullOrWhiteSpace(source.Cells[row,c]))){skipped++;continue;}
            string label=options.X<0?(box?"Alle Werte":(row+1).ToString()):(source.Cells[row,options.X]??"").Trim();
            if(!histogram && label==""){skipped++;continue;}
            double x=row+1;
            if(!histogram && result.NumericX && options.X>=0)
            {
                bool ok=result.DateX?Date(label,out x):Value(source,row,options.X,options.Numbers,out x);if(!ok){skipped++;continue;}
            }
            var values=new double?[ys.Count];bool any=false;
            for(int n=0;n<ys.Count;n++){double y;if(Value(source,row,ys[n],options.Numbers,out y)){values[n]=y;any=true;}}
            if(options.Aggregate==PlotAggregate.Count && !histogram && !box && options.Kind!=PlotKind.Line && options.Kind!=PlotKind.Scatter){values[0]=1;any=true;}
            if(!any){skipped++;continue;}samples.Add(new Sample {Row=row,Label=label,X=x,Y=values});
        }
        if(samples.Count==0)throw new FormatException("Für diese Auswahl wurden keine gültigen Daten gefunden. Bitte Achsen, Zahlenformat und markierte Zeilen prüfen.");
        string yName=options.Y<0?"Anzahl":source.Column(options.Y);
        result.XLabel=options.X<0?"Zeilennummer":source.Column(options.X);result.YLabel=yName;
        if(histogram)
        {
            var values=samples.Select(x=>x.Y[0].Value).OrderBy(x=>x).ToArray();double min=values[0],max=values[values.Length-1];FiniteSpan(min,max);
            int bins=options.Bins==0?Math.Max(1,Math.Min(40,(int)Math.Ceiling(Math.Log(values.Length,2)+1))):Math.Max(1,Math.Min(100,options.Bins));
            if(min==max){bins=1;double spread=Math.Max(.5,Math.Abs(min)*.05);min-=spread;max+=spread;FiniteSpan(min,max);}
            result.BinWidth=(max-min)/bins;if(result.BinWidth<=0)throw new FormatException("Die Werte liegen für diese Histogrammklassen zu dicht beieinander.");var counts=new int[bins];
            foreach(double value in values){int index=Math.Min(bins-1,Math.Max(0,(int)((value-min)/result.BinWidth)));counts[index]++;}
            var series=new PlotSeries {Name="Häufigkeit"};for(int i=0;i<bins;i++)series.Points.Add(new PlotPoint {X=min+(i+0.5)*result.BinWidth,Y=counts[i],Label=Short(min+i*result.BinWidth)+" bis "+Short(min+(i+1)*result.BinWidth)});
            result.Series.Add(series);result.XLabel=yName;result.YLabel="Häufigkeit";result.Title="Verteilung: "+yName;
        }
        else if(box)
        {
            var groups=samples.GroupBy(x=>options.X<0?"Alle Werte":x.Label).ToList();int limit=Math.Max(1,options.Limit);
            foreach(var group in groups.Take(limit))
            {
                var values=group.Where(x=>x.Y[0].HasValue).Select(x=>x.Y[0].Value).OrderBy(x=>x).ToArray();if(values.Length==0)continue;
                FiniteSpan(values[0],values[values.Length-1]);double q1=Quantile(values,.25),median=Quantile(values,.5),q3=Quantile(values,.75),iqr=q3-q1;
                var outliers=values.Where(x=>x<q1-1.5*iqr || x>q3+1.5*iqr).ToList();
                if(outliers.Count>1000){var all=outliers;outliers=Enumerable.Range(0,1000).Select(i=>all[(int)((long)i*(all.Count-1)/999)]).ToList();result.Notice=(result.Notice??"")+"Bis zu 1000 Ausreißer je Gruppe gleichmäßig ausgewählt. ";}
                result.Boxes.Add(new PlotBox {Label=group.Key,Q1=q1,Median=median,Q3=q3,Low=values.First(x=>x>=q1-1.5*iqr),High=values.Last(x=>x<=q3+1.5*iqr),Outliers=outliers});
            }
            result.Categories=result.Boxes.Select(x=>x.Label).ToList();result.XLabel=options.X<0?"":source.Column(options.X);result.Title=yName+" nach "+(options.X<0?"Verteilung":source.Column(options.X));
            if(groups.Count>limit)result.Notice=(result.Notice??"")+"Nur die ersten "+limit+" von "+groups.Count+" Gruppen angezeigt. ";
        }
        else if(options.Kind==PlotKind.Columns || options.Kind==PlotKind.Bars)
        {
            var groups=options.Aggregate==PlotAggregate.Rows?samples.Select(x=>new List<Sample>{x}).ToList():samples.GroupBy(x=>x.Label).Select(x=>x.ToList()).ToList();
            if(options.SortValue)groups=groups.OrderByDescending(g=>Math.Abs(Aggregate(g,0,options.Aggregate)??0)).ToList();
            int limit=Math.Max(1,options.Limit);var shown=groups.Take(limit).ToList();result.Categories=shown.Select(g=>g[0].Label).ToList();
            for(int n=0;n<ys.Count;n++)
            {
                var series=new PlotSeries {Name=ys[n]<0?"Anzahl":source.Column(ys[n])};
                for(int g=0;g<shown.Count;g++){double? value=Aggregate(shown[g],n,options.Aggregate);if(value.HasValue)series.Points.Add(new PlotPoint {X=g,Y=value.Value,Label=shown[g][0].Label});}result.Series.Add(series);
            }
            result.NumericX=false;result.DateX=false;result.Title=yName+" nach "+result.XLabel;
            if(options.Aggregate!=PlotAggregate.Rows)result.YLabel=(options.Aggregate==PlotAggregate.Sum?"Summe":options.Aggregate==PlotAggregate.Mean?"Mittelwert":"Anzahl")+(options.Aggregate==PlotAggregate.Count?"":" · "+yName);
            if(groups.Count>limit)result.Notice=limit+" von "+groups.Count+" Kategorien angezeigt"+(options.SortValue?" (nach Betrag sortiert)":" (erste Kategorien)")+". ";
        }
        else
        {
            if(result.NumericX && options.Kind==PlotKind.Line)samples=samples.OrderBy(x=>x.X).ToList();
            if(!result.NumericX)result.Categories=samples.Select(x=>x.Label).ToList();
            int cap=options.Kind==PlotKind.Scatter?5000:2000;
            for(int n=0;n<ys.Count;n++)
            {
                var series=new PlotSeries {Name=source.Column(ys[n])};for(int i=0;i<samples.Count;i++)if(samples[i].Y[n].HasValue)series.Points.Add(new PlotPoint {X=result.NumericX?samples[i].X:i,Y=samples[i].Y[n].Value,Label=samples[i].Label});
                if(series.Points.Count>cap){var all=series.Points;series.Points=Enumerable.Range(0,cap).Select(i=>all[(int)((long)i*(all.Count-1)/(cap-1))]).ToList();result.Notice="Für die Darstellung und den Export werden bis zu "+cap+" Punkte je Reihe gleichmäßig ausgewählt. ";}result.Series.Add(series);
            }result.Title=yName+" nach "+result.XLabel;
        }
        if(ys.Count>1){string names=string.Join(" / ",ys.Select(c=>c<0?"Anzahl":source.Column(c)));result.YLabel=result.YLabel.Replace(yName,names);result.Title=names+" nach "+result.XLabel;}
        if(!string.IsNullOrWhiteSpace(options.Title))result.Title=options.Title.Trim();
        if(!string.IsNullOrWhiteSpace(options.XLabel))result.XLabel=options.XLabel.Trim();
        if(!string.IsNullOrWhiteSpace(options.YLabel))result.YLabel=options.YLabel.Trim();
        result.Notice=(result.Notice??"")+samples.Count+" von "+total+" Zeilen verwendet"+(skipped>0?"; "+skipped+" leer/ungültig übersprungen":"")+".";
        if(ys.Count>1)result.Notice+=" Beide Reihen teilen dieselbe Y-Skala; fehlende Werte werden je Reihe ausgelassen.";
        return result;
    }
    static double? Aggregate(List<Sample> samples,int series,PlotAggregate mode)
    {
        var values=samples.Where(x=>x.Y[series].HasValue).Select(x=>x.Y[series].Value).ToArray();if(values.Length==0)return null;
        if(mode==PlotAggregate.Count)return values.Length;if(mode==PlotAggregate.Mean){double scale=values.Max(v=>Math.Abs(v));return scale==0?0:scale*values.Average(v=>v/scale);}if(mode==PlotAggregate.Rows)return values[0];double sum=values.Sum();if(double.IsInfinity(sum))throw new FormatException("Die Summe ist zu groß für die Darstellung.");return sum;
    }
    public static double Quantile(double[] ordered,double fraction){double index=(ordered.Length-1)*fraction;int lower=(int)index;return ordered[lower]+(ordered[Math.Min(lower+1,ordered.Length-1)]-ordered[lower])*(index-lower);}
    public static void FiniteSpan(double min,double max){if(double.IsInfinity(min) || double.IsInfinity(max) || double.IsInfinity(max-min) || double.IsNaN(min) || double.IsNaN(max))throw new FormatException("Der Wertebereich ist zu groß für die Darstellung.");}
    public static string Short(double value){if(value==0)value=0;return value.ToString(Math.Abs(value)>=1000000 || value!=0 && Math.Abs(value)<.001?"0.##E+0":"0.###",CultureInfo.GetCultureInfo("de-DE"));}
}
