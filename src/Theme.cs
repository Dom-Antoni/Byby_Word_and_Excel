using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;

internal sealed class Appearance
{
    public string Mode="System";
    public bool DarkDocument=true;
    public static string FilePath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Byby_Word_and_Excel","appearance.json");}}
    public static Appearance Load(){if(!File.Exists(FilePath))return new Appearance();var value=new JavaScriptSerializer().Deserialize<Appearance>(File.ReadAllText(FilePath));if(value==null || value.Mode!="Light" && value.Mode!="Dark" && value.Mode!="System")return new Appearance();return value;}
    public void Save(){Directory.CreateDirectory(Path.GetDirectoryName(FilePath));Ui.AtomicWrite(FilePath,delegate(string temp){File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));});}
}

internal static class Theme
{
    static ResourceDictionary resources;
    static bool systemHook;
    public static Appearance Current=ReadAppearance();
    public static event Action Changed;
    public static bool Dark {get{return Current.Mode=="Dark" || Current.Mode=="System" && SystemDark();}}
    static Appearance ReadAppearance(){try{return Appearance.Load();}catch{return new Appearance();}}
    static bool SystemDark(){try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return key!=null && Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0;}catch{return false;}}
    public static void Bind(FrameworkElement element,DependencyProperty property,string key){element.SetResourceReference(property,key);}
    public static void Install(Window window)
    {
        if(resources==null){resources=(ResourceDictionary)XamlReader.Parse(Styles);ApplyPalette(Dark);}
        if(Application.Current!=null){if(!Application.Current.Resources.MergedDictionaries.Contains(resources))Application.Current.Resources.MergedDictionaries.Add(resources);if(!systemHook){systemHook=true;SystemEvents.UserPreferenceChanged+=SystemChanged;Application.Current.Exit+=delegate{SystemEvents.UserPreferenceChanged-=SystemChanged;systemHook=false;};}}
        else if(!window.Resources.MergedDictionaries.Contains(resources))window.Resources.MergedDictionaries.Add(resources);
        Bind(window,Control.BackgroundProperty,"App.Background");Bind(window,Control.ForegroundProperty,"App.Text");
    }
    static void SystemChanged(object sender,UserPreferenceChangedEventArgs e){if(Current.Mode=="System" && Application.Current!=null)Application.Current.Dispatcher.BeginInvoke(new Action(delegate{ApplyPalette(Dark);if(Changed!=null)Changed();}));}
    public static void Set(string mode,bool darkDocument,bool persist=true)
    {
        Current.Mode=mode;Current.DarkDocument=darkDocument;
        if(resources!=null)ApplyPalette(Dark);if(persist)Current.Save();if(Changed!=null)Changed();
    }
    public static Brush Color(string key){if(resources==null){resources=(ResourceDictionary)XamlReader.Parse(Styles);ApplyPalette(Dark);}return (Brush)resources[key];}
    static void ApplyPalette(bool dark)
    {
        string[,] colors=dark?new[,]{{"App.Background","#181C22"},{"App.Panel","#232932"},{"App.Control","#303844"},{"App.Text","#E8ECF2"},{"App.Muted","#B0BAC9"},{"App.Border","#536072"},{"App.Hover","#424F61"},{"App.Selected","#344E71"},{"App.Accent","#9BC8FF"},{"App.Error","#FFACAD"}}:new[,]{{"App.Background","#E8EBF0"},{"App.Panel","#F8F9FB"},{"App.Control","#FFFFFF"},{"App.Text","#203040"},{"App.Muted","#536174"},{"App.Border","#A9B4C3"},{"App.Hover","#E1EAF6"},{"App.Selected","#C9DDF7"},{"App.Accent","#184A86"},{"App.Error","#A12734"}};
        for(int i=0;i<colors.GetLength(0);i++)resources[colors[i,0]]=new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(colors[i,1]));
        resources[SystemColors.WindowBrushKey]=resources["App.Control"];resources[SystemColors.WindowTextBrushKey]=resources["App.Text"];resources[SystemColors.ControlBrushKey]=resources["App.Panel"];resources[SystemColors.ControlTextBrushKey]=resources["App.Text"];resources[SystemColors.MenuBrushKey]=resources["App.Control"];resources[SystemColors.MenuTextBrushKey]=resources["App.Text"];resources[SystemColors.HighlightBrushKey]=resources["App.Selected"];resources[SystemColors.HighlightTextBrushKey]=resources["App.Text"];
        ((SolidColorBrush)Ui.Ink).Color=((SolidColorBrush)resources["App.Text"]).Color;((SolidColorBrush)Ui.Paper).Color=((SolidColorBrush)resources["App.Panel"]).Color;
    }
    const string Styles=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='{x:Type Button}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Control}'/><Setter Property='BorderBrush' Value='{DynamicResource App.Border}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'><Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='3' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource App.Hover}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource App.Accent}'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type TextBox}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Control}'/><Setter Property='BorderBrush' Value='{DynamicResource App.Border}'/><Setter Property='CaretBrush' Value='{DynamicResource App.Text}'/><Setter Property='SelectionBrush' Value='{DynamicResource App.Selected}'/></Style>
