using System.Text.Json;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Persistence; // Feature 5 — IPersistenceStore / JsonPersistenceStore / IAppDataPathProvider
using WeatherPoc2.Core.Tests.Support; // Feature 5 temp-dir store harness (reused per the Plan)
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

/// <summary>
/// Seam 1 (d) proof — the <c>search-history</c> persistence document seam, end-to-end with REAL file
/// I/O through the merged Feature-5 <see cref="JsonPersistenceStore"/> against a per-test temp directory
/// (e2e Principle 2 — real serializer output written to and re-read from a real file, not a mock both
/// sides agree on). No production code: this story only proves the existing seam holds the contract the
/// Spec pins (JSON array, most-recent-first, camelCase, nullable <c>openMeteoId</c>, fail-soft recovery),
/// and that <see cref="SearchHistory.Seed"/> normalises a parseable-but-invariant-violating document.
/// Tier-1 ($0, every commit).
/// </summary>
public class SearchHistoryPersistenceTests
{
    private const string Key = "search-history";

    private static string DocumentPath(IAppDataPathProvider paths) =>
        Path.Combine(paths.GetAppDataDirectory(), Key + ".json");

    [Fact]
    public async Task Roundtrips_an_ordered_list_of_locations()
    {
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, new CapturingLogger<JsonPersistenceStore>());
        var entries = new List<Location>
        {
            new(51.50853, -0.12574, "London, England, United Kingdom", 2643743),
            new(43.70011, -79.4163, "Toronto, Ontario, Canada", 6167865),
            new(40.4165, -3.70256, "Madrid, Spain", 3117735),
        };

        await store.SaveAsync<IReadOnlyList<Location>>(Key, entries);
        var loaded = await store.LoadAsync<List<Location>>(Key);

        Assert.NotNull(loaded);
        Assert.Equal(entries, loaded); // record value-equality, order preserved (most-recent-first)
    }

    [Fact]
    public async Task Serialized_json_is_a_camelcase_array()
    {
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, new CapturingLogger<JsonPersistenceStore>());

        await store.SaveAsync<IReadOnlyList<Location>>(Key, new List<Location>
        {
            new(51.50853, -0.12574, "London", 2643743),
        });

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
        var store = new JsonPersistenceStore(paths, new CapturingLogger<JsonPersistenceStore>());

        await store.SaveAsync<IReadOnlyList<Location>>(Key, new List<Location>
        {
            new(51.5, -0.1, "id-less", OpenMeteoId: null),
        });

        var json = await File.ReadAllTextAsync(DocumentPath(paths));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Null, doc.RootElement[0].GetProperty("openMeteoId").ValueKind);

        var loaded = await store.LoadAsync<List<Location>>(Key);
        Assert.Null(loaded![0].OpenMeteoId);
    }

    [Fact]
    public async Task Missing_file_loads_as_null_without_logging()
    {
        using var paths = new TempAppDataPathProvider();
        var log = new CapturingLogger<JsonPersistenceStore>();
        var store = new JsonPersistenceStore(paths, log); // nothing written yet

        var loaded = await store.LoadAsync<List<Location>>(Key);

        Assert.Null(loaded);        // absent = normal first run → coordinator seeds empty
        Assert.Empty(log.Entries);  // no log on first run (Spec D3 / Seam 1 (c))
    }

    [Fact]
    public async Task Malformed_document_loads_as_null_and_warns()
    {
        using var paths = new TempAppDataPathProvider();
        var log = new CapturingLogger<JsonPersistenceStore>();
        Directory.CreateDirectory(paths.GetAppDataDirectory());
        await File.WriteAllTextAsync(DocumentPath(paths), "{ not json");
        var store = new JsonPersistenceStore(paths, log);

        var loaded = await store.LoadAsync<List<Location>>(Key);

        Assert.Null(loaded); // store catches + Warning-logs (Feature 5 contract); coordinator seeds empty
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task A_parseable_overlength_or_duplicate_document_is_normalised_by_Seed()
    {
        // The store returns the raw parsed list (it does not enforce the domain invariant);
        // SearchHistory.Seed is what normalises it (Spec Seam 1 (c) + D2).
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, new CapturingLogger<JsonPersistenceStore>());
        var overlong = new List<Location>
        {
            new(0, 0, "a", 1), new(0, 0, "a-dup", 1), new(0, 0, "b", 2),
            new(0, 0, "c", 3), new(0, 0, "d", 4), new(0, 0, "e", 5),
        };
        await store.SaveAsync<IReadOnlyList<Location>>(Key, overlong);

        var loaded = await store.LoadAsync<List<Location>>(Key);
        var history = new SearchHistory();
        history.Seed(loaded!);

        Assert.Equal(new[] { "a", "b", "c", "d" }, history.Entries.Select(e => e.Label).ToArray());
    }

    [Fact]
    public async Task Save_failure_is_caught_logged_and_not_thrown()
    {
        // Base path resolves to an existing FILE (not a directory), so the store's create-dir /
        // serialize-to-temp-then-replace write fails. Per ADR-0003 / Feature-5 Seam 1 the store catches,
        // logs a Warning, and does NOT throw (Spec D3 / Seam 1 (d)) — so a persistence failure never
        // blocks the load. This is the D1/D3 outcome Feature 6 owns: SaveAsync completes without surfacing
        // an exception to the caller, so LocationLoader.LoadAsync — which awaits it — cannot be blocked by
        // a failed persist.
        var filePath =
            Path.Combine(Path.GetTempPath(), "wp2-searchhistory-file-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(filePath, "not a directory");
        try
        {
            var log = new CapturingLogger<JsonPersistenceStore>();
            var store = new JsonPersistenceStore(new FixedPathProvider(filePath), log);

            var thrown = await Record.ExceptionAsync(() =>
                store.SaveAsync<IReadOnlyList<Location>>(
                    Key, new List<Location> { new(51.50853, -0.12574, "London", 2643743) }));

            Assert.Null(thrown); // caught inside the store; never surfaced to the caller (D3)
            Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning); // Warning-logged, not thrown
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }
}
