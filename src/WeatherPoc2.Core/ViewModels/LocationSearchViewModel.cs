using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Navigation;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.ViewModels;

public sealed partial class LocationSearchViewModel : ObservableObject
{
    private const string NoMatchMessage = "No matching places found";
    private const string FriendlyError =
        "Couldn't reach the search service — check your connection and try again.";

    private readonly IWeatherGateway _gateway;
    private readonly ILoadedLocation _loadedLocation;
    private readonly INavigator _navigator;
    private readonly ILogger<LocationSearchViewModel> _logger;

    public LocationSearchViewModel(
        IWeatherGateway gateway,
        ILoadedLocation loadedLocation,
        INavigator navigator,
        ILogger<LocationSearchViewModel> logger)
    {
        _gateway = gateway;
        _loadedLocation = loadedLocation;
        _navigator = navigator;
        _logger = logger;
    }

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<SearchCandidate> Candidates { get; } = new();

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
        // Mint the resolved Location from the picked Candidate, set the shared holder, THEN navigate —
        // Current Conditions reads ILoadedLocation.Current on appearing (Seam 2 ordering).
        var location = new Location(candidate.Latitude, candidate.Longitude, candidate.Label, candidate.Id);
        _loadedLocation.Set(location);
        await _navigator.GoToCurrentConditionsAsync();
    }
}
