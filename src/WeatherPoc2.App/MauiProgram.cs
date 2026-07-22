using Microsoft.Extensions.Logging;
using WeatherPoc2.App.Views;
using WeatherPoc2.Core.DependencyInjection;

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

        builder.Services.AddWeatherPoc2Core();       // Gateway + HttpClient + ViewModel
        builder.Services.AddTransient<CurrentConditionsPage>();
        builder.Services.AddTransient<AppShell>();

        return builder.Build();
    }
}
