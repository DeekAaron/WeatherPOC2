using Microsoft.Extensions.Logging;
using WeatherPoc2.App.Navigation;
using WeatherPoc2.App.Views;
using WeatherPoc2.Core.DependencyInjection;
using WeatherPoc2.Core.Navigation;

namespace WeatherPoc2.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddWeatherPoc2Core();       // Gateway + HttpClient + ViewModels + ILoadedLocation

        // INavigator is deliberately NOT registered in Core (it is a MAUI type) — the app head supplies it.
        builder.Services.AddSingleton<INavigator, MauiNavigator>();

        builder.Services.AddTransient<CurrentConditionsPage>();
        builder.Services.AddTransient<LocationSearchPage>();
        builder.Services.AddTransient<AppShell>();

        return builder.Build();
    }
}
