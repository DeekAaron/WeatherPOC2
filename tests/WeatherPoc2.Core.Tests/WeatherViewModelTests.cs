using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.Navigation;
using WeatherPoc2.Core.Persistence;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.Units;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class WeatherViewModelTests
{
    private static DateTime Local(int h) => new(2026, 7, 22, h, 0, 0, DateTimeKind.Unspecified);

    private static ILoadedLocation LoadedWith(Location? location)
    {
        var holder = Substitute.For<ILoadedLocation>();
        holder.Current.Returns(location);
        return holder;
    }

    private static WeatherViewModel Vm(
        IWeatherGateway gateway,
        ILoadedLocation? loaded = null,
        INavigator? navigator = null,
        IUnitsService? units = null)
    {
        // Feature 3: the coordinator fetches for the loaded Location — default a resolved one so the
        // fetch-path tests still exercise a real fetch (they no longer depend on a hard-coded London).
        loaded ??= LoadedWith(Location.LondonGb);
        // The two display children now take the shared IUnitsService + the pure UnitFormatter (Feature 5).
        // A substitute fixed at the canonical defaults keeps these coordinator tests on °C / km/h; a real
        // UnitsService can be threaded in (teardown test) so both children share one Changed source.
        if (units is null)
        {
            var stub = Substitute.For<IUnitsService>();
            stub.Current.Returns(UnitPreferences.Default);
            units = stub;
        }
        var formatter = new UnitFormatter();
        var current = new CurrentConditionsViewModel(new WeatherConditionMapper(), units, formatter, NullLogger<CurrentConditionsViewModel>.Instance);
        var hourly = new HourlyForecastViewModel(new WeatherConditionMapper(), new HourlyWindow(), units, formatter, NullLogger<HourlyForecastViewModel>.Instance);
        return new WeatherViewModel(
            gateway, loaded, navigator ?? Substitute.For<INavigator>(), current, hourly,
            NullLogger<WeatherViewModel>.Instance);
    }

    private static WeatherBundle SampleBundle() => new(
        26.5, 12.6, 40, 3, true,
        new List<HourlyForecastPoint> { new(Local(17), 18.5, 3, true, 0), new(Local(18), 18.0, 2, true, 10) },
        Local(17));

    [Fact]
    public async Task Load_populates_both_children_from_one_fetch()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>()).Returns(SampleBundle());
        var vm = Vm(gateway);

        await vm.LoadCommand.ExecuteAsync(null);

        await gateway.Received(1).GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>());
        Assert.Equal("27°C", vm.CurrentConditions.TemperatureDisplay);
        Assert.Equal(2, vm.HourlyForecast.Entries.Count);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Load_clears_both_children_and_shows_the_friendly_error_on_failure()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>())
               .Returns<WeatherBundle>(_ => throw new WeatherUnavailableException("boom: 503"));
        var vm = Vm(gateway);
        await vm.LoadCommand.ExecuteAsync(null);           // (no prior success needed; assert cleared state)

        Assert.Equal(string.Empty, vm.CurrentConditions.TemperatureDisplay);
        Assert.Empty(vm.HourlyForecast.Entries);
        Assert.Equal("Couldn't reach the weather service — check your connection and try again.", vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Load_does_not_fetch_when_no_location_is_loaded()
    {
        // Launch state: nothing loaded (search is shown first) — the coordinator no-ops (Seam 2 defensive path).
        var gateway = Substitute.For<IWeatherGateway>();
        var vm = Vm(gateway, LoadedWith(null));

        await vm.LoadCommand.ExecuteAsync(null);

        await gateway.DidNotReceive().GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>());
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Disposing_the_coordinator_disposes_both_children_so_a_units_change_re_formats_neither()
    {
        // The coordinator owns the two transient display children; when its page tears down it must
        // propagate Dispose so both children detach from the singleton IUnitsService.Changed (no leak).
        using var paths = new TempAppDataPathProvider();
        var units = new UnitsService(new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance),
                                     NullLogger<UnitsService>.Instance);
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>()).Returns(SampleBundle());
        var vm = Vm(gateway, units: units);
        await vm.LoadCommand.ExecuteAsync(null); // populate both children on °C / km/h
        Assert.Equal("27°C", vm.CurrentConditions.TemperatureDisplay);
        var hourlyTempBefore = vm.HourlyForecast.Entries[0].TemperatureDisplay;

        vm.Dispose();
        vm.Dispose(); // idempotent — a second teardown must not throw

        await units.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit);

        Assert.Equal("27°C", vm.CurrentConditions.TemperatureDisplay);            // panel child detached
        Assert.Equal(hourlyTempBefore, vm.HourlyForecast.Entries[0].TemperatureDisplay); // strip child detached
    }

    [Fact]
    public async Task OpenSearch_navigates_to_the_search_screen()
    {
        var navigator = Substitute.For<INavigator>();
        var vm = Vm(Substitute.For<IWeatherGateway>(), navigator: navigator);

        await vm.OpenSearchCommand.ExecuteAsync(null);

        await navigator.Received(1).GoToSearchAsync();
    }
}
