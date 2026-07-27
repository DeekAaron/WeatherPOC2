using System.Globalization;

namespace WeatherPoc2.Core.Units;

/// <summary>
/// The single thin presentation layer for Units: composes <see cref="UnitConversion"/> with
/// whole-number (away-from-zero) rounding and the unit suffix into the display string. The weather
/// ViewModels call this and hold no formatting rules of their own. Pure; no I/O; total (cannot fail).
/// Temperature has no space before the degree symbol (<c>18°C</c>); Wind Speed is spaced (<c>12 km/h</c>).
/// Digits render with <see cref="CultureInfo.InvariantCulture"/> so the device locale never alters them.
/// </summary>
public sealed class UnitFormatter
{
    public string FormatTemperature(double celsius, TemperatureUnit unit)
    {
        var rounded = RoundWhole(UnitConversion.ToDisplayTemperature(celsius, unit));
        var symbol = unit == TemperatureUnit.Fahrenheit ? "°F" : "°C";
        return rounded.ToString(CultureInfo.InvariantCulture) + symbol;   // no space: "18°C"
    }

    public string FormatWindSpeed(double kilometresPerHour, WindSpeedUnit unit)
    {
        var rounded = RoundWhole(UnitConversion.ToDisplayWindSpeed(kilometresPerHour, unit));
        var suffix = unit switch
        {
            WindSpeedUnit.MilesPerHour => "mph",
            WindSpeedUnit.MetresPerSecond => "m/s",
            WindSpeedUnit.Knots => "kn",
            _ => "km/h",
        };
        return rounded.ToString(CultureInfo.InvariantCulture) + " " + suffix; // spaced: "12 km/h"
    }

    private static int RoundWhole(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
