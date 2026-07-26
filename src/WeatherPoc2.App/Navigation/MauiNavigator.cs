using WeatherPoc2.Core.Navigation;

namespace WeatherPoc2.App.Navigation;

/// <summary>
/// The MAUI-head implementation of <see cref="INavigator"/> (Seam 3): the Core ViewModels request
/// navigation and this drives MAUI Shell's absolute routing. The route strings here MUST stay in
/// lockstep with the <c>Route=</c> values on the <c>ShellContent</c> items in AppShell.xaml — that
/// match is exactly what this Feature's platform-verification story exists to prove.
/// </summary>
public sealed class MauiNavigator : INavigator
{
    public Task GoToCurrentConditionsAsync() => Shell.Current.GoToAsync("//current");

    public Task GoToSearchAsync() => Shell.Current.GoToAsync("//search");
}
