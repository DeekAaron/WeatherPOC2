using WeatherPoc2.Core.Persistence;

namespace WeatherPoc2.App;

/// <summary>
/// MAUI implementation of <see cref="IAppDataPathProvider"/> (Persistence Seam 2): the per-user,
/// per-app writable directory the JSON persistence store writes under. Host-supplied because
/// <c>FileSystem</c> is a MAUI type, so Core stays MAUI-free and Tier-1 testable against a temp dir.
/// </summary>
internal sealed class MauiAppDataPathProvider : IAppDataPathProvider
{
    public string GetAppDataDirectory() => FileSystem.Current.AppDataDirectory;
}
