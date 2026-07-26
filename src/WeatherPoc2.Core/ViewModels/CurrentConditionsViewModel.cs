using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.ViewModels;

/// <summary>
/// Display-only ViewModel for the Current Conditions panel. It no longer fetches — the parent
/// <c>WeatherViewModel</c> coordinator owns the single GetWeather call and pushes the bundle in via
/// <see cref="Apply"/> (or blanks the panel via <see cref="Clear"/> on failure). Derives the
/// condition word + day/night icon through the pure mapper (ADR-0001 derive-for-display).
/// </summary>
public sealed partial class CurrentConditionsViewModel : ObservableObject
{
    private readonly WeatherConditionMapper _mapper;
    private readonly ILogger<CurrentConditionsViewModel> _logger;

    public CurrentConditionsViewModel(
        WeatherConditionMapper mapper,
        ILogger<CurrentConditionsViewModel> logger)
    {
        _mapper = mapper;
        _logger = logger;
    }

    [ObservableProperty] private string _temperatureDisplay = string.Empty;
    [ObservableProperty] private string? _chanceOfRainDisplay;
    [ObservableProperty] private string? _windSpeedDisplay;
    [ObservableProperty] private string? _conditionText;
    [ObservableProperty] private string? _iconSource;

    /// <summary>
    /// Populates the five display properties from the shared bundle the coordinator fetched. Derives
    /// the condition word + day/night icon via the pure mapper; each lenient fall-back (unrecognized/
    /// absent weather_code, absent is_day) is logged as a Warning — never silent (Principle #1).
    /// </summary>
    public void Apply(WeatherBundle bundle)
    {
        TemperatureDisplay = bundle.CurrentTemperatureCelsius.ToString("0.0", CultureInfo.InvariantCulture) + " °C";
        ChanceOfRainDisplay = $"{bundle.CurrentChanceOfRainPercent}%";
        WindSpeedDisplay = bundle.CurrentWindSpeedKmh.ToString("0.#", CultureInfo.InvariantCulture) + " km/h";

        var condition = _mapper.Map(bundle.CurrentWeatherCode, bundle.IsDay);
        ConditionText = condition.DisplayName;
        IconSource = $"{condition.IconKey}.png";

        // Fail-visible (Principle #1): lenient fall-backs are logged as Warnings, never silent.
        if (!condition.Recognized)
            _logger.LogWarning(
                "Current Conditions: unrecognized/absent weather_code {Code} → Unknown icon", bundle.CurrentWeatherCode);
        if (bundle.IsDay is null)
            _logger.LogWarning("Current Conditions: is_day absent → defaulting to the day icon variant");
    }

    /// <summary>
    /// Blanks every display so no stale/partial panel reads as current. The coordinator calls this on
    /// <c>WeatherUnavailableException</c>, alongside surfacing the single friendly error itself.
    /// </summary>
    public void Clear()
    {
        TemperatureDisplay = string.Empty;
        ChanceOfRainDisplay = null;
        WindSpeedDisplay = null;
        ConditionText = null;
        IconSource = null;
    }
}
