using Microsoft.Extensions.DependencyInjection;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OS-agnostic Weather graph: the named HttpClient for the Gateway,
    /// the Gateway itself, and the Current Conditions ViewModel. The MAUI head calls this,
    /// then adds the platform Page. Callers must have added logging (AddLogging / MAUI default).
    /// </summary>
    public static IServiceCollection AddWeatherPoc2Core(this IServiceCollection services)
    {
        // Bound the app's only trust boundary against a slow-dripping or oversized response:
        // a 15 s timeout fails visible instead of holding the spinner for the 100 s framework
        // default (expiry surfaces as TaskCanceledException); a 1 MB buffer cap bounds a hostile
        // oversized body (exceeding it surfaces as HttpRequestException). Both convert to the
        // friendly error via the Gateway's transport catch.
        services.AddHttpClient(OpenMeteoGateway.HttpClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(15);
            c.MaxResponseContentBufferSize = 1_048_576;
        });
        services.AddSingleton<IWeatherGateway, OpenMeteoGateway>();
        services.AddTransient<CurrentConditionsViewModel>();
        return services;
    }
}