<Style TargetType='{x:Type ListBox}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Control}'/><Setter Property='BorderBrush' Value='{DynamicResource App.Border}'/></Style>
<Style TargetType='{x:Type ListBoxItem}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ListBoxItem}'><Border x:Name='b' Background='Transparent' Padding='3'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource App.Hover}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource App.Selected}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ComboBox}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Control}'/><Setter Property='BorderBrush' Value='{DynamicResource App.Border}'/><Setter Property='Padding' Value='7'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBox}'><Grid><ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}'><ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'><Border Background='{DynamicResource App.Control}' BorderBrush='{DynamicResource App.Border}' BorderThickness='1' CornerRadius='3'/></ControlTemplate></ToggleButton.Template></ToggleButton><ContentPresenter Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' IsHitTestVisible='False' Margin='8,4,24,4' VerticalAlignment='Center'/><TextBlock Text='▾' Foreground='{DynamicResource App.Text}' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,8,0' IsHitTestVisible='False'/><Popup Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False'><Border Background='{DynamicResource App.Control}' BorderBrush='{DynamicResource App.Border}' BorderThickness='1' MinWidth='{Binding ActualWidth,RelativeSource={RelativeSource TemplatedParent}}'><ScrollViewer MaxHeight='300'><ItemsPresenter/></ScrollViewer></Border></Popup></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type ComboBoxItem}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Control}'/><Setter Property='Padding' Value='8,5'/><Style.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter Property='Background' Value='{DynamicResource App.Selected}'/><Setter Property='Foreground' Value='{DynamicResource App.Text}'/></Trigger></Style.Triggers></Style>
<Style TargetType='{x:Type TabControl}'><Setter Property='Background' Value='{DynamicResource App.Panel}'/><Setter Property='BorderBrush' Value='{DynamicResource App.Border}'/></Style>
<Style TargetType='{x:Type TabItem}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TabItem}'><Border Name='b' Padding='12,8' Background='{DynamicResource App.Control}' BorderBrush='{DynamicResource App.Border}' BorderThickness='1'><ContentPresenter ContentSource='Header'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='b' Property='Background' Value='{DynamicResource App.Selected}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='{x:Type CheckBox}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/></Style><Style TargetType='{x:Type Expander}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/></Style>
<Style TargetType='{x:Type Menu}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Panel}'/></Style>
<Style TargetType='{x:Type ContextMenu}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Control}'/><Setter Property='BorderBrush' Value='{DynamicResource App.Border}'/></Style>
<Style TargetType='{x:Type MenuItem}'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Control}'/><Setter Property='Padding' Value='8,5'/><Style.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter Property='Foreground' Value='{DynamicResource App.Text}'/><Setter Property='Background' Value='{DynamicResource App.Selected}'/></Trigger></Style.Triggers></Style>
<SolidColorBrush x:Key='{x:Static SystemColors.WindowBrushKey}' Color='#FFFFFF'/><SolidColorBrush x:Key='{x:Static SystemColors.WindowTextBrushKey}' Color='#203040'/>
</ResourceDictionary>";
}
