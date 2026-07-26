using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.ViewModels;

/// <summary>
/// Screen coordinator for the weather page (Approach A / Spec D2). Owns the single
/// GetWeather call and distributes the one bundle to both child ViewModels — so Current
/// Conditions and the Hourly Forecast are mutually consistent by construction. On failure it
/// clears both and surfaces the single friendly message (Principle #1 fail-visible; Spec D3).
/// Refresh policy (load/focus/manual) is Feature 9 — this Feature loads on page appear only.
/// </summary>
public sealed partial class WeatherViewModel : ObservableObject
{
    private const string FriendlyError =
        "Couldn't reach the weather service — check your connection and try again.";

    private readonly IWeatherGateway _gateway;
    private readonly ILogger<WeatherViewModel> _logger;

    public WeatherViewModel(
        IWeatherGateway gateway,
        CurrentConditionsViewModel currentConditions,
        HourlyForecastViewModel hourlyForecast,
        ILogger<WeatherViewModel> logger)
    {
        _gateway = gateway;
        CurrentConditions = currentConditions;
        HourlyForecast = hourlyForecast;
        _logger = logger;
    }

    public CurrentConditionsViewModel CurrentConditions { get; }
    public HourlyForecastViewModel HourlyForecast { get; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var bundle = await _gateway.GetWeatherAsync(Location.LondonGb, cancellationToken);
            CurrentConditions.Apply(bundle);
            HourlyForecast.Apply(bundle);
        }
        catch (WeatherUnavailableException)
        {
            // Gateway already logged the diagnostic detail; surface friendly copy only and clear both
            // views so no stale/partial panel or strip reads as current.
            CurrentConditions.Clear();
            HourlyForecast.Clear();
            ErrorMessage = FriendlyError;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
