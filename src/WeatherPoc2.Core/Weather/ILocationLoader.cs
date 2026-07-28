namespace WeatherPoc2.Core.Weather;

/// <summary>
/// The single choke point every Location *load* passes through (Context.MD: picking a Candidate,
/// tapping a Recent entry, later opening a Favourite). Records the load into <see cref="SearchHistory"/>,
/// sets the shared <see cref="ILoadedLocation"/> holder, and persists — so no load path can bypass
/// history and every screen reads the same loaded Location. Spec D1.
/// </summary>
public interface ILocationLoader
{
    /// <summary>Record -> set holder -> persist, in that order. The caller navigates AFTER this returns.</summary>
    Task LoadAsync(Location location, CancellationToken cancellationToken = default);

    /// <summary>Read the persisted history once at startup and seed the state machine.</summary>
    Task HydrateAsync(CancellationToken cancellationToken = default);
}
