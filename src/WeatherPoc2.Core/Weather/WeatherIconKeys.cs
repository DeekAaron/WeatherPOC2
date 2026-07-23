namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The single source of truth for the finite set of Weather Icon asset keys the app may render.
/// The mapper only ever emits a key from <see cref="All"/>; the View binds its Image.Source to
/// "{key}.png" (MAUI's resizetizer emits a PNG per source SVG). Four conditions carry day/night
/// variants; six carry a single icon; plus the neutral "unknown" fallback = 15 keys.
/// </summary>
public static class WeatherIconKeys
{
    public const string ClearDay = "clear_day";
    public const string ClearNight = "clear_night";
    public const string PartlyCloudyDay = "partly_cloudy_day";
    public const string PartlyCloudyNight = "partly_cloudy_night";
    public const string Cloudy = "cloudy";
    public const string Fog = "fog";
    public const string Drizzle = "drizzle";
    public const string Rain = "rain";
    public const string RainShowersDay = "rain_showers_day";
    public const string RainShowersNight = "rain_showers_night";
    public const string Snow = "snow";
    public const string SnowShowersDay = "snow_showers_day";
    public const string SnowShowersNight = "snow_showers_night";
    public const string Thunderstorm = "thunderstorm";
    public const string Unknown = "unknown";

    /// <summary>Every declared icon key. A key the mapper emits MUST be a member of this set.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        ClearDay, ClearNight, PartlyCloudyDay, PartlyCloudyNight, Cloudy, Fog, Drizzle, Rain,
        RainShowersDay, RainShowersNight, Snow, SnowShowersDay, SnowShowersNight, Thunderstorm, Unknown,
    };
}
