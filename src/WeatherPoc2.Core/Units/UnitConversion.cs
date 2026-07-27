namespace WeatherPoc2.Core.Units;

/// <summary>
/// Pure, deterministic conversion from canonical units (°C, km/h) to the user's chosen display unit
/// (ADR-0001: weather is held canonical and converted in-app). Returns a number only — no rounding,
/// no suffix, no I/O. Presentation (rounding + suffix) is <see cref="UnitFormatter"/>'s job.
/// Total over the closed unit enums: no failure path.
/// </summary>
public static class UnitConversion
{
    public static double ToDisplayTemperature(double celsius, TemperatureUnit unit) => unit switch
    {
        TemperatureUnit.Celsius => celsius,
        TemperatureUnit.Fahrenheit => celsius * 9d / 5d + 32d,
        _ => celsius,
    };

    public static double ToDisplayWindSpeed(double kilometresPerHour, WindSpeedUnit unit) => unit switch
    {
        WindSpeedUnit.KilometresPerHour => kilometresPerHour,
        WindSpeedUnit.MilesPerHour => kilometresPerHour * 0.621371d,
        WindSpeedUnit.MetresPerSecond => kilometresPerHour / 3.6d,
        WindSpeedUnit.Knots => kilometresPerHour * 0.539957d,
        _ => kilometresPerHour,
    };
}
