using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherPoc2.Core.Persistence; // Feature 5 — IPersistenceStore / JsonPersistenceStore / IAppDataPathProvider
using WeatherPoc2.Core.Tests.Support; // Feature 5 temp-dir store harness (reused per the Plan)
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests.Favourites;

/// <summary>
/// Seam 1 (d) proof — the <c>favourites</c> persistence document seam, end-to-end with REAL file I/O
/// through the merged Feature-5 <see cref="JsonPersistenceStore"/> against a per-test temp directory
/// (real serializer output written to and re-read from a real file, not a mock both sides agree on).
/// No production code: this story only proves the existing store round-trips an
/// <see cref="System.Collections.Generic.IReadOnlyList{T}"/> of <see cref="Location"/> under the new
/// <c>favourites</c> key holding the contract the Spec pins (JSON array, most-recently-marked-first,
/// camelCase, nullable <c>openMeteoId</c>, fail-soft recovery). The Favourites domain invariant
/// (dedupe + cap five) is normalised by <c>Favourites.Seed</c>, not the store — this slice proves only
/// the raw round-trip. Tier-1 ($0, every commit).
/// </summary>
public class JsonPersistenceStoreFavouritesTests
{
    private const string Key = "favourites";

    private static string DocumentPath(IAppDataPathProvider paths) =>
        Path.Combine(paths.GetAppDataDirectory(), Key + ".json");

    [Fact]
    public async Task Round_trips_an_ordered_list_of_locations_by_value()
    {
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance);
        var entries = new List<Location>
        {
            new(51.5, -0.12, "London, GB", 2643743),
            new(40.71, -74.0, "New York, US", 5128581),
            new(48.85, 2.35, "Paris, FR", 2988507),
        };

        await store.SaveAsync<IReadOnlyList<Location>>(Key, entries);
        var loaded = await store.LoadAsync<List<Location>>(Key);

        Assert.NotNull(loaded);
        Assert.Equal(entries, loaded); // record value-equality, order preserved (most-recently-marked-first)
    }

    [Fact]
    public async Task Serialized_document_is_a_camelCase_json_array()
    {
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance);
        await store.SaveAsync<IReadOnlyList<Location>>(Key, new List<Location> { new(51.5, -0.12, "London, GB", 2643743) });

        var json = await File.ReadAllTextAsync(DocumentPath(paths));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        var first = doc.RootElement[0];
        Assert.True(first.TryGetProperty("latitude", out _));
        Assert.True(first.TryGetProperty("longitude", out _));
        Assert.True(first.TryGetProperty("label", out _));
        Assert.True(first.TryGetProperty("openMeteoId", out _));
    }

    [Fact]
    public async Task Null_open_meteo_id_round_trips_as_null()
    {
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance);
        await store.SaveAsync<IReadOnlyList<Location>>(Key, new List<Location> { new(51.5, -0.12, "no-id place", null) });

        var json = await File.ReadAllTextAsync(DocumentPath(paths));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, doc.RootElement[0].GetProperty("openMeteoId").ValueKind);

        var loaded = await store.LoadAsync<List<Location>>(Key);
        Assert.Null(loaded![0].OpenMeteoId);
    }

    [Fact]
    public async Task Missing_file_returns_null_with_no_log()
    {
        using var paths = new TempAppDataPathProvider();
        var log = new CapturingLogger<JsonPersistenceStore>();
        var store = new JsonPersistenceStore(paths, log);

        var loaded = await store.LoadAsync<List<Location>>(Key);

        Assert.Null(loaded);                 // absent → null (coordinator seeds empty)
        Assert.Empty(log.Entries);           // normal first run → no log
    }

    [Fact]
    public async Task Malformed_document_returns_null_and_logs_a_warning()
    {
        using var paths = new TempAppDataPathProvider();
        var log = new CapturingLogger<JsonPersistenceStore>();
        var store = new JsonPersistenceStore(paths, log);
        Directory.CreateDirectory(paths.GetAppDataDirectory());
        await File.WriteAllTextAsync(DocumentPath(paths), "{ not json");

        var loaded = await store.LoadAsync<List<Location>>(Key);

        Assert.Null(loaded);                                             // malformed → null (seed empty)
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning);  // fail-visible
    }

    [Fact]
    public async Task Save_failure_is_warning_logged_and_not_thrown()
    {
        // Point the store at a path that is an existing file → CreateDirectory/write fails (D4 fail-soft).
        var tempFile = Path.Combine(Path.GetTempPath(), "weatherpoc2-fav-notadir-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(tempFile, "x");
        try
        {
            var log = new CapturingLogger<JsonPersistenceStore>();
            var store = new JsonPersistenceStore(new FixedPathProvider(tempFile), log);

            var thrown = await Record.ExceptionAsync(() =>
                store.SaveAsync<IReadOnlyList<Location>>(Key, new List<Location> { new(51.5, -0.12, "London, GB", 2643743) }));

            Assert.Null(thrown);                                             // caught inside the store, never surfaced (D4)
            Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning);  // Warning-logged, not thrown
        }
        finally { File.Delete(tempFile); }
    }
}
