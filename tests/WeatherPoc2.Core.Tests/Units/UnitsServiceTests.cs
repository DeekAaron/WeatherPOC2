using WeatherPoc2.Core.Persistence;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.Units;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace WeatherPoc2.Core.Tests.Units;

public class UnitsServiceTests
{
    private static (UnitsService service, JsonPersistenceStore store, TempAppDataPathProvider paths) New()
    {
        var paths = new TempAppDataPathProvider();
        var store = new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance);
        return (new UnitsService(store, NullLogger<UnitsService>.Instance), store, paths);
    }

    [Fact]
    public void Current_starts_at_the_canonical_defaults_before_initialize()
    {
        var (service, _, paths) = New();
        using var _p = paths;
        Assert.Equal(UnitPreferences.Default, service.Current);
    }

    [Fact]
    public async Task InitializeAsync_keeps_defaults_and_does_not_raise_when_nothing_is_persisted()
    {
        var (service, _, paths) = New();
        using var _p = paths;
        var raised = 0;
        service.Changed += (_, _) => raised++;

        await service.InitializeAsync();

        Assert.Equal(UnitPreferences.Default, service.Current);
        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task InitializeAsync_adopts_persisted_prefs_and_raises_changed_when_they_differ()
    {
        var (service, store, paths) = New();
        using var _p = paths;
        await store.SaveAsync("units", new UnitPreferences(TemperatureUnit.Fahrenheit, WindSpeedUnit.Knots));
        var raised = 0;
        service.Changed += (_, _) => raised++;

        await service.InitializeAsync();

        Assert.Equal(new UnitPreferences(TemperatureUnit.Fahrenheit, WindSpeedUnit.Knots), service.Current);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task SetTemperatureUnitAsync_updates_current_raises_changed_and_persists()
    {
        var (service, store, paths) = New();
        using var _p = paths;
        var raised = 0;
        service.Changed += (_, _) => raised++;

        await service.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit);

        Assert.Equal(TemperatureUnit.Fahrenheit, service.Current.Temperature);
        Assert.Equal(1, raised);
        Assert.Equal(TemperatureUnit.Fahrenheit,
            (await store.LoadAsync<UnitPreferences>("units"))!.Temperature); // persisted
    }

    [Fact]
    public async Task Setting_the_same_unit_is_a_no_op_and_does_not_raise()
    {
        var (service, _, paths) = New();
        using var _p = paths;
        var raised = 0;
        service.Changed += (_, _) => raised++;

        await service.SetWindSpeedUnitAsync(WindSpeedUnit.KilometresPerHour); // already the default

        Assert.Equal(0, raised);
    }

    [Fact]
    public async Task InitializeAsync_keeps_defaults_when_the_persisted_file_is_malformed()
    {
        using var paths = new TempAppDataPathProvider();
        var storeLog = new CapturingLogger<JsonPersistenceStore>();
        var store = new JsonPersistenceStore(paths, storeLog);
        Directory.CreateDirectory(paths.GetAppDataDirectory());
        await File.WriteAllTextAsync(Path.Combine(paths.GetAppDataDirectory(), "units.json"), "{ not json");
        var service = new UnitsService(store, NullLogger<UnitsService>.Instance);

        await service.InitializeAsync();

        Assert.Equal(UnitPreferences.Default, service.Current);               // malformed → defaults (D5)
        Assert.Contains(storeLog.Entries, e => e.Level == LogLevel.Warning);  // the store logs the Warning
    }

    [Fact]
    public async Task SetTemperatureUnitAsync_keeps_the_in_memory_value_when_the_store_write_fails()
    {
        // A store pointed at a path that is a file → SaveAsync's write fails (caught + Warning, never thrown).
        var tempFile = Path.Combine(Path.GetTempPath(), "weatherpoc2-notadir-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(tempFile, "x");
        try
        {
            var storeLog = new CapturingLogger<JsonPersistenceStore>();
            var store = new JsonPersistenceStore(new FixedPathProvider(tempFile), storeLog);
            var service = new UnitsService(store, NullLogger<UnitsService>.Instance);

            await service.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit); // must not throw

            Assert.Equal(TemperatureUnit.Fahrenheit, service.Current.Temperature); // in-memory kept (UI consistent)
            Assert.Contains(storeLog.Entries, e => e.Level == LogLevel.Warning);   // the store logs the write failure
        }
        finally { File.Delete(tempFile); }
    }
}
