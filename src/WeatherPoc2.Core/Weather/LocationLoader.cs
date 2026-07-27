using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Persistence;

namespace WeatherPoc2.Core.Weather;

/// <summary>
/// Default <see cref="ILocationLoader"/>. Owns the Search History persistence read/write; the
/// <see cref="SearchHistory"/> stays pure. Registered as a singleton so every load path and the
/// startup hydration share one instance. Spec D1/D3.
/// </summary>
public sealed class LocationLoader : ILocationLoader
{
    public const string StorageKey = "search-history";

    private readonly SearchHistory _history;
    private readonly ILoadedLocation _loadedLocation;
    private readonly IPersistenceStore _store;
    private readonly ILogger<LocationLoader> _logger;

    public LocationLoader(
        SearchHistory history,
        ILoadedLocation loadedLocation,
        IPersistenceStore store,
        ILogger<LocationLoader> logger)
    {
        _history = history;
        _loadedLocation = loadedLocation;
        _store = store;
        _logger = logger;
    }

    public async Task LoadAsync(Location location, CancellationToken cancellationToken = default)
    {
        // Ordering is the contract (Spec load-ordering clause):
        // 1) record (raises Changed so the Recent list updates), 2) set the holder (so the on-appearing
        // weather fetch reads the just-loaded Location), 3) persist. Recording is independent of and
        // prior to any weather-fetch outcome (Spec D1).
        _history.Record(location);
        _loadedLocation.Set(location);
        await _store.SaveAsync<IReadOnlyList<Location>>(StorageKey, _history.Entries, cancellationToken);
        // SaveAsync fails soft inside the store (Warning-logged, not thrown — ADR-0003 / Feature-5 Seam 1),
        // so a persistence failure never blocks the load or the navigation that follows.
    }

    public async Task HydrateAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _store.LoadAsync<List<Location>>(StorageKey, cancellationToken);
        _history.Seed(stored ?? new List<Location>());
    }
}
