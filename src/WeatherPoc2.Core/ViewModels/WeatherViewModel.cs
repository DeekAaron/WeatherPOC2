using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Navigation;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.ViewModels;

/// <summary>
/// Screen coordinator for the weather page (Approach A / Spec D2). Owns the single
/// GetWeather call and distributes the one bundle to both child ViewModels — so Current
/// Conditions and the Hourly Forecast are mutually consistent by construction. On failure it
/// clears both and surfaces the single friendly message (Principle #1 fail-visible; Spec D3).
/// Refresh policy (load/focus/manual) is Feature 9 — this Feature loads on page appear only.
///
/// Feature 3 integration: the Location is no longer hard-coded — it comes from the shared
/// <see cref="ILoadedLocation"/> holder the search flow sets. A null holder is the launch state
/// (search shows first), so the coordinator no-ops rather than fetching. The always-available
/// magnifying-glass action routes back to search via <see cref="INavigator"/> (Reqs 19 / 21),
/// keeping the coordinator MAUI-free (Overriding Principle #2).
///
/// Feature 7 integration: the coordinator — not the display-only child — owns the Favourites star for
/// the loaded Location (Spec D5), because it holds the loaded Location. <see cref="ToggleFavouriteCommand"/>
/// marks/unmarks it (surfacing the exact full-list copy on <see cref="MarkResult.RefusedFull"/> through the
/// shared <see cref="ErrorMessage"/> channel, no-opping when nothing is loaded), and
/// <see cref="IsCurrentFavourite"/> tracks it — recomputed on load and on <see cref="IFavouritesService.Changed"/>.
/// </summary>
public sealed partial class WeatherViewModel : ObservableObject, IDisposable
{
    private const string FriendlyError =
        "Couldn't reach the weather service — check your connection and try again.";
    private const string FavouritesFullMessage = "Favourites are full — remove one first";

    private readonly IWeatherGateway _gateway;
    private readonly ILoadedLocation _loadedLocation;
    private readonly INavigator _navigator;
    private readonly IFavouritesService _favourites;
    private readonly ILogger<WeatherViewModel> _logger;

    public WeatherViewModel(
        IWeatherGateway gateway,
        ILoadedLocation loadedLocation,
        INavigator navigator,
        IFavouritesService favourites,
        CurrentConditionsViewModel currentConditions,
        HourlyForecastViewModel hourlyForecast,
        ILogger<WeatherViewModel> logger)
    {
        _gateway = gateway;
        _loadedLocation = loadedLocation;
        _navigator = navigator;
        _favourites = favourites;
        CurrentConditions = currentConditions;
        HourlyForecast = hourlyForecast;
        _logger = logger;
        _favourites.Changed += OnFavouritesChanged; // keep the star in sync with mark/unmark elsewhere
    }

    public CurrentConditionsViewModel CurrentConditions { get; }
    public HourlyForecastViewModel HourlyForecast { get; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isCurrentFavourite;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        // Feature 3: the Location comes from the loaded-Location holder, not a constant. Null is the
        // launch state (search is shown first) — nothing to fetch (Seam 2 defensive path).
        var location = _loadedLocation.Current;
        if (location is null)
            return;

        IsLoading = true;
        ErrorMessage = null;
        RefreshFavouriteState();                 // the star reflects the just-loaded Location
        try
        {
            var bundle = await _gateway.GetWeatherAsync(location, cancellationToken);
            CurrentConditions.Apply(bundle);
            HourlyForecast.Apply(bundle);
        }
        catch (WeatherUnavailableException)
        {
            // Gateway already logged the diagnostic detail; surface friendly copy only and clear both
            // views so no stale/partial panel or strip reads as current.
            CurrentConditions.Clear();
            HourlyForecast.Clear();
            ErrorMessage = FriendlyError;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Always-available magnifying-glass action → back to Location Search (Reqs 19 / 21).</summary>
    [RelayCommand]
    private Task OpenSearchAsync() => _navigator.GoToSearchAsync();

    /// <summary>Mark/unmark the loaded Location as a Favourite (Spec D5 — the star lives on the
    /// coordinator because it holds the loaded Location, not on the display-only child).</summary>
    [RelayCommand]
    private async Task ToggleFavouriteAsync(CancellationToken cancellationToken)
    {
        var location = _loadedLocation.Current;
        if (location is null)
            return;

        if (_favourites.IsFavourite(location))
        {
            await _favourites.UnmarkAsync(location, cancellationToken);
        }
        else
        {
            var result = await _favourites.MarkAsync(location, cancellationToken);
            if (result == MarkResult.RefusedFull)
                ErrorMessage = FavouritesFullMessage;
        }
        RefreshFavouriteState();
    }

    /// <summary>Recompute the star from the loaded Location; <c>false</c> when nothing is loaded.
    /// Public so a page can refresh it on appearing / after a load (Spec D5).</summary>
    public void RefreshFavouriteState()
    {
        var location = _loadedLocation.Current;
        IsCurrentFavourite = location is not null && _favourites.IsFavourite(location);
    }

    private void OnFavouritesChanged(object? sender, EventArgs e) => RefreshFavouriteState();

    /// <summary>
    /// Tears down the whole display graph together: the coordinator owns both transient children, so it
    /// propagates Dispose to each — detaching their <see cref="IUnitsService.Changed"/> subscriptions so
    /// the singleton units service no longer roots either child once the page disposes the coordinator.
    /// It also detaches its own <see cref="IFavouritesService.Changed"/> handler so the singleton
    /// Favourites service does not root this transient coordinator (Spec D5). Idempotent — detaching an
    /// already-detached handler is a no-op and each child guards its own second Dispose.
    /// </summary>
    public void Dispose()
    {
        _favourites.Changed -= OnFavouritesChanged;
        CurrentConditions.Dispose();
        HourlyForecast.Dispose();
    }
}
