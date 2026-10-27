using System;
using System.Globalization;
using System.Windows;

internal sealed class DocumentMargins
{
    public double Left=25,Right=25,Top=25,Bottom=25;
    public static double Pixels(double millimeters){return millimeters*96/25.4;}
    public static double Millimeters(double pixels){return pixels*25.4/96;}
    public bool Valid {get{return ValidValue(Left) && ValidValue(Right) && ValidValue(Top) && ValidValue(Bottom);}}
    static bool ValidValue(double value){return !double.IsNaN(value) && !double.IsInfinity(value) && value>=0 && value<=50;}
    public Thickness Padding {get{return new Thickness(Pixels(Left),Pixels(Top),Pixels(Right),Pixels(Bottom));}}
    public DocumentMargins Copy(){return (DocumentMargins)MemberwiseClone();}
    public bool Same(DocumentMargins other){return other!=null && Left==other.Left && Right==other.Right && Top==other.Top && Bottom==other.Bottom;}
    public static bool TryLength(string text,bool centimeters,out double millimeters)
    {
        text=(text??"").Trim().ToLowerInvariant();
        if(text.EndsWith("mm")){centimeters=false;text=text.Substring(0,text.Length-2).Trim();}
        else if(text.EndsWith("cm")){centimeters=true;text=text.Substring(0,text.Length-2).Trim();}
        double value;bool parsed=double.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out value);
        millimeters=parsed?value*(centimeters?10:1):0;return parsed && ValidValue(millimeters);
    }
}
