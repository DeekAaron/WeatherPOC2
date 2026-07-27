using WeatherPoc2.Core.Persistence;

namespace WeatherPoc2.Core.Tests.Support;

/// <summary>
/// An <see cref="IAppDataPathProvider"/> over a fresh, disposable temp directory — the real-I/O
/// substrate for the store seam tests. Deliberately does NOT create the directory up front,
/// so a test can assert the store creates it on save (Seam 2 create-if-missing).
/// </summary>
internal sealed class TempAppDataPathProvider : IAppDataPathProvider, IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "weatherpoc2-tests-" + Guid.NewGuid().ToString("N"));

    public string GetAppDataDirectory() => _dir;

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best-effort cleanup */ }
    }
}

/// <summary>
/// An <see cref="IAppDataPathProvider"/> returning a fixed path — used to point the store at a path that
/// is an existing file, so <c>Directory.CreateDirectory</c> throws and a write fails deterministically
/// (the D5 "SaveAsync failure → Warning, never thrown" fail-soft proof, Seam 1 (d)).
/// </summary>
internal sealed class FixedPathProvider(string dir) : IAppDataPathProvider
{
    public string GetAppDataDirectory() => dir;
}
