using Microsoft.Extensions.DependencyInjection;
using WeatherPoc2.Core.Persistence;
using WeatherPoc2.Core.Units;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OS-agnostic Weather graph: the named HttpClient for the Gateway, the Gateway
    /// itself, the pure stateless singletons (WeatherConditionMapper, HourlyWindow), the shared
    /// in-memory ILoadedLocation holder, the shared SearchHistory state machine and its ILocationLoader
    /// coordinator (the single load choke point), and the ViewModels — the WeatherViewModel coordinator plus
    /// its two display-only children (CurrentConditions, HourlyForecast) and the LocationSearchViewModel
    /// as transients. INavigator is NOT registered here (it is a MAUI type — the app head supplies it).
    /// The MAUI head calls this, then adds INavigator and the platform Pages. Callers must have added
    /// logging (AddLogging / MAUI default).
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
        services.AddSingleton<WeatherConditionMapper>();            // pure + stateless
        services.AddSingleton<HourlyWindow>();                      // pure + stateless
        services.AddSingleton<ILoadedLocation, LoadedLocation>();   // shared across the app (search flow ↔ Current Conditions)
        services.AddSingleton<SearchHistory>();                     // pure state machine, one shared instance
        services.AddSingleton<ILocationLoader, LocationLoader>();   // the single load choke point (owns history persistence)
        services.AddTransient<CurrentConditionsViewModel>();
        services.AddTransient<HourlyForecastViewModel>();
        services.AddTransient<WeatherViewModel>();
        services.AddTransient<LocationSearchViewModel>();

        // Units + Persistence (ADR-0001 / ADR-0003). IAppDataPathProvider is NOT registered here —
        // it is MAUI-specific and host-supplied by MauiProgram; the DI test injects a fake provider.
        services.AddSingleton<IPersistenceStore, JsonPersistenceStore>();
        services.AddSingleton<IUnitsService, UnitsService>();   // single owner of the units state
        services.AddSingleton<UnitFormatter>();                 // pure + stateless
        services.AddTransient<SettingsViewModel>();             // the dedicated Settings/Units screen VM
        return services;
    }
}
