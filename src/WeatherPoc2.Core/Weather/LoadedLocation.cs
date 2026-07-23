namespace WeatherPoc2.Core.Weather;

/// <summary>In-memory <see cref="ILoadedLocation"/>. Registered as a singleton so the search flow and
/// Current Conditions share one instance. No persistence (Feature 3 scope).</summary>
public sealed class LoadedLocation : ILoadedLocation
{
    public Location? Current { get; private set; }
    public void Set(Location location) => Current = location;
}
