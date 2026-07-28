namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The single owner of "which Locations are Favourites". Holds the <see cref="Favourites"/> state
/// machine, loads/saves it through the Persistence Store under the <c>favourites</c> key, and raises
/// <see cref="Changed"/> so the star toggle and the Favourites list re-render. Mirrors
/// <c>IUnitsService</c> / Feature 6's load coordinator (Spec D5).
/// </summary>
public interface IFavouritesService
{
    /// <summary>Most-recently-marked-first, 0..5, always distinct by identity (delegates to the machine).</summary>
    IReadOnlyList<Location> Entries { get; }

    /// <summary>True iff an identity-equal Location is currently a Favourite (delegates to the machine).</summary>
    bool IsFavourite(Location location);

    /// <summary>Raised whenever the Favourites set actually changes (forwards the machine's event).</summary>
    event EventHandler? Changed;

    /// <summary>Read the persisted list once at startup and seed the machine (empty on absent/malformed).</summary>
    Task HydrateAsync(CancellationToken cancellationToken = default);

    /// <summary>Mark the Location; persist only on <see cref="MarkResult.Marked"/>. Returns the outcome.</summary>
    Task<MarkResult> MarkAsync(Location location, CancellationToken cancellationToken = default);

    /// <summary>Unmark the Location by identity; persist only on a real removal.</summary>
    Task UnmarkAsync(Location location, CancellationToken cancellationToken = default);
}
