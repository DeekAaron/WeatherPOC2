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

public class CurrentConditionsViewModelTests
{
    // The merged CurrentConditionsViewModel is display-only (Story #71) — no IWeatherGateway. It now also
    // takes the shared IUnitsService + the pure UnitFormatter so it formats its held bundle through the
    // current units and re-formats on IUnitsService.Changed (ADR-0001: no re-fetch, cannot fail).
    private static CurrentConditionsViewModel Vm(IUnitsService units,
        ILogger<CurrentConditionsViewModel>? logger = null)
        => new(new WeatherConditionMapper(), units, new UnitFormatter(),
               logger ?? NullLogger<CurrentConditionsViewModel>.Instance);

    // A units service fixed at the given preferences — the simple, deterministic case.
    private static IUnitsService FixedUnits(UnitPreferences prefs)
    {
        var units = Substitute.For<IUnitsService>();
        units.Current.Returns(prefs);
        return units;
    }

    // A widened bundle with an empty hourly series is fine here — this VM only reads current fields.
    private static WeatherBundle Bundle(double temp, double wind, int chance, int? code, bool? isDay)
        => new(temp, wind, chance, code, isDay, Array.Empty<HourlyForecastPoint>(), default);

    [Fact]
    public void Apply_populates_all_current_conditions_displays()
    {
        var vm = Vm(FixedUnits(UnitPreferences.Default));

        vm.Apply(Bundle(26.5, 12.6, 40, 2, false)); // code 2 partly cloudy, night

        Assert.Equal("27°C", vm.TemperatureDisplay);    // whole-number, away-from-zero (was "26.5 °C" pre-Units)
        Assert.Equal("40%", vm.ChanceOfRainDisplay);
        Assert.Equal("13 km/h", vm.WindSpeedDisplay);    // whole-number (was "12.6 km/h" pre-Units)
        Assert.Equal("Partly cloudy", vm.ConditionText);
        Assert.Equal("partly_cloudy_night.png", vm.IconSource);
    }

    [Fact]
    public void Apply_shows_temperature_and_wind_in_the_current_units_as_whole_numbers()
    {
        var vm = Vm(FixedUnits(UnitPreferences.Default)); // °C, km/h

        vm.Apply(Bundle(23.3, 10.0, 20, 0, true));

        Assert.Equal("23°C", vm.TemperatureDisplay);
        Assert.Equal("10 km/h", vm.WindSpeedDisplay);
    }

    [Fact]
    public async Task Changing_units_reformats_the_retained_bundle_with_no_second_apply()
    {
        // A real UnitsService over a temp-dir store + a real UnitFormatter (both deterministic).
        using var paths = new TempAppDataPathProvider();
        var units = new UnitsService(new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance),
                                     NullLogger<UnitsService>.Instance);
        var vm = Vm(units);
        vm.Apply(Bundle(0.0, 36.0, 20, 0, true)); // single Apply — the only bundle push
        Assert.Equal("0°C", vm.TemperatureDisplay);
        Assert.Equal("36 km/h", vm.WindSpeedDisplay);

