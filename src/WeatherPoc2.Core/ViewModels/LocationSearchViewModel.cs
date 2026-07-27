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
    private readonly ILogger<LocationSearchViewModel> _logger;

    // The Changed handler held in a field so it is removable in Dispose. This VM is transient while
    // SearchHistory is a singleton, so a method-group/lambda subscription would root every dead instance
    // forever (mirrors the CurrentConditionsViewModel IDisposable detach pattern, Story #81).
    private readonly EventHandler _onHistoryChanged;
    private bool _disposed;

    public LocationSearchViewModel(
        IWeatherGateway gateway,
        ILocationLoader loader,
        SearchHistory history,
        INavigator navigator,
        ILogger<LocationSearchViewModel> logger)
    {
        _gateway = gateway;
        _loader = loader;
        _history = history;
        _navigator = navigator;
        _logger = logger;

        // Recent mirrors the pure state machine. A user load raises Changed on the UI thread already;
        // the startup HydrateAsync continuation is marshalled onto the UI/dispatcher thread by the App
        // head (Spec UI-thread-affinity clause). Core stays MAUI-free — no dispatcher reference here;
        // the VM just rebuilds Recent whenever Changed fires (and once now, for an already-hydrated history).
        _onHistoryChanged = OnHistoryChanged;
        _history.Changed += _onHistoryChanged;
        RebuildRecent();
    }

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<SearchCandidate> Candidates { get; } = new();

    /// <summary>The Search History as a bound, most-recent-first list of Locations (Context.MD: Recent).</summary>
    public ObservableCollection<Location> Recent { get; } = new();

    private void OnHistoryChanged(object? sender, EventArgs e) => RebuildRecent();

    private void RebuildRecent()
    {
        Recent.Clear();
        foreach (var location in _history.Entries)
            Recent.Add(location);
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

    /// <summary>
    /// Detaches the <see cref="SearchHistory.Changed"/> subscription so the singleton history no longer
    /// roots this transient search-page VM once its page tears down (no leak, no rebuilding a dead
    /// instance's Recent). Idempotent — safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _history.Changed -= _onHistoryChanged;
    }
}
