namespace WeatherPoc2.Core.Weather;

/// <summary>
/// Holds the one Location currently loaded (Context.MD: "the loaded Location" — the event Search
/// History and Favourites will later key on). In-memory only; nothing is persisted in Feature 3, so
/// <see cref="Current"/> is null again on every launch. Null = nothing loaded (the launch state).
/// </summary>
public interface ILoadedLocation
{
    Location? Current { get; }
    void Set(Location location);
}
