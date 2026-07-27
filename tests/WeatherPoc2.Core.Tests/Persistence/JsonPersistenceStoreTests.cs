using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Persistence;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.Units;
using Xunit;

namespace WeatherPoc2.Core.Tests.Persistence;

public class JsonPersistenceStoreTests
{
    private static (JsonPersistenceStore store, TempAppDataPathProvider paths, CapturingLogger<JsonPersistenceStore> log) NewStore()
    {
        var paths = new TempAppDataPathProvider();
        var log = new CapturingLogger<JsonPersistenceStore>();
        return (new JsonPersistenceStore(paths, log), paths, log);
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_the_value()
    {
        var (store, paths, _) = NewStore();
        using var _p = paths;
        var prefs = new UnitPreferences(TemperatureUnit.Fahrenheit, WindSpeedUnit.Knots);

        await store.SaveAsync("units", prefs);
        var loaded = await store.LoadAsync<UnitPreferences>("units");

        Assert.Equal(prefs, loaded);
    }

    [Fact]
    public async Task SaveAsync_serializes_enum_members_by_name_with_camelCase_property_names()
    {
        var (store, paths, _) = NewStore();
        using var _p = paths;

        await store.SaveAsync("units", new UnitPreferences(TemperatureUnit.Fahrenheit, WindSpeedUnit.MilesPerHour));

        var json = await File.ReadAllTextAsync(Path.Combine(paths.GetAppDataDirectory(), "units.json"));
        Assert.Contains("\"Fahrenheit\"", json);      // enum by name, not ordinal
        Assert.Contains("\"MilesPerHour\"", json);
        Assert.Contains("\"temperature\"", json);     // camelCase property names — the durable wire shape (Seam 1 (c))
        Assert.Contains("\"windSpeed\"", json);
    }

    [Fact]
    public async Task LoadAsync_returns_null_and_does_not_log_when_the_file_is_absent()
    {
        var (store, paths, log) = NewStore();
        using var _p = paths;

        var loaded = await store.LoadAsync<UnitPreferences>("units");

        Assert.Null(loaded);
        Assert.Empty(log.Entries); // first-run is normal — no log (D5)
    }

    [Fact]
    public async Task LoadAsync_returns_null_and_logs_a_warning_on_malformed_json()
    {
        var (store, paths, log) = NewStore();
        using var _p = paths;
        Directory.CreateDirectory(paths.GetAppDataDirectory());
        await File.WriteAllTextAsync(Path.Combine(paths.GetAppDataDirectory(), "units.json"), "{ not json");

        var loaded = await store.LoadAsync<UnitPreferences>("units");

        Assert.Null(loaded);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task LoadAsync_returns_null_and_logs_a_warning_on_an_unknown_enum_name()
    {
        var (store, paths, log) = NewStore();
        using var _p = paths;
        Directory.CreateDirectory(paths.GetAppDataDirectory());
        await File.WriteAllTextAsync(
            Path.Combine(paths.GetAppDataDirectory(), "units.json"),
            "{\"temperature\":\"Kelvin\",\"windSpeed\":\"KilometresPerHour\"}");

        var loaded = await store.LoadAsync<UnitPreferences>("units");

        Assert.Null(loaded);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task SaveAsync_creates_the_base_directory_when_it_does_not_exist()
    {
        var (store, paths, _) = NewStore();
        using var _p = paths;
        Assert.False(Directory.Exists(paths.GetAppDataDirectory())); // TempAppDataPathProvider does not pre-create

        await store.SaveAsync("units", UnitPreferences.Default);

        Assert.True(File.Exists(Path.Combine(paths.GetAppDataDirectory(), "units.json")));
    }

    [Fact]
    public async Task LoadAsync_soft_defaults_a_missing_member_and_does_not_log()
    {
        var (store, paths, log) = NewStore();
        using var _p = paths;
        Directory.CreateDirectory(paths.GetAppDataDirectory());
        await File.WriteAllTextAsync(
            Path.Combine(paths.GetAppDataDirectory(), "units.json"),
            "{\"temperature\":\"Fahrenheit\"}"); // windSpeed absent

        var loaded = await store.LoadAsync<UnitPreferences>("units");

        // System.Text.Json soft-defaults the absent positional member (forward-compatible) —
        // not null, no Warning. (Contrast malformed/unknown-enum, which are null + Warning.)
        Assert.NotNull(loaded);
        Assert.Equal(new UnitPreferences(TemperatureUnit.Fahrenheit, WindSpeedUnit.KilometresPerHour), loaded);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task SaveAsync_logs_a_warning_and_does_not_throw_when_the_write_fails()
    {
        // Point the store at a path that is an existing FILE, so Directory.CreateDirectory throws
        // (cross-platform). The store must catch it, log a Warning, and NOT throw to the caller (D5).
        var tempFile = Path.Combine(Path.GetTempPath(), "weatherpoc2-notadir-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(tempFile, "x");
        try
        {
            var log = new CapturingLogger<JsonPersistenceStore>();
            var store = new JsonPersistenceStore(new FixedPathProvider(tempFile), log);

            await store.SaveAsync("units", UnitPreferences.Default); // must not throw

            Assert.Contains(log.Entries, e => e.Level == LogLevel.Warning);
        }
        finally { File.Delete(tempFile); }
    }

    [Fact]
    public async Task Overlapping_SaveAsync_calls_to_one_key_never_leave_a_torn_file()
    {
        // Seam 1 concurrency/atomicity (c): the fire-and-forget picker path can start two overlapping
        // writes to units.json. The per-key gate serializes them and the temp-file+rename is atomic, so
        // the file is never truncated/partial — LoadAsync always returns a complete, parseable value
        // equal to one of the issued preferences (never a torn document that resets to defaults).
        var (store, paths, log) = NewStore();
        using var _p = paths;
        var a = new UnitPreferences(TemperatureUnit.Fahrenheit, WindSpeedUnit.Knots);
        var b = new UnitPreferences(TemperatureUnit.Celsius, WindSpeedUnit.MilesPerHour);

        // Fire both concurrently, many times, to exercise interleaving.
        for (var i = 0; i < 50; i++)
            await Task.WhenAll(store.SaveAsync("units", a), store.SaveAsync("units", b));

        var loaded = await store.LoadAsync<UnitPreferences>("units");
        Assert.NotNull(loaded);                    // never null → the file was never left torn/malformed
        Assert.Contains(loaded, new[] { a, b });   // a complete one of the two, never a partial document
        // Writes are *serialized* (one completes before the next begins), not dropped: a naive shared
        // write to the same handle would fail the overlapping writer with a sharing violation and log a
        // Warning (a silently-lost preference — a PRD-48 risk). The per-key gate means no write is ever
        // dropped, so no Warning is logged.
        Assert.DoesNotContain(log.Entries, e => e.Level == LogLevel.Warning);
        // No orphaned temp file is left behind after the atomic rename.
        Assert.False(File.Exists(Path.Combine(paths.GetAppDataDirectory(), "units.json.tmp")));
    }
}
