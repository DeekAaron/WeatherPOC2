namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The app's curated set of Weather Conditions (Context.MD): Open-Meteo's numeric WMO weather codes
/// are collapsed onto this small set rather than mirroring every code. <see cref="Unknown"/> is the
/// neutral fallback for an unrecognized or absent code.
/// </summary>
public enum WeatherCondition
{
    Clear,
    PartlyCloudy,
    Cloudy,
    Fog,
    Drizzle,
    Rain,
    RainShowers,
    Snow,
    SnowShowers,
    Thunderstorm,
    Unknown,
}
