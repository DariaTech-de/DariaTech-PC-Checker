using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DariaTech.PcDoctor.Core;

namespace DariaTech.PcDoctor.UI.Converters;

/// <summary>Wandelt eine <see cref="Severity"/> in die zugehörige Ampelfarbe.</summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    public static readonly SolidColorBrush Ok = Freeze("#2DA44E");
    public static readonly SolidColorBrush Info = Freeze("#5A6877");
    public static readonly SolidColorBrush Warning = Freeze("#E0B000");
    public static readonly SolidColorBrush Critical = Freeze("#D32F2F");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Severity s
            ? s switch
            {
                Severity.Ok => Ok,
                Severity.Warning => Warning,
                Severity.Critical => Critical,
                _ => Info
            }
            : Info;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush Freeze(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
