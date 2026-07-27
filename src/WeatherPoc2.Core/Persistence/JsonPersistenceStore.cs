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

    private readonly IAppDataPathProvider _paths;
    private readonly ILogger<JsonPersistenceStore> _logger;

    public JsonPersistenceStore(IAppDataPathProvider paths, ILogger<JsonPersistenceStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_paths.GetAppDataDirectory(), key + ".json");
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken);
    }

    public async Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var directory = _paths.GetAppDataDirectory();
        var path = Path.Combine(directory, key + ".json");
        Directory.CreateDirectory(directory);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);
    }
}
