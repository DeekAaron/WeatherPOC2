using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class HourlyForecastViewModelTests
{
    private static HourlyForecastViewModel Vm(ILogger<HourlyForecastViewModel>? logger = null)
        => new(new WeatherConditionMapper(), new HourlyWindow(), logger ?? NullLogger<HourlyForecastViewModel>.Instance);

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
        var vm = Vm();

        vm.Apply(BundleWith(hourly, Local(16, 20)));

        Assert.Equal(3, vm.Entries.Count);
        Assert.Equal("16:00", vm.Entries[0].TimeDisplay);
        Assert.Equal("19°", vm.Entries[0].TemperatureDisplay);   // whole-degree, variant A
        Assert.Equal("5%", vm.Entries[0].ChanceOfRainDisplay);
        Assert.Equal("cloudy.png", vm.Entries[0].IconSource);    // code 3 -> cloudy
        Assert.True(vm.Entries[0].IsNow);
        Assert.False(vm.Entries[1].IsNow);
        Assert.Equal("clear_night.png", vm.Entries[2].IconSource); // code 0 + night
    }

    [Fact]
    public void Apply_renders_a_placeholder_and_logs_a_warning_for_a_null_measure()
    {
        var hourly = new List<HourlyForecastPoint>
        {
            new(Local(16), null, 3, true, null),   // temperature AND chance absent (soft gap)
        };
        var logger = new CapturingLogger<HourlyForecastViewModel>();
        var vm = Vm(logger);

        vm.Apply(BundleWith(hourly, Local(16, 20)));

        Assert.Equal("—", vm.Entries[0].TemperatureDisplay);
        Assert.Equal("—", vm.Entries[0].ChanceOfRainDisplay);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void Apply_replaces_prior_entries_on_each_call()
    {
        var vm = Vm();
        vm.Apply(BundleWith(new List<HourlyForecastPoint> { new(Local(16), 19.0, 0, true, 0) }, Local(16, 5)));
        vm.Apply(BundleWith(new List<HourlyForecastPoint> { new(Local(4), 9.0, 0, true, 0), new(Local(5), 8.0, 0, true, 0) }, Local(4, 5)));

        Assert.Equal(2, vm.Entries.Count);
        Assert.Equal("04:00", vm.Entries[0].TimeDisplay);
    }

    [Fact]
    public void Clear_empties_the_entries()
    {
        var vm = Vm();
        vm.Apply(BundleWith(new List<HourlyForecastPoint> { new(Local(16), 19.0, 0, true, 0) }, Local(16, 5)));

        vm.Clear();

        Assert.Empty(vm.Entries);
    }
}
