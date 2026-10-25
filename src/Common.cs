using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

internal static class Ui
{
    public static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(31, 47, 57));
    public static readonly Brush Paper = new SolidColorBrush(Color.FromRgb(246, 247, 243));
    public static void Window(Window window, string title)
    {
        window.Title = title; window.Width = 1120; window.Height = 760;
        window.MinWidth = 760; window.MinHeight = 480;
        window.Background = Paper; window.Foreground = Ink;
        window.FontFamily = new FontFamily("Segoe UI"); window.FontSize = 14;
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
    public static Button Button(Panel panel, string label, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 0), Background = Brushes.White };
        button.Click += delegate { try { action(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Aktion fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error); } };
        panel.Children.Add(button); return button;
    }
    public static void AtomicWrite(string path, Action<string> write)
    {
        string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "." + Guid.NewGuid().ToString("N") + ".tmp");
        try { write(temp); if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static bool ConfirmSave(bool dirty, Func<bool> save)
    {
        if (!dirty) return true;
        var choice = MessageBox.Show("Änderungen vor dem Fortfahren speichern?", "Ungespeicherte Änderungen", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return choice == MessageBoxResult.No || (choice == MessageBoxResult.Yes && save());
    }
}
