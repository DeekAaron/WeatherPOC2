namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The Gateway's return shape, in canonical units (ADR-0001: weather is always held in canonical
/// units). Extended — not reshaped — across Features: F1 added the temperature; F2 adds Wind Speed,
/// the current-hour Chance of Rain, and the raw weather_code / is_day hints the mapper resolves for
/// display; F4 adds the full hourly series (Hourly) and the Location-local "now" (LocalNow), both
/// under timezone=auto (ADR-0002).
///
/// Nullability: the three current numeric measures are non-null once produced (a missing one is the
/// Gateway's failure path). CurrentWeatherCode and IsDay are nullable — an absent icon-only hint flows
/// through and the mapper falls back (Unknown / day). Hourly is never null (an empty list at worst is a
/// Gateway fail-closed path, not a null); each HourlyForecastPoint carries its own per-field
/// nullability. LocalNow is the Location's wall clock parsed from current.time (Kind=Unspecified).
/// </summary>
public sealed record WeatherBundle(
    double CurrentTemperatureCelsius,
    double CurrentWindSpeedKmh,
    int CurrentChanceOfRainPercent,
    int? CurrentWeatherCode,
    bool? IsDay,
    IReadOnlyList<HourlyForecastPoint> Hourly,
    DateTime LocalNow);
