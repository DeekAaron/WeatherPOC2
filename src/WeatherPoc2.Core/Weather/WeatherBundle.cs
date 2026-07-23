namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The Gateway's return shape, in canonical units (ADR-0001: weather is always held in canonical
/// units). Extended — not reshaped — across Features: F1 added the temperature; F2 adds Wind Speed,
/// the current-hour Chance of Rain, and the raw weather_code / is_day hints the mapper resolves for
/// display.
///
/// Nullability: the three numeric measures are non-null once produced (a missing one is the Gateway's
/// failure path). CurrentWeatherCode and IsDay are nullable — an absent icon-only hint flows through
/// and the mapper falls back (Unknown / day).
/// </summary>
public sealed record WeatherBundle(
    double CurrentTemperatureCelsius,
    double CurrentWindSpeedKmh,
    int CurrentChanceOfRainPercent,
    int? CurrentWeatherCode,
    bool? IsDay);
