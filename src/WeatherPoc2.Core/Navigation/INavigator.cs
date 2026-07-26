namespace WeatherPoc2.Core.Navigation;

/// <summary>
/// Screen navigation the ViewModels request without depending on MAUI (Overriding Principle #2,
/// MVVM-only — the ViewModels stay unit-testable). The app head implements it over Shell routing.
/// </summary>
public interface INavigator
{
    /// <summary>Show Current Conditions for the currently-loaded Location.</summary>
    Task GoToCurrentConditionsAsync();

    /// <summary>Show the Location Search screen (the always-available magnifying-glass action).</summary>
    Task GoToSearchAsync();
}
