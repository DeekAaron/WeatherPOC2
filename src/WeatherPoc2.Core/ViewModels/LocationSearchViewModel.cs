using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Navigation;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.ViewModels;

public sealed partial class LocationSearchViewModel : ObservableObject, IDisposable
{
    private const string NoMatchMessage = "No matching places found";
    private const string FriendlyError =
        "Couldn't reach the search service — check your connection and try again.";

    private readonly IWeatherGateway _gateway;
    private readonly ILocationLoader _loader;
    private readonly SearchHistory _history;
    private readonly INavigator _navigator;
    private readonly IFavouritesService _favourites;
    private readonly ILogger<LocationSearchViewModel> _logger;

    // Changed handlers held in fields so they are removable in Dispose. This VM is transient while
    // SearchHistory and IFavouritesService are singletons, so a method-group/lambda subscription would
    // root every dead instance forever (mirrors the CurrentConditionsViewModel IDisposable detach pattern,
    // Story #81).
    private readonly EventHandler _onHistoryChanged;
    private readonly EventHandler _onFavouritesChanged;
    private bool _disposed;

    public LocationSearchViewModel(
        IWeatherGateway gateway,
        ILocationLoader loader,
        SearchHistory history,
        INavigator navigator,
        IFavouritesService favourites,
        ILogger<LocationSearchViewModel> logger)
    {
        _gateway = gateway;
        _loader = loader;
        _history = history;
        _navigator = navigator;
        _favourites = favourites;
        _logger = logger;

        // Recent and Favourites each mirror their state machine. A user mutation raises Changed on the UI
        // thread already; the startup HydrateAsync continuation is marshalled onto the UI/dispatcher thread
        // by the App head (Spec UI-thread-affinity clause). Core stays MAUI-free — no dispatcher reference
        // here; the VM just rebuilds whenever Changed fires (and once now, for already-hydrated state).
        _onHistoryChanged = OnHistoryChanged;
        _history.Changed += _onHistoryChanged;
        RebuildRecent();

        _onFavouritesChanged = OnFavouritesChanged;
        _favourites.Changed += _onFavouritesChanged;
        RebuildFavourites();
    }

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<SearchCandidate> Candidates { get; } = new();

    /// <summary>The Search History as a bound, most-recent-first list of Locations (Context.MD: Recent).</summary>
    public ObservableCollection<Location> Recent { get; } = new();

    /// <summary>The Favourites as a bound, most-recently-marked-first list of Locations (Context.MD: Favourites).
    /// Empty when there are no Favourites — the page renders no list section (Spec D5).</summary>
    public ObservableCollection<Location> Favourites { get; } = new();

    private void OnHistoryChanged(object? sender, EventArgs e) => RebuildRecent();

    private void RebuildRecent()
    {
        Recent.Clear();
        foreach (var location in _history.Entries)
            Recent.Add(location);
    }

    private void OnFavouritesChanged(object? sender, EventArgs e) => RebuildFavourites();

    private void RebuildFavourites()
    {
        Favourites.Clear();
        foreach (var location in _favourites.Entries)
            Favourites.Add(location);
    }

    [RelayCommand]
    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        var name = Query?.Trim() ?? string.Empty;
        if (name.Length == 0)
            return; // empty/whitespace query fires no call

        StatusMessage = null;
        ErrorMessage = null;
        try
        {
            var candidates = await _gateway.SearchAsync(name, cancellationToken);
            Candidates.Clear();
            foreach (var c in candidates)
                Candidates.Add(c);
            StatusMessage = Candidates.Count == 0 ? NoMatchMessage : null;
        }
        catch (LocationSearchUnavailableException)
        {
            // Gateway has already logged the diagnostic detail; surface friendly copy (fail-visible).
            Candidates.Clear();
            ErrorMessage = FriendlyError;
        }
    }

    [RelayCommand]
    private async Task SelectCandidateAsync(SearchCandidate candidate)
    {
        // Mint the resolved Location from the picked Candidate, then load it through the single
        // coordinator (records to history -> sets the holder -> persists) and navigate. The loader
        // owns ILoadedLocation now — the VM never sets it directly (Spec D1/D4).
        var location = new Location(candidate.Latitude, candidate.Longitude, candidate.Label, candidate.Id);
        await _loader.LoadAsync(location);
        await _navigator.GoToCurrentConditionsAsync();
    }

    [RelayCommand]
    private async Task SelectRecentAsync(Location location)
    {
        // Tapping a Recent entry is a load like any other: same coordinator, same navigation, no gateway
        // search. Reloading an existing entry moves it to most-recent (SearchHistory.Record dedupes/moves-to-front).
        await _loader.LoadAsync(location);
        await _navigator.GoToCurrentConditionsAsync();
    }

    [RelayCommand]
    private async Task OpenFavouriteAsync(Location location)
    {
        // Opening a Favourite is the third load path (Spec D1) — the same single choke point as picking a
        // Candidate or tapping Recent: LoadAsync records to history, sets the holder, and persists (Feature 6's
        // ordering), so the opened Favourite becomes the most-recent history entry for free (PRD-40). The VM
        // never touches ILoadedLocation and never calls the gateway.
        await _loader.LoadAsync(location);
        await _navigator.GoToCurrentConditionsAsync();
    }

    /// <summary>
    /// Detaches the <see cref="SearchHistory.Changed"/> and <see cref="IFavouritesService.Changed"/>
    /// subscriptions so the singleton history / favourites service no longer roots this transient
    /// search-page VM once its page tears down (no leak, no rebuilding a dead instance's Recent /
    /// Favourites). Idempotent — safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _history.Changed -= _onHistoryChanged;
        _favourites.Changed -= _onFavouritesChanged;
    }
}
