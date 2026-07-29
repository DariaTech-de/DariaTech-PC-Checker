using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DariaTech.PcDoctor.UI.Converters;

/// <summary>
/// bool → Visibility. ConverterParameter "Invert" kehrt das Ergebnis um.
/// (Der eingebaute BooleanToVisibilityConverter unterstützt das nicht.)
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Sichtbar, wenn der Wert vorhanden ist (für Detail-/Hinweisfelder).
/// Leere oder nur aus Leerzeichen bestehende Texte gelten als „nicht vorhanden" –
/// sonst erschiene ein leerer Rahmen ohne Inhalt.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            null => Visibility.Collapsed,
            string s => string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible,
            _ => Visibility.Visible
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
