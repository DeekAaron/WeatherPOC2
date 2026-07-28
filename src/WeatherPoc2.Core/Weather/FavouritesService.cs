using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Persistence;

namespace WeatherPoc2.Core.Weather;

/// <summary>
/// Singleton <see cref="IFavouritesService"/>. Owns the pure <see cref="Favourites"/> machine and its
/// persistence under the <c>favourites</c> key. A mutator changes in-memory state (raising Changed
/// synchronously via the machine) and then persists only when the state actually changed — a save
/// failure is logged by the store, never surfaced (ADR-0003 / Principle 1).
/// </summary>
/// <remarks>
/// <para><b>Thread-affinity contract (Spec <i>UI-thread affinity</i> clause).</b> <see cref="Changed"/>
/// is forwarded <b>synchronously</b> on whatever thread called the mutator — this type does not marshal.
/// Its handlers mutate MAUI data-bound state, so the <i>caller</i> must invoke on the UI/dispatcher
/// thread: a star tap already runs on the UI thread; the startup <see cref="HydrateAsync"/> raise is
/// marshalled by the App-head hook resuming on the UI thread (mirrors <c>UnitsService</c>).</para>
/// <para><b>Security.</b> This service's own <see cref="ILogger"/> emits nothing on the persistence
/// path — no <see cref="Location"/> latitude, longitude, or <see cref="Location.Label"/> reaches the log
/// sink. Write-failure logging is the store's job and carries the storage key + exception type only
/// (Story security AC; mirrors the Gateway's coordinate-logging control).</para>
/// </remarks>
public sealed class FavouritesService : IFavouritesService
{
    private const string StorageKey = "favourites";

    private readonly Favourites _favourites;
    private readonly IPersistenceStore _store;
    private readonly ILogger<FavouritesService> _logger;

    public FavouritesService(Favourites favourites, IPersistenceStore store, ILogger<FavouritesService> logger)
    {
        _favourites = favourites;
        _store = store;
        _logger = logger;
        _favourites.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty); // forward the machine's event
    }

    public IReadOnlyList<Location> Entries => _favourites.Entries;

    public bool IsFavourite(Location location) => _favourites.IsFavourite(location);

    public event EventHandler? Changed;

    public async Task HydrateAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await _store.LoadAsync<List<Location>>(StorageKey, cancellationToken);
        _favourites.Seed(loaded ?? new List<Location>()); // null (absent/malformed) → empty; Seed normalises
    }

    public async Task<MarkResult> MarkAsync(Location location, CancellationToken cancellationToken = default)
    {
        var result = _favourites.Mark(location);
        if (result == MarkResult.Marked)
            await PersistAsync(cancellationToken); // only a real change is written
        return result;
    }

    public async Task UnmarkAsync(Location location, CancellationToken cancellationToken = default)
    {
        if (_favourites.Unmark(location))
            await PersistAsync(cancellationToken); // only a real removal is written
    }

    private Task PersistAsync(CancellationToken cancellationToken)
        => _store.SaveAsync<IReadOnlyList<Location>>(StorageKey, _favourites.Entries, cancellationToken); // store logs a write failure (D4)
}
