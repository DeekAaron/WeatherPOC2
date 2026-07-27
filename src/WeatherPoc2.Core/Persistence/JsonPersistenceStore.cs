using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace WeatherPoc2.Core.Persistence;

/// <summary>
/// <see cref="IPersistenceStore"/> backed by one <c>System.Text.Json</c> document per key under the
/// injected <see cref="IAppDataPathProvider"/> base directory. Enums are stored by name (stable across
/// enum reordering). Fail-visible + fail-soft per ADR-0003 / Technical-Context Principle 1.
/// </summary>
public sealed class JsonPersistenceStore : IPersistenceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    // Per-key write gate (Seam 1 concurrency clause): serializes concurrent SaveAsync calls to the same
    // key so one write completes before the next begins — no interleaved/dropped writers to one file.
    // Static so a single key is gated process-wide (the store is a DI singleton; static also holds under
    // tests that construct multiple stores over the same key).
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    private readonly IAppDataPathProvider _paths;
    private readonly ILogger<JsonPersistenceStore> _logger;

    public JsonPersistenceStore(IAppDataPathProvider paths, ILogger<JsonPersistenceStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        var path = Path.Combine(_paths.GetAppDataDirectory(), key + ".json");
        if (!File.Exists(path))
            return default; // absent = normal first run; no log (D5)

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Any content-parse or read failure fails closed to defaults + a Warning (never to the
            // caller): malformed syntax, an unknown enum name, or a hostile deep-nesting JsonException
            // all land here, so a tampered/corrupt file can never fail the weather view (ADR-0001 / D5).
            _logger.LogWarning(ex, "Persistence: could not read '{Key}' at {Path} — using defaults", key, path);
            return default;
        }
    }

    public async Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        var directory = _paths.GetAppDataDirectory();
        var path = Path.Combine(directory, key + ".json");
        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);            // serialize writers to this key (Seam 1 (c))
        try
        {
            Directory.CreateDirectory(directory); // Seam 2: the app-data dir is not guaranteed to pre-exist
            // Atomic write: serialize to a temp file, then replace the live file — an interrupted write
            // never truncates units.json (a torn file would otherwise read back malformed and reset to
            // defaults, losing a just-made preference across restart — a PRD-48 violation).
            var tempPath = path + ".tmp";
            await using (var stream = File.Create(tempPath))
                await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);
            // Atomic replace on the same volume. File.Replace is the atomic-swap primitive when the live
            // file already exists (File.Move overwrite maps to MoveFileEx replace, which fails with
            // UnauthorizedAccessException on Windows); a plain Move covers the first-ever write.
            if (File.Exists(path))
                File.Replace(tempPath, path, destinationBackupFileName: null);
            else
                File.Move(tempPath, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Persistence: could not write '{Key}' at {Path} — change kept in memory only", key, path);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Rejects any <paramref name="key"/> that is not a single path segment — a caller-contract guard
    /// (distinct from the D5 fail-soft handling of file <em>content</em>) that runs before any file
    /// access. The store builds <c>{basePath}/{key}.json</c>; a key with a directory separator, a
    /// <c>..</c> traversal segment, or a rooted/absolute path would escape the injected base directory
    /// (arbitrary read on load, arbitrary overwrite on save). Only <c>units</c> is used today, but this
    /// is the generic seam Features 6/7 extend, so the guard belongs here.
    /// </summary>
    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Persistence key must be a non-empty single path segment.", nameof(key));
        if (key.Contains('/') || key.Contains('\\'))
            throw new ArgumentException($"Persistence key '{key}' must not contain a directory separator.", nameof(key));
        if (key.Contains(".."))
            throw new ArgumentException($"Persistence key '{key}' must not contain a '..' traversal segment.", nameof(key));
        if (Path.IsPathRooted(key))
            throw new ArgumentException($"Persistence key '{key}' must not be a rooted/absolute path.", nameof(key));
    }
}
