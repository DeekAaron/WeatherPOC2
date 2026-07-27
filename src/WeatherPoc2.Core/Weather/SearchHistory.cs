namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The four most recently *loaded* Locations (Context.MD: Search History), recency-ordered and keyed
/// by Location identity. Pure and in-memory — no I/O. Fed exclusively through <see cref="ILocationLoader"/>;
/// persistence and hydration are the coordinator's concern (Spec D1/D2/D3). Raises <see cref="Changed"/>
/// whenever <see cref="Entries"/> changes so a bound view can rebuild.
/// </summary>
public sealed class SearchHistory
{
    public const int Capacity = 4;

    private readonly List<Location> _entries = new();

    /// <summary>Most-recent-first, 0..4, always distinct by identity.</summary>
    public IReadOnlyList<Location> Entries => _entries;

    public event EventHandler? Changed;

    /// <summary>Record a load: dedupe-by-identity -> move-to-front -> cap 4 (evict the tail).</summary>
    public void Record(Location location)
    {
        var existingIndex = _entries.FindIndex(e => SameLocation(e, location));
        var alreadyFrontIdentical = existingIndex == 0 && _entries[0] == location;
        if (alreadyFrontIdentical)
            return; // no observable change

        if (existingIndex >= 0)
            _entries.RemoveAt(existingIndex);
        _entries.Insert(0, location);
        if (_entries.Count > Capacity)
            _entries.RemoveRange(Capacity, _entries.Count - Capacity);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Hydrate from persistence. Normalises rather than trusts: dedupe by identity keeping the
    /// front-most occurrence, then cap to the first <see cref="Capacity"/> — so a parseable-but-invalid
    /// stored list (duplicates, over-length) can never violate the in-memory invariant.
    /// </summary>
    public void Seed(IEnumerable<Location> entries)
    {
        _entries.Clear();
        foreach (var candidate in entries)
        {
            if (_entries.Count >= Capacity)
                break;
            if (!_entries.Any(e => SameLocation(e, candidate)))
                _entries.Add(candidate);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The single definition of Location identity (Spec D2): Open-Meteo id when both present,
    /// else exact coordinates; Label is never part of identity.</summary>
    private static bool SameLocation(Location a, Location b) =>
        a.OpenMeteoId is int ida && b.OpenMeteoId is int idb
            ? ida == idb
            : a.Latitude == b.Latitude && a.Longitude == b.Longitude;
}
