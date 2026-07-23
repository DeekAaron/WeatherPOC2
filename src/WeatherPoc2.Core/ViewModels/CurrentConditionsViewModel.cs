using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.ViewModels;

public sealed partial class CurrentConditionsViewModel : ObservableObject
{
    private const string FriendlyError =
        "Couldn't reach the weather service — check your connection and try again.";

    private readonly IWeatherGateway _gateway;
    private readonly WeatherConditionMapper _mapper;
    private readonly ILogger<CurrentConditionsViewModel> _logger;

    public CurrentConditionsViewModel(
        IWeatherGateway gateway,
        WeatherConditionMapper mapper,
        ILogger<CurrentConditionsViewModel> logger)
    {
        _gateway = gateway;
        _mapper = mapper;
        _logger = logger;
    }

    [ObservableProperty] private string _temperatureDisplay = string.Empty;
    [ObservableProperty] private string? _chanceOfRainDisplay;
    [ObservableProperty] private string? _windSpeedDisplay;
    [ObservableProperty] private string? _conditionText;
    [ObservableProperty] private string? _iconSource;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isLoading;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var bundle = await _gateway.GetWeatherAsync(Location.LondonGb, cancellationToken);

            TemperatureDisplay = bundle.CurrentTemperatureCelsius.ToString("0.0", CultureInfo.InvariantCulture) + " °C";
            ChanceOfRainDisplay = $"{bundle.CurrentChanceOfRainPercent}%";
            WindSpeedDisplay = bundle.CurrentWindSpeedKmh.ToString("0.#", CultureInfo.InvariantCulture) + " km/h";

            // Derive the condition word + day/night icon for display (mapper is pure; ADR-0001 derive-for-display).
            var condition = _mapper.Map(bundle.CurrentWeatherCode, bundle.IsDay);
            ConditionText = condition.DisplayName;
            IconSource = $"{condition.IconKey}.png";

            // Fail-visible (Principle #1): the lenient fall-backs are logged as Warnings, never silent.
            if (!condition.Recognized)
                _logger.LogWarning(
                    "Current Conditions: unrecognized/absent weather_code {Code} → Unknown icon", bundle.CurrentWeatherCode);
            if (bundle.IsDay is null)
                _logger.LogWarning("Current Conditions: is_day absent → defaulting to the day icon variant");
        }
        catch (WeatherUnavailableException)
        {
            // Gateway has already logged the diagnostic detail; surface fixed friendly copy only (fail-visible,
            // no upstream/internal detail leaked). Clear every measure so no stale/partial panel reads as current.
            TemperatureDisplay = string.Empty;
            ChanceOfRainDisplay = null;
            WindSpeedDisplay = null;
            ConditionText = null;
            IconSource = null;
            ErrorMessage = FriendlyError;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
