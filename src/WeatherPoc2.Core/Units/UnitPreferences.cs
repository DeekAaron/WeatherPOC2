namespace WeatherPoc2.Core.Units;

/// <summary>
/// The user's per-measure display Units — the unit of persistence for this Feature (Seam 1).
/// Value-equality (record) lets later Features decide whether a change is a no-op.
/// </summary>
public sealed record UnitPreferences(TemperatureUnit Temperature, WindSpeedUnit WindSpeed)
{
    /// <summary>Canonical defaults (°C, km/h) — used on first run and on any failed/absent read (D5).</summary>
    public static UnitPreferences Default { get; } = new(TemperatureUnit.Celsius, WindSpeedUnit.KilometresPerHour);
}
