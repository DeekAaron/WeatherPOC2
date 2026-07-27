namespace WeatherPoc2.Core.Persistence;

/// <summary>
/// Supplies the base directory the <see cref="IPersistenceStore"/> writes under. Core depends only on
/// this abstraction; the MAUI head implements it with <c>FileSystem.AppDataDirectory</c> (Seam 2),
/// and tests inject a temp directory. This keeps the JSON logic host-agnostic and Tier-1 testable.
/// </summary>
public interface IAppDataPathProvider
{
    string GetAppDataDirectory();
}
