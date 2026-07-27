using Microsoft.Extensions.Logging;
using WeatherPoc2.App.Navigation;
using WeatherPoc2.App.Views;
using WeatherPoc2.Core.DependencyInjection;
using WeatherPoc2.Core.Navigation;
using WeatherPoc2.Core.Persistence;

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

        // Host-supplied path provider (Persistence Seam 2). Core leaves IAppDataPathProvider
        // unregistered on purpose; the MAUI head wires the real FileSystem.AppDataDirectory here.
        // Must precede AddWeatherPoc2Core so JsonPersistenceStore / LocationLoader / IUnitsService resolve.
        builder.Services.AddSingleton<IAppDataPathProvider, MauiAppDataPathProvider>();

        builder.Services.AddWeatherPoc2Core();       // Gateway + HttpClient + ViewModels + ILoadedLocation + Search History + Units

        // INavigator is deliberately NOT registered in Core (it is a MAUI type) — the app head supplies it.
        builder.Services.AddSingleton<INavigator, MauiNavigator>();

        builder.Services.AddTransient<CurrentConditionsPage>();
        builder.Services.AddTransient<LocationSearchPage>();
        builder.Services.AddTransient<AppShell>();

        return builder.Build();
    }
}
