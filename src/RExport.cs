using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

internal sealed class RExportResult
{
    public string Directory,Project,Script,Message;
    public bool PlotObjectCreated;
}
internal static class RExport
{
    static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
    internal static string Q(string value)
    {
        var text=new StringBuilder("\"");foreach(char c in value??"")switch(c){case '\\':text.Append("\\\\");break;case '"':text.Append("\\\"");break;case '\r':text.Append("\\r");break;case '\n':text.Append("\\n");break;case '\t':text.Append("\\t");break;default:if(c<32)text.Append("\\u"+((int)c).ToString("x4"));else text.Append(c);break;}return text.Append('"').ToString();
    }
    static string N(double value){return value.ToString("R",Invariant);}
    static string Vector(IEnumerable<string> values){return "c("+string.Join(", ",values.Select(Q))+")";}
    static void Csv(string path,IEnumerable<string[]> rows)
    {using(var file=new StreamWriter(path,false,new UTF8Encoding(false)))foreach(var row in rows)file.WriteLine(string.Join(",",row.Select(v=>Sheet.Quote(v??""))));}
    static string Read(string name){return "read.csv(file.path(.byby_root, "+Q(name)+"), fileEncoding = 'UTF-8', check.names = FALSE, colClasses = 'character', na.strings = character(), strip.white = FALSE)";}
    static bool Number(PlotSource source,int r,int c,string mode,out double value)
    {
        string raw=source.Cells[r,c]??"";if(!raw.StartsWith("="))return Plots.Number(raw,mode,out value);
        try{value=Sheet.Value(source.Cells,r,c,new HashSet<string>());return !double.IsNaN(value) && !double.IsInfinity(value);}catch{value=0;return false;}
    }
    static string Data(PlotSource source,string directory,string mode)
    {
        int lastRow=source.Start-1,lastColumn=0;
        for(int r=0;r<source.Cells.GetLength(0);r++)for(int c=0;c<source.Cells.GetLength(1);c++)if(!string.IsNullOrEmpty(source.Cells[r,c])){lastRow=Math.Max(lastRow,r);lastColumn=Math.Max(lastColumn,c);}
        var raw=new List<string[]> {Enumerable.Range(0,lastColumn+1).Select(source.Column).ToArray()};var typed=new List<string[]> {raw[0]};
        var types=new string[lastColumn+1];
        for(int c=0;c<=lastColumn;c++)
        {
            var rows=Enumerable.Range(source.Start,Math.Max(0,lastRow-source.Start+1)).Where(r=>!string.IsNullOrWhiteSpace(source.Cells[r,c])).ToArray();double value;
            // Leading zero identifiers remain text. The original strings are always available as daten_roh.
            types[c]=rows.Length>0 && rows.All(r=>Number(source,r,c,mode,out value) && !System.Text.RegularExpressions.Regex.IsMatch(source.Cells[r,c]??"",@"^\s*0\d"))?"numeric":rows.Length>0 && rows.All(r=>Plots.Date(source.Cells[r,c],out value))?"date":"text";
        }
        for(int r=source.Start;r<=lastRow;r++)
        {
            var original=new string[lastColumn+1];var values=new string[lastColumn+1];
            for(int c=0;c<=lastColumn;c++){original[c]=source.Cells[r,c]??"";double number;values[c]=types[c]=="numeric"?(Number(source,r,c,mode,out number)?N(number):""):types[c]=="date"?(Plots.Date(original[c],out number)?DateTime.FromOADate(number).ToString("yyyy-MM-dd'T'HH:mm:ss",Invariant):""):original[c];}
            raw.Add(original);typed.Add(values);
        }
        Csv(Path.Combine(directory,"daten_roh.csv"),raw);Csv(Path.Combine(directory,"daten.csv"),typed);
        var code=new StringBuilder("daten_roh <- "+Read("daten_roh.csv")+"\ndaten <- "+Read("daten.csv")+"\n");
        for(int c=0;c<types.Length;c++)if(types[c]!="text")code.AppendLine("daten[["+(c+1)+"]] <- "+(types[c]=="numeric"?"as.numeric(daten[["+(c+1)+"]])":"as.POSIXct(daten[["+(c+1)+"]], format = '%Y-%m-%dT%H:%M:%S', tz = 'UTC')"));
        code.AppendLine("saveRDS(daten, file.path(.byby_root, 'daten.rds'))");return code.ToString();
    }
    static string Plot(PlotData data,string directory,int width,int height)
    {
        var rows=new List<string[]> {new[]{"x","y","reihe","gruppe","messung","farbe","label","ymin","lower","middle","upper","ymax"}};var outliers=new List<string[]> {new[]{"x","y","farbe"}};
        var colors=new Dictionary<string,string>();var measures=new List<string>();var series=new List<string>();
        for(int s=0;s<data.Series.Count;s++)
        {
            var item=data.Series[s];string color=data.Groups.Count>0?item.Group:item.Name,measure=data.Groups.Count>0 && data.Measures.Count>item.MeasureIndex?data.Measures[item.MeasureIndex]:item.Name;
            colors[color]=item.ColorIndex>=0?PlotPalette.Color(item.ColorIndex):PlotDrawing.Colors[s%PlotDrawing.Colors.Length];if(!measures.Contains(measure))measures.Add(measure);series.Add("s"+s);
            foreach(var p in item.Points)rows.Add(new[]{N(data.NumericX?p.X:p.X+1),N(p.Y),"s"+s,item.Group??"",measure,color,p.Label??"","","","","",""});
        }
        for(int i=0;i<data.Boxes.Count;i++)
        {
            var b=data.Boxes[i];bool grouped=data.Groups.Count>0;double x=grouped?b.CategoryIndex+1+.72*(-.5+(b.GroupSlot+.5)/data.Groups.Count):i+1;
            string color=grouped?b.Group:data.YLabel;colors[color]=b.ColorIndex>=0?PlotPalette.Color(b.ColorIndex):PlotDrawing.Colors[0];series.Add("b"+i);if(!measures.Contains(data.YLabel))measures.Add(data.YLabel);
            rows.Add(new[]{N(x),N(b.Median),"b"+i,b.Group??"",data.YLabel,color,b.Label,N(b.Low),N(b.Q1),N(b.Median),N(b.Q3),N(b.High)});
            foreach(double y in b.Outliers)outliers.Add(new[]{N(x),N(y),color});
        }
        Csv(Path.Combine(directory,"plot_daten.csv"),rows);Csv(Path.Combine(directory,"plot_ausreisser.csv"),outliers);
        var code=new StringBuilder();code.AppendLine("\n# ggplot2 wird nur benötigt, wenn ein Plot exportiert wurde. Keine automatische Installation.");code.AppendLine("if (!requireNamespace('ggplot2', quietly = TRUE)) stop('Daten sind geladen. Für den Plot bitte ggplot2 installieren und start.R erneut ausführen.')\nlibrary(ggplot2)");
        code.AppendLine("plot_daten <- "+Read("plot_daten.csv"));code.AppendLine("for (.spalte in c('x','y','ymin','lower','middle','upper','ymax')) plot_daten[[.spalte]] <- as.numeric(plot_daten[[.spalte]])");
        code.AppendLine("plot_daten$reihe <- factor(plot_daten$reihe, levels = "+Vector(series)+")");code.AppendLine("plot_daten$farbe <- factor(plot_daten$farbe, levels = "+Vector(colors.Keys)+")");code.AppendLine("plot_daten$messung <- factor(plot_daten$messung, levels = "+Vector(measures)+")");
        if(data.DateX)code.AppendLine("plot_daten$x <- as.POSIXct(plot_daten$x * 86400, origin = '1899-12-30', tz = 'UTC')");
        if(data.Kind==PlotKind.Box)
        {
            code.AppendLine("plot <- ggplot(plot_daten, aes(x = x, group = reihe, colour = farbe, fill = farbe)) + geom_boxplot(aes(ymin = ymin, lower = lower, middle = middle, upper = upper, ymax = ymax), stat = 'identity', orientation = 'x', width = "+N(.6/Math.Max(1,data.Groups.Count))+", alpha = 0.3)");
            code.AppendLine("plot_ausreisser <- "+Read("plot_ausreisser.csv")+"\nplot_ausreisser$x <- as.numeric(plot_ausreisser$x)\nplot_ausreisser$y <- as.numeric(plot_ausreisser$y)\nplot <- plot + geom_point(data = plot_ausreisser, aes(x = x, y = y, colour = farbe), inherit.aes = FALSE, size = 1.5)");
        }
        else
        {
            code.AppendLine("plot <- ggplot(plot_daten, aes(x = x, y = y, group = reihe, colour = farbe))");
            if(data.Kind==PlotKind.Line)code.AppendLine("plot <- plot + geom_line(aes(linetype = messung)) + geom_point(aes(shape = messung), size = 1.8)");
            else if(data.Kind==PlotKind.Scatter)code.AppendLine("plot <- plot + geom_point(aes(shape = messung), size = 2)");
            else code.AppendLine("plot <- plot + geom_col(aes(fill = farbe"+(data.Groups.Count>0 && data.Measures.Count>1?", alpha = messung":"")+"), width = "+N(data.Kind==PlotKind.Histogram?data.BinWidth*.9:.76)+", position = position_dodge(width = "+N(data.Kind==PlotKind.Histogram?data.BinWidth:.76)+"))");
        }
        if(!data.NumericX && data.Categories.Count>0)code.AppendLine("plot <- plot + scale_x_continuous(breaks = seq_along("+Vector(data.Categories)+"), labels = "+Vector(data.Categories)+")");
        code.AppendLine(".farben <- setNames("+Vector(colors.Values)+", "+Vector(colors.Keys)+")\nplot <- plot + scale_colour_manual(values = .farben)"+(data.Kind==PlotKind.Line || data.Kind==PlotKind.Scatter?"":" + scale_fill_manual(values = .farben)"));
        if(data.Groups.Count>0 && data.Measures.Count>1 && (data.Kind==PlotKind.Columns || data.Kind==PlotKind.Bars))code.AppendLine("plot <- plot + scale_alpha_manual(values = c(1, 0.55))");
        if(data.Kind==PlotKind.Bars)code.AppendLine("plot <- plot + coord_flip()");
        if(data.Zero || data.Kind==PlotKind.Columns || data.Kind==PlotKind.Bars || data.Kind==PlotKind.Histogram)code.AppendLine("plot <- plot + expand_limits(y = 0)");
        code.AppendLine("plot <- plot + "+(data.Dark?"theme_dark()":"theme_minimal()")+" + labs(title = "+Q(data.Title)+", x = "+Q(data.XLabel)+", y = "+Q(data.YLabel)+", colour = "+Q(data.Groups.Count>0?data.GroupLabel:"Messreihe")+(data.Kind==PlotKind.Line || data.Kind==PlotKind.Scatter?"":", fill = "+Q(data.Groups.Count>0?data.GroupLabel:"Messreihe"))+(data.Kind==PlotKind.Line?", shape = 'Messreihe', linetype = 'Messreihe'":data.Kind==PlotKind.Scatter?", shape = 'Messreihe'":data.Groups.Count>0 && data.Measures.Count>1 && (data.Kind==PlotKind.Columns || data.Kind==PlotKind.Bars)?", alpha = 'Messreihe'":"")+")");
        code.AppendLine("saveRDS(plot_daten, file.path(.byby_root, 'plot_daten.rds'))\nsaveRDS(plot, file.path(.byby_root, 'plot.rds'))\nsave(daten, daten_roh, plot_daten, plot, file = file.path(.byby_root, 'analyse.RData'))\nif (interactive()) print(plot)");
        code.AppendLine("# Eigene Änderungen hier ergänzen, zum Beispiel: plot + theme_bw()\n# ggsave(file.path(.byby_root, 'plot-bearbeitet.png'), plot, width = "+N(width/150.0)+", height = "+N(height/150.0)+", units = 'in', dpi = 150)");return code.ToString();
    }
    public static RExportResult Create(string directory,PlotSource source,PlotData plot=null,string mode="Auto",int width=1800,int height=1200)
    {
        directory=Path.GetFullPath(directory);if(System.IO.Directory.Exists(directory) || File.Exists(directory))throw new IOException("Der R-Projektordner existiert schon. Bitte einen neuen Namen wählen.");System.IO.Directory.CreateDirectory(directory);
        var result=new RExportResult {Directory=directory,Project=Path.Combine(directory,"Tabelle.Rproj"),Script=Path.Combine(directory,"start.R")};
        string boot="# Aus Tabelle exportiert. Dieses Projekt benötigt kein Internet.\n# daten: typisierte Werte; daten_roh: unveränderte Zelltexte einschließlich Formeln.\n.byby_file <- tryCatch(sys.frame(1)$ofile, error = function(e) NULL)\nif (is.null(.byby_file)) {\n  .byby_args <- grep('^--file=', commandArgs(FALSE), value = TRUE)\n  if (length(.byby_args)) .byby_file <- sub('^--file=', '', .byby_args[1])\n}\n.byby_root <- if (!is.null(.byby_file)) dirname(normalizePath(.byby_file)) else getwd()\nif (!file.exists(file.path(.byby_root, 'daten.csv'))) stop('Bitte das exportierte R-Projekt öffnen oder start.R mit source() ausführen.')\n";
        string script=boot+Data(source,directory,mode)+(plot==null?"\n# Dein R-Skript: hier mit daten weiterarbeiten.\n":Plot(plot,directory,width,height));
        File.WriteAllText(result.Script,script,new UTF8Encoding(false));
        File.WriteAllText(result.Project,"Version: 1.0\nRestoreWorkspace: No\nSaveWorkspace: No\nAlwaysSaveHistory: No\nEncoding: UTF-8\n",new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory,".Rprofile"),"source('start.R', local = .GlobalEnv, encoding = 'UTF-8')\n",new UTF8Encoding(false));
        if(plot!=null){PlotDrawing.Export(plot,Path.Combine(directory,"plot.svg"),width,height);PlotDrawing.Export(plot,Path.Combine(directory,"plot.png"),width,height);}
        File.WriteAllText(Path.Combine(directory,"LIESMICH.txt"),"In RStudio Tabelle.Rproj öffnen: start.R lädt daten und optional plot automatisch.\r\nIn R: source('Pfad/zum/start.R', encoding='UTF-8').\r\nNur das aktuelle Blatt wird exportiert. daten_roh enthält originale Zelltexte; daten enthält erkannte Zahlen, Datumswerte und berechnete Formelergebnisse.\r\nplot_daten enthält exakt die dargestellten Werte einschließlich Auswahl, Aggregation, Gruppen- und Kategorienlimits. Histogrammklassen und Boxplot-Statistiken sind vorberechnet, damit R dieselben Werte zeigt. Die Gestaltung kann von der Vorschau abweichen.\r\nNach Ausführung entstehen daten.rds und bei vorhandenem ggplot2 plot.rds, plot_daten.rds und analyse.RData. Ohne R bleiben CSV, R-Skript und Plotbilder vollständig nutzbar. ggplot2 wird nicht automatisch installiert.\r\nOriginaldateien werden nicht verändert.\r\n",Encoding.UTF8);
        result.Message="R-Projekt exportiert. start.R lädt die Daten"+(plot==null?".":" und erzeugt das ggplot-Objekt.");return result;
    }
    internal static string FindRscript()
    {
        foreach(string hive in new[]{@"HKEY_CURRENT_USER\Software\R-core\R",@"HKEY_LOCAL_MACHINE\Software\R-core\R"}){string root=Registry.GetValue(hive,"InstallPath",null) as string;if(root!=null)foreach(string sub in new[]{@"bin\Rscript.exe",@"bin\x64\Rscript.exe"}){string file=Path.Combine(root,sub);if(File.Exists(file))return file;}}
        foreach(string parent in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"R"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","R")})if(System.IO.Directory.Exists(parent))foreach(string root in System.IO.Directory.GetDirectories(parent).OrderByDescending(p=>p,StringComparer.OrdinalIgnoreCase))foreach(string sub in new[]{@"bin\Rscript.exe",@"bin\x64\Rscript.exe"}){string file=Path.Combine(root,sub);if(File.Exists(file))return file;}
        return FindOnPath("Rscript.exe");
    }
    static string FindOnPath(string name){foreach(string dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(';'))try{string file=Path.Combine(dir.Trim('"'),name);if(Path.IsPathRooted(file) && File.Exists(file))return file;}catch(ArgumentException){}return null;}
    internal static string FindRStudio()
    {
        foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs")})foreach(string sub in new[]{@"RStudio\rstudio.exe",@"Posit\RStudio\rstudio.exe"}){string file=Path.Combine(root,sub);if(File.Exists(file))return file;}
        return FindOnPath("rstudio.exe");
    }
    public static async Task PrepareObjects(RExportResult result)
    {
        string r=FindRscript();if(r==null){result.Message+=" R ist nicht installiert; .rds-Dateien entstehen beim späteren Ausführen des Skripts.";return;}
        await Task.Run(async delegate
        {
            using(var process=new Process {StartInfo=new ProcessStartInfo(r,"--vanilla \""+result.Script+"\"") {WorkingDirectory=result.Directory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}})
            {
                foreach(string variable in new[]{"LC_ALL","LC_CTYPE","LANG"})if(process.StartInfo.EnvironmentVariables[variable]=="C.UTF-8")process.StartInfo.EnvironmentVariables.Remove(variable);
                process.Start();var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
                bool done=await Task.Run(()=>process.WaitForExit(120000));if(!done){try{process.Kill();}catch(InvalidOperationException){}result.Message+=" R hat länger als zwei Minuten benötigt. CSV und Skript sind gespeichert; bitte in RStudio fortsetzen.";return;}
                await output;string error=await errors;result.PlotObjectCreated=process.ExitCode==0 && File.Exists(Path.Combine(result.Directory,"plot.rds"));
                result.Message+=process.ExitCode==0?(result.PlotObjectCreated?" plot.rds und analyse.RData sind fertig.":" daten.rds ist fertig."):" Die R-Ausführung meldet: "+error.Trim();
            }
        });
    }
    public static void Open(RExportResult result)
    {
        string studio=FindRStudio();if(studio!=null){Process.Start(new ProcessStartInfo(studio,"\""+result.Project+"\"") {UseShellExecute=true});return;}
        // Shell opening .Rproj delegates to a user-selected R editor; never execute arbitrary imported cell contents.
        try{Process.Start(new ProcessStartInfo(result.Project){UseShellExecute=true});}catch(System.ComponentModel.Win32Exception){Process.Start(new ProcessStartInfo("explorer.exe","\""+result.Directory+"\""){UseShellExecute=true});}
    }
}
