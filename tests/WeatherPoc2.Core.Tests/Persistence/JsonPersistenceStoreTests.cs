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
}
