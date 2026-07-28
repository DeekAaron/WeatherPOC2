using System.Globalization;

namespace WeatherPoc2.App;

/// <summary>
/// True when the bound count is greater than zero. Lets the search page hide the "Recent" header
/// when Search History is empty (empty history shows just the search box) — bindings only, no
/// code-behind logic (Overriding Principle #2).
/// </summary>
public sealed class CountToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