        await units.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit);
        await units.SetWindSpeedUnitAsync(WindSpeedUnit.MilesPerHour);

        Assert.Equal("32°F", vm.TemperatureDisplay);   // 0°C -> 32°F, re-rendered from the retained bundle
        Assert.Equal("22 mph", vm.WindSpeedDisplay);   // 36 km/h -> 22 mph
        // network-free is structural: the display-only VM has no gateway dependency to call.
    }

    [Fact]
    public void Changing_units_does_not_affect_chance_of_rain()
    {
        using var paths = new TempAppDataPathProvider();
        var units = new UnitsService(new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance),
                                     NullLogger<UnitsService>.Instance);
        var vm = Vm(units);
        vm.Apply(Bundle(10.0, 10.0, 40, 0, true));
        Assert.Equal("40%", vm.ChanceOfRainDisplay);

        // Fire the async setter and drain it so the synchronous Changed raise has run.
        units.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit).GetAwaiter().GetResult();

        Assert.Equal("40%", vm.ChanceOfRainDisplay);   // Chance of Rain stays a percentage (PRD-45)
    }

    [Fact]
    public void Apply_logs_a_warning_and_shows_Unknown_when_the_weather_code_is_absent()
    {
        var logger = new CapturingLogger<CurrentConditionsViewModel>();
        var vm = Vm(FixedUnits(UnitPreferences.Default), logger);

        vm.Apply(Bundle(26.5, 12.6, 40, null, true));

        Assert.Equal("Unknown", vm.ConditionText);
        Assert.Equal("unknown.png", vm.IconSource);
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning && (e.Message.Contains("weather_code") || e.Message.Contains("Unknown")));
    }

    [Fact]
    public void Apply_logs_a_warning_and_shows_the_day_variant_when_is_day_is_absent()
    {
        var logger = new CapturingLogger<CurrentConditionsViewModel>();
        var vm = Vm(FixedUnits(UnitPreferences.Default), logger);

        vm.Apply(Bundle(26.5, 12.6, 40, 0, null));

        Assert.Equal("clear_day.png", vm.IconSource);
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("is_day"));
    }

    [Fact]
    public void Clear_blanks_every_display_so_no_stale_panel_shows()
    {
        var vm = Vm(FixedUnits(UnitPreferences.Default));
        vm.Apply(Bundle(26.5, 12.6, 40, 0, true));

        vm.Clear();

        Assert.Equal(string.Empty, vm.TemperatureDisplay);
        Assert.Null(vm.ChanceOfRainDisplay);
        Assert.Null(vm.WindSpeedDisplay);
        Assert.Null(vm.ConditionText);
        Assert.Null(vm.IconSource);
    }

    [Fact]
    public async Task Disposing_the_panel_detaches_it_so_a_units_change_no_longer_re_formats()
    {
        // The panel subscribes to IUnitsService.Changed in its ctor; the service is a singleton that
        // outlives this transient VM. Dispose() must detach that subscription so the disposed panel no
        // longer re-formats (no leak, no work on a dead instance). Idempotent: safe to call twice.
        using var paths = new TempAppDataPathProvider();
        var units = new UnitsService(new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance),
                                     NullLogger<UnitsService>.Instance);
        var vm = Vm(units);
        vm.Apply(Bundle(0.0, 36.0, 20, 0, true)); // °C / km/h — the retained canonical bundle
        Assert.Equal("0°C", vm.TemperatureDisplay);
        Assert.Equal("36 km/h", vm.WindSpeedDisplay);

        vm.Dispose();
        vm.Dispose(); // idempotent — a second detach must not throw

        await units.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit);
        await units.SetWindSpeedUnitAsync(WindSpeedUnit.MilesPerHour);

        Assert.Equal("0°C", vm.TemperatureDisplay);      // detached — no re-format on the disposed panel
        Assert.Equal("36 km/h", vm.WindSpeedDisplay);
    }

    [Fact]
    public void A_units_change_after_Clear_does_not_repopulate_the_panel()
    {
        // Clear drops the retained bundle, so a later Changed raise has nothing to re-format —
        // no stale panel reappears on the coordinator's failure path.
        using var paths = new TempAppDataPathProvider();
        var units = new UnitsService(new JsonPersistenceStore(paths, NullLogger<JsonPersistenceStore>.Instance),
                                     NullLogger<UnitsService>.Instance);
        var vm = Vm(units);
        vm.Apply(Bundle(20.0, 10.0, 40, 0, true));
        vm.Clear();

        units.SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit).GetAwaiter().GetResult();

        Assert.Equal(string.Empty, vm.TemperatureDisplay);
        Assert.Null(vm.WindSpeedDisplay);
    }
}
