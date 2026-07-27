using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.App;

public partial class App : Application
{
    public App(AppShell shell, ILocationLoader loader)
    {
        InitializeComponent();
        MainPage = shell;

        // Startup hydration of Search History (Spec D4 + the UI-thread-affinity clause). Dispatched onto
        // the MAUI UI thread so the Seed/Changed continuation that rebuilds the bound Recent collection is
        // UI-thread-safe. Fire-and-forget: HydrateAsync's inner store read yields for I/O, so this is not a
        // synchronous UI block (Overriding Principle #4); the store fails soft (ADR-0003) so it cannot throw.
        Dispatcher.DispatchAsync(() => loader.HydrateAsync());
    }
}
