using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.Persistence; // Feature 5 (IPersistenceStore)
using WeatherPoc2.Core.Tests.Support; // CapturingLogger
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

/// <summary>
/// The load-coordinator seam (Spec D1 / load-ordering clause). <see cref="LocationLoader.LoadAsync"/>
/// is the single choke point every load funnels through: it records into <see cref="SearchHistory"/>,
/// sets the <see cref="ILoadedLocation"/> holder, then awaits a persist — in that order — before the
/// caller navigates. <see cref="LocationLoader.HydrateAsync"/> seeds the state machine from the
/// persisted list at startup (empty when the store returns null). Tier-1 ($0, every commit).
/// </summary>
public class LocationLoaderTests
{
    private const string Key = "search-history";

    private static Location Loc(int id, string label = "x") => new(0, 0, label, id);

    private static LocationLoader NewLoader(
        SearchHistory history, ILoadedLocation loaded, IPersistenceStore store) =>
        new(history, loaded, store, NullLogger<LocationLoader>.Instance);

    [Fact]
    public async Task LoadAsync_records_then_sets_the_holder_then_persists_in_order()
    {
        var history = new SearchHistory();
        var loaded = Substitute.For<ILoadedLocation>();
        var store = Substitute.For<IPersistenceStore>();
        var loader = NewLoader(history, loaded, store);
        var london = Loc(2643743, "London");

        await loader.LoadAsync(london);

        Assert.Equal(new[] { "London" }, history.Entries.Select(e => e.Label).ToArray());
        loaded.Received(1).Set(london);
        await store.Received(1).SaveAsync<IReadOnlyList<Location>>(
            Key, Arg.Any<IReadOnlyList<Location>>(), Arg.Any<CancellationToken>());

        Received.InOrder(() =>
        {
            loaded.Set(london);
            store.SaveAsync<IReadOnlyList<Location>>(
                Key, Arg.Any<IReadOnlyList<Location>>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task LoadAsync_persists_the_post_record_entries()
    {
        var history = new SearchHistory();
        var loaded = Substitute.For<ILoadedLocation>();
        var store = Substitute.For<IPersistenceStore>();
        string[]? persisted = null;
        store.When(s => s.SaveAsync<IReadOnlyList<Location>>(
                 Key, Arg.Any<IReadOnlyList<Location>>(), Arg.Any<CancellationToken>()))
             .Do(ci => persisted = ci.Arg<IReadOnlyList<Location>>().Select(e => e.Label).ToArray());
        var loader = NewLoader(history, loaded, store);

        await loader.LoadAsync(Loc(1, "a"));
        await loader.LoadAsync(Loc(2, "b"));

        // The payload reflects the post-record entries, most-recent-first.
        Assert.Equal(new[] { "b", "a" }, persisted);
    }

    [Fact]
    public async Task HydrateAsync_seeds_history_from_the_persisted_list()
    {
        var history = new SearchHistory();
        var loaded = Substitute.For<ILoadedLocation>();
        var store = Substitute.For<IPersistenceStore>();
        store.LoadAsync<List<Location>>(Key, Arg.Any<CancellationToken>())
             .Returns(new List<Location> { Loc(1, "a"), Loc(2, "b") });
        var loader = NewLoader(history, loaded, store);

        await loader.HydrateAsync();

        Assert.Equal(new[] { "a", "b" }, history.Entries.Select(e => e.Label).ToArray());
    }

    [Fact]
    public async Task HydrateAsync_seeds_empty_when_the_store_returns_null()
    {
        var history = new SearchHistory();
        var loaded = Substitute.For<ILoadedLocation>();
        var store = Substitute.For<IPersistenceStore>();
        store.LoadAsync<List<Location>>(Key, Arg.Any<CancellationToken>())
             .Returns((List<Location>?)null); // absent or malformed (store already Warning-logged malformed)
        var loader = NewLoader(history, loaded, store);

        await loader.HydrateAsync();

        Assert.Empty(history.Entries);
    }

    [Fact]
    public async Task No_log_entry_leaks_a_loaded_locations_coordinates()
    {
        // Security control (mirrors the OpenMeteoGateway coordinate-logging control): no log the loader
        // emits — on LoadAsync, HydrateAsync, or any diagnostic/failure path — may carry a Location's
        // Latitude/Longitude, nor the Location record's default ToString() (which renders both). At most
        // the Label and/or the storage key may appear. Distinctive coordinates pin the control against
        // regression (e.g. interpolating a whole Location record into a message).
        var history = new SearchHistory();
        var loaded = Substitute.For<ILoadedLocation>();
        var store = Substitute.For<IPersistenceStore>();
        var logger = new CapturingLogger<LocationLoader>();
        var loader = new LocationLoader(history, loaded, store, logger);

        const double lat = 12.3456789;
        const double lon = -98.7654321;
        var secret = new Location(lat, lon, "Secretville", 424242);

        // Hydrate reads a persisted entry with the same distinctive coordinates, so a leak on the
        // hydrate path (logging the seeded entries) would be caught too.
        store.LoadAsync<List<Location>>(Key, Arg.Any<CancellationToken>())
             .Returns(new List<Location> { secret });

        await loader.HydrateAsync();
        await loader.LoadAsync(secret);

        var latText = lat.ToString(CultureInfo.InvariantCulture);
        var lonText = lon.ToString(CultureInfo.InvariantCulture);
        foreach (var message in logger.Messages)
        {
            Assert.DoesNotContain(latText, message);
            Assert.DoesNotContain(lonText, message);
            Assert.DoesNotContain(secret.ToString(), message); // guards the record's default ToString()
        }
    }

    [Fact]
    public async Task LoadAsync_completes_and_records_even_when_the_persist_fails()
    {
        // AC: a save failure does not block LoadAsync. Driven end-to-end through the REAL Feature-5
        // store pointed at a base path that is an existing FILE, so its create-dir/atomic write fails —
        // the store catches + Warning-logs and never throws (ADR-0003 / D3), so the awaited persist
        // completes and LoadAsync returns normally with the Location correctly recorded most-recent.
        var filePath =
            Path.Combine(Path.GetTempPath(), "wp2-loader-file-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(filePath, "not a directory");
        try
        {
            var history = new SearchHistory();
            var loaded = Substitute.For<ILoadedLocation>();
            var store = new JsonPersistenceStore(
                new FixedPathProvider(filePath), NullLogger<JsonPersistenceStore>.Instance);
            var loader = NewLoader(history, loaded, store);
            var london = new Location(51.50853, -0.12574, "London", 2643743);

            var thrown = await Record.ExceptionAsync(() => loader.LoadAsync(london));

            Assert.Null(thrown); // the failed persist never surfaced — LoadAsync was not blocked
            loaded.Received(1).Set(london);
            Assert.Equal(new[] { "London" }, history.Entries.Select(e => e.Label).ToArray());
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

}
