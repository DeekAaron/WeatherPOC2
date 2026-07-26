using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class CurrentConditionsViewModelTests
{
    private static CurrentConditionsViewModel Vm(ILogger<CurrentConditionsViewModel>? logger = null)
        => new(new WeatherConditionMapper(), logger ?? NullLogger<CurrentConditionsViewModel>.Instance);

    // A widened bundle with an empty hourly series is fine here — this VM only reads current fields.
    private static WeatherBundle Bundle(double temp, double wind, int chance, int? code, bool? isDay)
        => new(temp, wind, chance, code, isDay, Array.Empty<HourlyForecastPoint>(), default);

    [Fact]
    public void Apply_populates_all_current_conditions_displays()
    {
        var vm = Vm();

        vm.Apply(Bundle(26.5, 12.6, 40, 2, false)); // code 2 partly cloudy, night

        Assert.Equal("26.5 °C", vm.TemperatureDisplay);
        Assert.Equal("40%", vm.ChanceOfRainDisplay);
        Assert.Equal("12.6 km/h", vm.WindSpeedDisplay);
        Assert.Equal("Partly cloudy", vm.ConditionText);
        Assert.Equal("partly_cloudy_night.png", vm.IconSource);
    }

    [Fact]
    public void Apply_logs_a_warning_and_shows_Unknown_when_the_weather_code_is_absent()
    {
        var logger = new CapturingLogger<CurrentConditionsViewModel>();
        var vm = Vm(logger);

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
        var vm = Vm(logger);

        vm.Apply(Bundle(26.5, 12.6, 40, 0, null));

        Assert.Equal("clear_day.png", vm.IconSource);
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("is_day"));
    }

    [Fact]
    public void Clear_blanks_every_display_so_no_stale_panel_shows()
    {
        var vm = Vm();
        vm.Apply(Bundle(26.5, 12.6, 40, 0, true));

        vm.Clear();

        Assert.Equal(string.Empty, vm.TemperatureDisplay);
        Assert.Null(vm.ChanceOfRainDisplay);
        Assert.Null(vm.WindSpeedDisplay);
        Assert.Null(vm.ConditionText);
        Assert.Null(vm.IconSource);
    }
}
