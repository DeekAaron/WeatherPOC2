using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.Persistence;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.Units;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class HourlyForecastViewModelTests
{
    // The display-only Hourly VM now takes the shared IUnitsService + the pure UnitFormatter (Feature 5):
    // each entry's TemperatureDisplay is formatted through the current Temperature unit and re-formatted
    // on IUnitsService.Changed. Time, icon, and Chance are not units-affected.
    private static HourlyForecastViewModel Vm(IUnitsService units,
        ILogger<HourlyForecastViewModel>? logger = null)
        => new(new WeatherConditionMapper(), new HourlyWindow(), units, new UnitFormatter(),
               logger ?? NullLogger<HourlyForecastViewModel>.Instance);

    private static IUnitsService FixedUnits(UnitPreferences prefs)
    {
        var units = Substitute.For<IUnitsService>();
        units.Current.Returns(prefs);
        return units;
    }

    private static DateTime Local(int h, int mi = 0) => new(2026, 7, 22, h, mi, 0, DateTimeKind.Unspecified);

    private static WeatherBundle BundleWith(IReadOnlyList<HourlyForecastPoint> hourly, DateTime localNow)
        => new(20.0, 10.0, 0, 0, true, hourly, localNow);

    [Fact]
    public void Apply_builds_one_entry_per_windowed_hour_with_formatted_fields()
    {
        var hourly = new List<HourlyForecastPoint>
        {
            new(Local(16), 19.4, 3, true, 5),    // current hour
            new(Local(17), 18.6, 2, true, 20),
            new(Local(23), 12.1, 0, false, 10),  // clear night -> night icon
        };
        var vm = Vm(FixedUnits(UnitPreferences.Default));

        vm.Apply(BundleWith(hourly, Local(16, 20)));

        Assert.Equal(3, vm.Entries.Count);
        Assert.Equal("16:00", vm.Entries[0].TimeDisplay);
        Assert.Equal("19°C", vm.Entries[0].TemperatureDisplay);  // whole-degree, now unit-suffixed (was "19°")
        Assert.Equal("5%", vm.Entries[0].ChanceOfRainDisplay);
        Assert.Equal("cloudy.png", vm.Entries[0].IconSource);    // code 3 -> cloudy
        Assert.True(vm.Entries[0].IsNow);
        Assert.False(vm.Entries[1].IsNow);
        Assert.Equal("clear_night.png", vm.Entries[2].IconSource); // code 0 + night
    }

    [Fact]
    public async Task Changing_the_temperature_unit_reformats_every_hourly_entry_temperature_only()
    {
        // A real UnitsService over a temp-dir store + a real UnitFormatter (both deterministic).
        using var paths = new TempAppDataPathProvider();
        var units = new UnitsService(new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance),
                                     NullLogger<UnitsService>.Instance);
        var vm = Vm(units);
        var hourly = new List<HourlyForecastPoint>
        {
            new(Local(16), 0.0, 0, true, 5),    // 0 °C -> 32 °F
            new(Local(17), 10.0, 2, true, 20),  // 10 °C -> 50 °F
        };
        vm.Apply(BundleWith(hourly, Local(16, 20))); // single Apply — the only bundle push
        Assert.Equal("0°C", vm.Entries[0].TemperatureDisplay);
        Assert.Equal("10°C", vm.Entries[1].TemperatureDisplay);
        var timeBefore = vm.Entries[0].TimeDisplay;
        var iconBefore = vm.Entries[0].IconSource;
        var chanceBefore = vm.Entries[0].ChanceOfRainDisplay;

        await units.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit);

        Assert.Equal("32°F", vm.Entries[0].TemperatureDisplay);  // re-rendered from the retained windowed points
        Assert.Equal("50°F", vm.Entries[1].TemperatureDisplay);
        Assert.Equal(timeBefore, vm.Entries[0].TimeDisplay);     // Time unchanged
        Assert.Equal(iconBefore, vm.Entries[0].IconSource);      // icon unchanged
        Assert.Equal(chanceBefore, vm.Entries[0].ChanceOfRainDisplay); // Chance unchanged (stays %)
    }

    [Fact]
    public async Task Changing_the_temperature_unit_keeps_the_placeholder_for_a_null_hour_temperature()
    {
        using var paths = new TempAppDataPathProvider();
        var units = new UnitsService(new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance),
                                     NullLogger<UnitsService>.Instance);
        var vm = Vm(units);
        vm.Apply(BundleWith(new List<HourlyForecastPoint> { new(Local(16), null, 0, true, 5) }, Local(16, 20)));
        Assert.Equal("—", vm.Entries[0].TemperatureDisplay);

        await units.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit);

        Assert.Equal("—", vm.Entries[0].TemperatureDisplay);     // null temperature keeps the placeholder
    }

    [Fact]
    public void Apply_renders_a_placeholder_and_logs_a_warning_for_a_null_measure()
    {
        var hourly = new List<HourlyForecastPoint>
        {
            new(Local(16), null, 3, true, null),   // temperature AND chance absent (soft gap)
        };
        var logger = new CapturingLogger<HourlyForecastViewModel>();
        var vm = Vm(FixedUnits(UnitPreferences.Default), logger);

        vm.Apply(BundleWith(hourly, Local(16, 20)));

        Assert.Equal("—", vm.Entries[0].TemperatureDisplay);
        Assert.Equal("—", vm.Entries[0].ChanceOfRainDisplay);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void Apply_replaces_prior_entries_on_each_call()
    {
        var vm = Vm(FixedUnits(UnitPreferences.Default));
        vm.Apply(BundleWith(new List<HourlyForecastPoint> { new(Local(16), 19.0, 0, true, 0) }, Local(16, 5)));
        vm.Apply(BundleWith(new List<HourlyForecastPoint> { new(Local(4), 9.0, 0, true, 0), new(Local(5), 8.0, 0, true, 0) }, Local(4, 5)));

        Assert.Equal(2, vm.Entries.Count);
        Assert.Equal("04:00", vm.Entries[0].TimeDisplay);
    }

    [Fact]
    public void Clear_empties_the_entries()
    {
        var vm = Vm(FixedUnits(UnitPreferences.Default));
        vm.Apply(BundleWith(new List<HourlyForecastPoint> { new(Local(16), 19.0, 0, true, 0) }, Local(16, 5)));

        vm.Clear();

        Assert.Empty(vm.Entries);
    }

    [Fact]
    public async Task Disposing_the_strip_detaches_it_so_a_units_change_no_longer_rebuilds()
    {
        // The strip subscribes to IUnitsService.Changed in its ctor; the service is a singleton that
        // outlives this transient VM. Dispose() must detach that subscription so the disposed strip no
        // longer rebuilds (no leak, no warning logs on a dead instance). Idempotent: safe to call twice.
        using var paths = new TempAppDataPathProvider();
        var units = new UnitsService(new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance),
                                     NullLogger<UnitsService>.Instance);
        var vm = Vm(units);
        var hourly = new List<HourlyForecastPoint>
        {
            new(Local(16), 0.0, 0, true, 5),    // 0 °C
            new(Local(17), 10.0, 2, true, 20),  // 10 °C
        };
        vm.Apply(BundleWith(hourly, Local(16, 20)));
        Assert.Equal("0°C", vm.Entries[0].TemperatureDisplay);
        Assert.Equal("10°C", vm.Entries[1].TemperatureDisplay);

        vm.Dispose();
        vm.Dispose(); // idempotent — a second detach must not throw

        await units.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit);

        Assert.Equal("0°C", vm.Entries[0].TemperatureDisplay);  // detached — no rebuild on the disposed strip
        Assert.Equal("10°C", vm.Entries[1].TemperatureDisplay);
    }

    [Fact]
    public void A_units_change_after_Clear_does_not_repopulate_the_strip()
    {
        using var paths = new TempAppDataPathProvider();
        var units = new UnitsService(new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance),
                                     NullLogger<UnitsService>.Instance);
        var vm = Vm(units);
        vm.Apply(BundleWith(new List<HourlyForecastPoint> { new(Local(16), 19.0, 0, true, 0) }, Local(16, 5)));
        vm.Clear();

        units.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit).GetAwaiter().GetResult();

        Assert.Empty(vm.Entries);   // no retained points -> nothing to rebuild
    }
}
