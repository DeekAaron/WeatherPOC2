using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using WeatherPoc2.App;

namespace WeatherPoc2.App.WinUI;

/// <summary>
/// The WinUI application host. Its presence gives the Windows head a generated entry point
/// (fixes CS5001) and hands control to the shared MAUI app via <see cref="MauiProgram"/>.
/// </summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
