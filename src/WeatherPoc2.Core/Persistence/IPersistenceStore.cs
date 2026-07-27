namespace WeatherPoc2.Core.Persistence;

/// <summary>
/// The durable-state seam (ADR-0003): one JSON document per <c>key</c>. Scoped to Units
/// today; Search History and Favourites extend it with their own keys. Read is fail-soft (absent or
/// unreadable → <c>null</c>, caller uses defaults); write never throws to the caller (D5). The
/// <c>key</c> must be a single path segment — the store rejects a separator/traversal/rooted key
/// (<see cref="System.ArgumentException"/>) before any file access.
/// </summary>
public interface IPersistenceStore
{
    Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}
