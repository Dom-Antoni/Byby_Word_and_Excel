using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

internal static class Sheet
{
    public static string Column(int n) { string s = ""; do { s = (char)('A' + n % 26) + s; n = n / 26 - 1; } while (n >= 0); return s; }
    public static List<string[]> Read(string text, char separator)
    {
        var rows = new List<string[]>(); var row = new List<string>(); var field = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else if (quoted || field.Length == 0) quoted = !quoted; else throw new FormatException("Ungültiges Anführungszeichen in CSV."); }
            else if (c == separator && !quoted) { row.Add(field.ToString()); field.Clear(); }
            else if ((c == '\r' || c == '\n') && !quoted) { if (c == '\r' && i + 1 < text.Length && text[i+1] == '\n') i++; row.Add(field.ToString()); field.Clear(); rows.Add(row.ToArray()); row.Clear(); }
            else field.Append(c);
        }
        if (quoted) throw new FormatException("Nicht geschlossenes Anführungszeichen in CSV.");
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
    public static string Quote(string text) { return "\"" + text.Replace("\"", "\"\"") + "\""; }
    public static double Value(string[,] cells, int row, int col, HashSet<string> active)
    {
        if (row < 0 || col < 0 || row >= cells.GetLength(0) || col >= cells.GetLength(1)) throw new FormatException("#BEZUG!");
        string key = row + ":" + col;
        if (active.Count > 200 || !active.Add(key)) throw new FormatException("#ZIRKEL!");
        try
        {
            string raw = cells[row,col] ?? "";
            if (raw.StartsWith("=")) return new Formula(raw.Substring(1), cells, active).Parse();
            if (raw.Trim().Length == 0) return 0;
            double value;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out value) || double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
            throw new FormatException("#WERT!");
        }
        finally { active.Remove(key); }
    }
    sealed class Formula
    {
        readonly string text; readonly string[,] cells; readonly HashSet<string> active; int pos;
        public Formula(string t, string[,] c, HashSet<string> a) { text = t.ToUpperInvariant(); cells = c; active = a; }
        void Space() { while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++; }
        bool Take(char c) { Space(); if (pos < text.Length && text[pos] == c) { pos++; return true; } return false; }
        public double Parse() { double n = Add(); Space(); if (pos != text.Length || double.IsNaN(n) || double.IsInfinity(n)) throw new FormatException("#FORMEL!"); return n; }
        double Add() { double n = Multiply(); while (true) { if (Take('+')) n += Multiply(); else if (Take('-')) n -= Multiply(); else return n; } }
        double Multiply() { double n = Atom(); while (true) { if (Take('*')) n *= Atom(); else if (Take('/')) { double d = Atom(); if (d == 0) throw new FormatException("#DIV/0!"); n /= d; } else return n; } }
        void Cell(out int r, out int c)
        {
            Space(); c = 0; int start = pos;
            while (pos < text.Length && text[pos] >= 'A' && text[pos] <= 'Z') { c = checked(c * 26 + text[pos++] - 'A' + 1); }
            if (pos == start) throw new FormatException("#BEZUG!");
            start = pos; while (pos < text.Length && char.IsDigit(text[pos])) pos++;
            if (pos == start || !int.TryParse(text.Substring(start, pos-start), out r)) throw new FormatException("#BEZUG!"); r--; c--;
        }
        double Atom()
        {
            Space(); if (Take('+')) return Atom(); if (Take('-')) return -Atom();
            if (Take('(')) { double v = Add(); if (!Take(')')) throw new FormatException("#FORMEL!"); return v; }
            var function = Regex.Match(text.Substring(pos), @"^(SUMME|SUM|MITTELWERT|AVERAGE|MIN|MAX)\s*\(");
            if (function.Success)
            {
                pos += function.Length; int r1,c1,r2,c2; Cell(out r1,out c1); r2=r1;c2=c1;
                if (Take(':')) Cell(out r2,out c2);
                if (!Take(')')) throw new FormatException("#FORMEL!");
                if (r1 < 0 || c1 < 0 || r2 >= cells.GetLength(0) || c2 >= cells.GetLength(1) || r2 < r1 || c2 < c1) throw new FormatException("#BEZUG!");
                var values = new List<double>();
                for (int r=r1;r<=r2;r++) for (int c=c1;c<=c2;c++)
                {
                    string raw = cells[r,c] ?? ""; double ignored;
                    if (raw.StartsWith("=") || double.TryParse(raw, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"),out ignored) || double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture,out ignored)) values.Add(Value(cells,r,c,active));
                }
                string name=function.Groups[1].Value;
                if (name=="SUM" || name=="SUMME") return values.Sum();
                if (values.Count==0) throw new FormatException("#WERT!");
                if (name=="MIN") return values.Min(); if (name=="MAX") return values.Max(); return values.Average();
            }
            if (pos < text.Length && char.IsLetter(text[pos])) { int r,c; Cell(out r,out c); return Value(cells,r,c,active); }
            var number=Regex.Match(text.Substring(pos), @"^(?:\d+(?:[.,]\d*)?|[.,]\d+)(?:E[+-]?\d+)?");
            if (!number.Success) throw new FormatException("#FORMEL!"); pos+=number.Length;
            return double.Parse(number.Value.Replace(',','.'), CultureInfo.InvariantCulture);
        }
    }
}

internal sealed class Tabelle : Window
{
    readonly DataGrid grid = new DataGrid(); readonly TextBox formula = new TextBox(); readonly TextBlock status = new TextBlock();
    readonly ComboBox separator = new ComboBox { ItemsSource = new[] { "Semikolon ;", "Komma ,", "Tabulator" }, SelectedIndex = 0, Width = 130 };
    string[,] cells; DataTable table; string path; bool dirty, results; int selectedRow, selectedCol;
    readonly ComboBox worksheets = new ComboBox { MinWidth=140, Margin=new Thickness(0,0,0,12), Visibility=Visibility.Collapsed };
    List<Formats.Worksheet> workbook;
    [STAThread] public static void Main() { new Application().Run(new Tabelle()); }
    public Tabelle()
    {
        Ui.Window(this, "Tabelle · Lokal rechnen");
        var root = new DockPanel { Margin = new Thickness(24) }; Content=root;
        var title = new TextBlock { Text="Tabelle", FontSize=30, FontWeight=FontWeights.SemiBold, Margin=new Thickness(0,0,0,16) }; DockPanel.SetDock(title,Dock.Top);root.Children.Add(title);
        var bar = new StackPanel { Orientation=Orientation.Horizontal, Margin=new Thickness(0,0,0,14) }; DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);
        Ui.Button(bar,"Neu",delegate { Sync();if(CanLeave()) { path=null; workbook=null;worksheets.Visibility=Visibility.Collapsed;Reset(100,26); dirty=false; UpdateTitle(); } });
        Ui.Button(bar,"Öffnen…",Open); Ui.Button(bar,"Speichern",delegate { Save(); });
        Ui.Button(bar,"+ 50 Zeilen",delegate { Sync(); Resize(cells.GetLength(0)+50,cells.GetLength(1)); });
        Ui.Button(bar,"+ Spalte",delegate { Sync(); Resize(cells.GetLength(0),cells.GetLength(1)+1); });
        Ui.Button(bar,"Ergebnisse / Eingabe",delegate { Sync(); results=!results; Render(); });
        bar.Children.Add(separator);
        var fb = new DockPanel { Margin=new Thickness(0,0,0,14) }; DockPanel.SetDock(fb,Dock.Top);root.Children.Add(fb);
        Ui.Button(fb,"Übernehmen",delegate { string value=formula.Text;Sync();if(selectedRow<0)return;cells[selectedRow,selectedCol]=value; dirty=true;Render();UpdateTitle(); });
        formula.Padding=new Thickness(8);fb.Children.Add(formula);
        DockPanel.SetDock(worksheets,Dock.Top);root.Children.Add(worksheets);
        worksheets.SelectionChanged+=delegate { if(workbook==null || worksheets.SelectedIndex<0)return;Sync();cells=workbook[worksheets.SelectedIndex].Cells;selectedRow=selectedCol=0;Render(); };
        status.Margin=new Thickness(0,12,0,0);DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);
        grid.CanUserAddRows=false;grid.CanUserDeleteRows=false;grid.CanUserReorderColumns=false;grid.CanUserSortColumns=false;grid.AutoGenerateColumns=true;grid.HeadersVisibility=DataGridHeadersVisibility.All;
        grid.LoadingRow+=delegate(object s,DataGridRowEventArgs e) { e.Row.Header=(e.Row.GetIndex()+1).ToString(); };
        grid.CurrentCellChanged+=delegate { if(grid.CurrentCell.Column==null || grid.CurrentItem==null) return; var item=grid.CurrentItem as DataRowView; if(item==null)return; selectedRow=table.Rows.IndexOf(item.Row);selectedCol=grid.CurrentCell.Column.DisplayIndex; if(selectedRow>=0) formula.Text=cells[selectedRow,selectedCol] ?? ""; };
        grid.CellEditEnding+=delegate { dirty=true;UpdateTitle(); };
        root.Children.Add(grid);Reset(100,26);
        Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e) { Sync();e.Cancel=!CanLeave(); };
    }
    char Delimiter { get { return new[] {';',',','\t'}[separator.SelectedIndex]; } }
    void UpdateTitle() { Title=(dirty?"● ":"")+(path==null?"Unbenannt":Path.GetFileName(path))+" · Tabelle"; }
    bool CanLeave() { return Ui.ConfirmSave(dirty,Save); }
    void Reset(int rows,int cols) { cells=new string[rows,cols];selectedRow=selectedCol=0;Render(); }
    void Resize(int rows,int cols) { var next=new string[rows,cols];for(int r=0;r<cells.GetLength(0);r++)for(int c=0;c<cells.GetLength(1);c++)next[r,c]=cells[r,c];cells=next;dirty=true;Render();UpdateTitle(); }
    void Sync()
    {
        grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);
        if(results || table==null)return;
        for(int r=0;r<cells.GetLength(0);r++)for(int c=0;c<cells.GetLength(1);c++)cells[r,c]=Convert.ToString(table.Rows[r][c]);
    }
    void Render()
    {
        table=new DataTable();for(int c=0;c<cells.GetLength(1);c++)table.Columns.Add(Sheet.Column(c),typeof(string));
        for(int r=0;r<cells.GetLength(0);r++)
        {
            var row=table.NewRow();for(int c=0;c<cells.GetLength(1);c++) { string raw=cells[r,c]??"";if(results && raw.StartsWith("=")) { try {raw=Sheet.Value(cells,r,c,new HashSet<string>()).ToString("G12",CultureInfo.GetCultureInfo("de-DE"));}catch(Exception ex){raw=ex is FormatException?ex.Message:"#FORMEL!";} }row[c]=raw; }table.Rows.Add(row);
        }
        grid.ItemsSource=table.DefaultView;grid.IsReadOnly=results;
        status.Text=(results?"Ergebnisse · Formeln oben bearbeiten":"Eingabe · Formeln beginnen mit =")+"  ·  "+cells.GetLength(0)+" Zeilen  ·  Offline";
    }
    void Open()
    {
        Sync();if(!CanLeave())return;
        var dialog=new OpenFileDialog { Filter="Tabellen (*.csv;*.tsv;*.xlsx)|*.csv;*.tsv;*.xlsx" };if(dialog.ShowDialog()!=true)return;
        if(Path.GetExtension(dialog.FileName).Equals(".xlsx",StringComparison.OrdinalIgnoreCase))
        {
            var imported=Formats.ReadXlsx(dialog.FileName);if(imported.Count==0)throw new FormatException("Keine Tabellenblätter gefunden.");
            MessageBox.Show("XLSX wird als Zellwerte importiert. Excel-Formeln werden über gespeicherte Ergebnisse angezeigt; fehlende Ergebnisse sind markiert. Datum und Formatierungen werden nicht übernommen. Speichern exportiert nur das ausgewählte Blatt als CSV/TSV.","XLSX-Werteimport");
            workbook=imported;worksheets.ItemsSource=workbook.Select(x=>x.Name).ToArray();worksheets.Visibility=Visibility.Visible;worksheets.SelectedIndex=0;
            cells=workbook[0].Cells;selectedRow=selectedCol=0;path=null;dirty=false;Render();UpdateTitle();return;
        }
        char delimiter=Path.GetExtension(dialog.FileName).ToLowerInvariant()==".tsv"?'\t':Delimiter;
        var rows=Sheet.Read(File.ReadAllText(dialog.FileName,Encoding.UTF8),delimiter);
        int columns=rows.Count==0?26:Math.Max(26,rows.Max(x=>x.Length));
        if(rows.Count>10000 || columns>512)throw new FormatException("Diese Grundversion unterstützt höchstens 10.000 Zeilen und 512 Spalten.");
        workbook=null;worksheets.Visibility=Visibility.Collapsed;Reset(Math.Max(100,rows.Count),columns);
        for(int r=0;r<rows.Count;r++)for(int c=0;c<rows[r].Length;c++)cells[r,c]=rows[r][c];
        separator.SelectedIndex=delimiter=='\t'?2:delimiter==','?1:0;path=dialog.FileName;dirty=false;Render();UpdateTitle();
    }
    bool Save()
    {
        Sync();
        var dialog=new SaveFileDialog { Filter="CSV (*.csv)|*.csv|TSV (*.tsv)|*.tsv", FileName=path==null?"Tabelle.csv":Path.GetFileName(path) };if(dialog.ShowDialog()!=true)return false;
        try
        {
            char delimiter=Path.GetExtension(dialog.FileName).ToLowerInvariant()==".tsv"?'\t':Delimiter;
            int lastRow=0,lastCol=0;for(int r=0;r<cells.GetLength(0);r++)for(int c=0;c<cells.GetLength(1);c++)if(!string.IsNullOrEmpty(cells[r,c])){lastRow=Math.Max(lastRow,r);lastCol=Math.Max(lastCol,c);}
            Ui.AtomicWrite(dialog.FileName,delegate(string temp) { using(var writer=new StreamWriter(temp,false,new UTF8Encoding(true)))for(int r=0;r<=lastRow;r++){var fields=new List<string>();for(int c=0;c<=lastCol;c++)fields.Add(Sheet.Quote(cells[r,c]??""));writer.WriteLine(string.Join(delimiter.ToString(),fields));} });
            path=dialog.FileName;dirty=false;UpdateTitle();return true;
        }
        catch(Exception ex){MessageBox.Show(ex.Message,"Speichern fehlgeschlagen");return false;}
    }
}
