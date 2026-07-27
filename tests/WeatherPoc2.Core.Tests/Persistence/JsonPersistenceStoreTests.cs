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
}
