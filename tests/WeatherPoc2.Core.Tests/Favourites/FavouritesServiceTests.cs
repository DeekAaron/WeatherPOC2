using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherPoc2.Core.Persistence;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests.Favourites;

public class FavouritesServiceTests
{
    private const string Key = "favourites";
    private static Location Loc(int id) => new(id, id, $"Place {id}", id);

    private static (FavouritesService svc, JsonPersistenceStore store, TempAppDataPathProvider paths) New()
    {
        var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance);
        return (new FavouritesService(new WeatherPoc2.Core.Weather.Favourites(), store, NullLogger<FavouritesService>.Instance), store, paths);
    }

    [Fact]
    public async Task MarkAsync_marks_raises_changed_returns_marked_and_persists_on_marked()
    {
        var (svc, store, paths) = New();
        using var _p = paths;
        var raised = 0;
        svc.Changed += (_, _) => raised++;

        var result = await svc.MarkAsync(Loc(1));

        Assert.Equal(MarkResult.Marked, result);
        Assert.True(svc.IsFavourite(Loc(1)));
        Assert.Equal(1, raised);
        var persisted = await store.LoadAsync<List<Location>>(Key);
        Assert.Equal(new int?[] { 1 }, persisted!.Select(e => e.OpenMeteoId));
    }

    [Fact]
    public async Task MarkAsync_at_capacity_returns_RefusedFull_and_does_not_persist_a_change()
    {
        var (svc, store, paths) = New();
        using var _p = paths;
        for (var i = 1; i <= 5; i++) await svc.MarkAsync(Loc(i));

        var result = await svc.MarkAsync(Loc(6));

        Assert.Equal(MarkResult.RefusedFull, result);
        var persisted = await store.LoadAsync<List<Location>>(Key);
        Assert.Equal(5, persisted!.Count);                       // the sixth was never written
        Assert.DoesNotContain(persisted, e => e.OpenMeteoId == 6);
    }

    [Fact]
    public async Task UnmarkAsync_removes_by_identity_and_persists()
    {
        var (svc, store, paths) = New();
        using var _p = paths;
        await svc.MarkAsync(Loc(1));
        await svc.MarkAsync(Loc(2));

        await svc.UnmarkAsync(Loc(1));

        Assert.False(svc.IsFavourite(Loc(1)));
        var persisted = await store.LoadAsync<List<Location>>(Key);
        Assert.Equal(new int?[] { 2 }, persisted!.Select(e => e.OpenMeteoId));
    }

    [Fact]
    public async Task HydrateAsync_seeds_from_a_persisted_list_preserving_order()
    {
        var (svc, store, paths) = New();
        using var _p = paths;
        await store.SaveAsync<IReadOnlyList<Location>>(Key, new List<Location> { Loc(3), Loc(4) });

        await svc.HydrateAsync();

        Assert.Equal(new int?[] { 3, 4 }, svc.Entries.Select(e => e.OpenMeteoId));
    }

    [Fact]
    public async Task HydrateAsync_seeds_empty_when_nothing_is_persisted()
    {
        var (svc, _, paths) = New();
        using var _p = paths;

        await svc.HydrateAsync();

        Assert.Empty(svc.Entries);
    }

    [Fact]
    public async Task IsFavourite_and_Entries_delegate_to_the_machine_and_Changed_forwards_the_machine_event()
    {
        var (svc, _, paths) = New();
        using var _p = paths;
        var raised = 0;
        svc.Changed += (_, _) => raised++;

        await svc.MarkAsync(Loc(1));

        Assert.True(svc.IsFavourite(Loc(1)));                                 // delegates IsFavourite
        Assert.Equal(new int?[] { 1 }, svc.Entries.Select(e => e.OpenMeteoId)); // delegates Entries
        Assert.Equal(1, raised);                                              // Changed forwarded from the machine
    }

    [Fact]
    public async Task MarkAsync_keeps_the_in_memory_state_when_the_store_write_fails()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "weatherpoc2-fav-svc-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(tempFile, "x"); // path is a file → write fails
        try
        {
            var storeLog = new CapturingLogger<JsonPersistenceStore>();
            var store = new JsonPersistenceStore(new FixedPathProvider(tempFile), storeLog);
            var svc = new FavouritesService(new WeatherPoc2.Core.Weather.Favourites(), store, NullLogger<FavouritesService>.Instance);

            var result = await svc.MarkAsync(Loc(1)); // must not throw

            Assert.Equal(MarkResult.Marked, result);
            Assert.True(svc.IsFavourite(Loc(1)));                                  // in-memory kept
            Assert.Contains(storeLog.Entries, e => e.Level == LogLevel.Warning);   // store logged the failure
        }
        finally { File.Delete(tempFile); }
    }

    // ---- Security acceptance criteria ------------------------------------------------------------

    [Fact]
    public async Task HydrateAsync_is_fail_closed_on_a_malformed_document_and_yields_empty()
    {
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance);
        Directory.CreateDirectory(paths.GetAppDataDirectory());
        await File.WriteAllTextAsync(Path.Combine(paths.GetAppDataDirectory(), "favourites.json"), "{ not json");
        var svc = new FavouritesService(new WeatherPoc2.Core.Weather.Favourites(), store, NullLogger<FavouritesService>.Instance);

        await svc.HydrateAsync(); // must not throw for any on-disk content

        Assert.Empty(svc.Entries);
    }

    [Fact]
    public async Task HydrateAsync_is_fail_closed_on_an_over_capacity_duplicate_document_normalising_to_five_distinct()
    {
        using var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance);
        // A parseable-but-invariant-violating document: eight entries, one duplicate id.
        var tampered = new List<Location> { Loc(1), Loc(2), Loc(2), Loc(3), Loc(4), Loc(5), Loc(6), Loc(7) };
        await store.SaveAsync<IReadOnlyList<Location>>(Key, tampered);
        var svc = new FavouritesService(new WeatherPoc2.Core.Weather.Favourites(), store, NullLogger<FavouritesService>.Instance);

        await svc.HydrateAsync(); // must not throw and cannot escape the cap

        Assert.Equal(new int?[] { 1, 2, 3, 4, 5 }, svc.Entries.Select(e => e.OpenMeteoId));
    }

    [Fact]
    public async Task Save_failure_emits_no_location_coordinates_or_label_on_any_logger()
    {
        const double distinctiveLat = 12.3456789;
        const double distinctiveLon = -98.7654321;
        const string distinctiveLabel = "Secret-Place-XYZ";

        var tempFile = Path.Combine(Path.GetTempPath(), "weatherpoc2-fav-pii-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(tempFile, "x"); // path is a file → write fails
        try
        {
            var storeLog = new CapturingLogger<JsonPersistenceStore>();
            var svcLog = new CapturingLogger<FavouritesService>();
            var store = new JsonPersistenceStore(new FixedPathProvider(tempFile), storeLog);
            var svc = new FavouritesService(new WeatherPoc2.Core.Weather.Favourites(), store, svcLog);

            await svc.MarkAsync(new Location(distinctiveLat, distinctiveLon, distinctiveLabel, 424242));

            var messages = svcLog.Entries.Select(e => e.Message)
                .Concat(storeLog.Entries.Select(e => e.Message))
                .ToList();
            foreach (var message in messages)
            {
                Assert.DoesNotContain(distinctiveLabel, message);
                Assert.DoesNotContain(distinctiveLat.ToString(CultureInfo.InvariantCulture), message);
                Assert.DoesNotContain(distinctiveLon.ToString(CultureInfo.InvariantCulture), message);
            }
        }
        finally { File.Delete(tempFile); }
    }
}
