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
    private readonly ILogger<CurrentConditionsViewModel> _logger;

    public CurrentConditionsViewModel(IWeatherGateway gateway, ILogger<CurrentConditionsViewModel> logger)
    {
        _gateway = gateway;
        _logger = logger;
    }

    [ObservableProperty] private string _temperatureDisplay = string.Empty;
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
        }
        catch (WeatherUnavailableException)
        {
            // Gateway has already logged the diagnostic detail; surface friendly copy (fail-visible).
            TemperatureDisplay = string.Empty;
            ErrorMessage = FriendlyError;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
