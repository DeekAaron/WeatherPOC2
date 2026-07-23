namespace WeatherPoc2.Core.Weather;

/// <summary>The mapper's output: the curated condition, a human display name, the icon-asset key,
/// and whether the source WMO code was recognized (false ⇒ the Unknown fallback was used).</summary>
public sealed record WeatherConditionResult(
    WeatherCondition Condition,
    string DisplayName,
    string IconKey,
    bool Recognized);

/// <summary>
/// Pure, deterministic mapping from Open-Meteo's numeric WMO weather code (and the is_day flag) to a
/// curated <see cref="WeatherCondition"/> + display name + icon key. No I/O, no logging (the caller
/// logs the lenient fall-back). An unlisted or null code maps to <see cref="WeatherCondition.Unknown"/>;
/// a null is_day selects the day variant. Freezing-precipitation codes (56, 57, 66, 67) fold into Snow.
/// </summary>
public sealed class WeatherConditionMapper
{
    public WeatherConditionResult Map(int? weatherCode, bool? isDay)
    {
        var condition = weatherCode switch
        {
            0 or 1 => WeatherCondition.Clear,
            2 => WeatherCondition.PartlyCloudy,
            3 => WeatherCondition.Cloudy,
            45 or 48 => WeatherCondition.Fog,
            51 or 53 or 55 => WeatherCondition.Drizzle,
            61 or 63 or 65 => WeatherCondition.Rain,
            80 or 81 or 82 => WeatherCondition.RainShowers,
            71 or 73 or 75 or 77 or 56 or 57 or 66 or 67 => WeatherCondition.Snow,
            85 or 86 => WeatherCondition.SnowShowers,
            95 or 96 or 99 => WeatherCondition.Thunderstorm,
            _ => WeatherCondition.Unknown,
        };

        var recognized = condition != WeatherCondition.Unknown;
        var day = isDay ?? true; // lenient: absent is_day => day variant

        var iconKey = condition switch
        {
            WeatherCondition.Clear => day ? WeatherIconKeys.ClearDay : WeatherIconKeys.ClearNight,
            WeatherCondition.PartlyCloudy => day ? WeatherIconKeys.PartlyCloudyDay : WeatherIconKeys.PartlyCloudyNight,
            WeatherCondition.Cloudy => WeatherIconKeys.Cloudy,
            WeatherCondition.Fog => WeatherIconKeys.Fog,
            WeatherCondition.Drizzle => WeatherIconKeys.Drizzle,
            WeatherCondition.Rain => WeatherIconKeys.Rain,
            WeatherCondition.RainShowers => day ? WeatherIconKeys.RainShowersDay : WeatherIconKeys.RainShowersNight,
            WeatherCondition.Snow => WeatherIconKeys.Snow,
            WeatherCondition.SnowShowers => day ? WeatherIconKeys.SnowShowersDay : WeatherIconKeys.SnowShowersNight,
            WeatherCondition.Thunderstorm => WeatherIconKeys.Thunderstorm,
            _ => WeatherIconKeys.Unknown,
        };

        var displayName = condition switch
        {
            WeatherCondition.Clear => "Clear",
            WeatherCondition.PartlyCloudy => "Partly cloudy",
            WeatherCondition.Cloudy => "Cloudy",
            WeatherCondition.Fog => "Fog",
            WeatherCondition.Drizzle => "Drizzle",
            WeatherCondition.Rain => "Rain",
            WeatherCondition.RainShowers => "Rain showers",
            WeatherCondition.Snow => "Snow",
            WeatherCondition.SnowShowers => "Snow showers",
            WeatherCondition.Thunderstorm => "Thunderstorm",
            _ => "Unknown",
        };

        return new WeatherConditionResult(condition, displayName, iconKey, recognized);
    }
}
