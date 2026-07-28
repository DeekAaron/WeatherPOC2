namespace WeatherPoc2.Core.Weather;

/// <summary>
/// Pure, in-memory state machine over up to five user-curated Locations (Context.MD: Favourites),
/// most-recently-marked-first, distinct by <see cref="LocationIdentity"/>. No I/O — persistence is the
/// coordinator's job. Recency NEVER evicts: at capacity, a new mark is refused
/// (<see cref="MarkResult.RefusedFull"/>), never dropping an existing Favourite (Spec D3).
/// </summary>
public sealed class Favourites
{
    /// <summary>The block-on-overflow cap: five distinct Favourites, most-recently-marked-first.</summary>
    public const int Capacity = 5;

    private readonly List<Location> _entries = new();

    /// <summary>Most-recently-marked-first, 0..5, always distinct by identity.</summary>
    public IReadOnlyList<Location> Entries => _entries;

    /// <summary>Raised whenever <see cref="Entries"/> actually changes (never on a no-op).</summary>
    public event EventHandler? Changed;

    /// <summary>Insert at front; refuse at capacity (no eviction); no-op-with-signal if already present.</summary>
    public MarkResult Mark(Location location)
    {
        if (_entries.Any(e => LocationIdentity.Same(e, location)))
            return MarkResult.AlreadyFavourite; // no reorder, no change, no raise
        if (_entries.Count >= Capacity)
            return MarkResult.RefusedFull;      // recency never evicts, no change, no raise

        _entries.Insert(0, location);           // most-recently-marked-first
        RaiseChanged();
        return MarkResult.Marked;
    }

    /// <summary>Remove the identity-equal entry. Returns false (no change) if not a Favourite.</summary>
    public bool Unmark(Location location)
    {
        var index = _entries.FindIndex(e => LocationIdentity.Same(e, location));
        if (index < 0)
            return false;                       // absent → no change, no raise
        _entries.RemoveAt(index);
        RaiseChanged();
        return true;
    }

    /// <summary>True iff an identity-equal Location is currently a Favourite.</summary>
    public bool IsFavourite(Location location)
        => _entries.Any(e => LocationIdentity.Same(e, location));

    /// <summary>
    /// Hydrate from persistence. NORMALISES rather than trusts: dedupe by identity keeping the
    /// front-most occurrence, then cap to the first five — so a parseable-but-invalid document
    /// (duplicates, more than five entries, or value-degenerate coordinates) can never violate the
    /// invariant. Total — never throws for any input (the persistence trust boundary, Spec D3 security).
    /// </summary>
    public void Seed(IEnumerable<Location> entries)
    {
        _entries.Clear();
        foreach (var candidate in entries ?? Enumerable.Empty<Location>())
        {
            if (candidate is null)
                continue;                       // a null element is not a valid Location → drop it
            if (_entries.Count >= Capacity)
                break;
            if (!_entries.Any(e => LocationIdentity.Same(e, candidate)))
                _entries.Add(candidate);
        }
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
