namespace WeatherPoc2.Core.ViewModels;

/// <summary>One immutable hourly strip cell (variant A: Time · Icon · Temperature · Chance).
/// Built fresh each Apply; the collection is rebuilt rather than mutating items, so no per-item
/// change notification is needed. IsNow flags the current hour for the "Now" treatment.</summary>
public sealed record HourlyForecastItem(
    string TimeDisplay,
    string IconSource,
    string TemperatureDisplay,
    string ChanceOfRainDisplay,
    bool IsNow);
