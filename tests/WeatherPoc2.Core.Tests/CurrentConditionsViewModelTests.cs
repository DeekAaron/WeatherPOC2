using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class CurrentConditionsViewModelTests
{
    // The ViewModel now derives the condition/icon via the real (pure) mapper — exercised real, not faked.
    private static CurrentConditionsViewModel VmWith(IWeatherGateway gateway)
        => new(gateway, new WeatherConditionMapper(), NullLogger<CurrentConditionsViewModel>.Instance);

    [Fact]
    public async Task Load_shows_the_temperature_and_no_error_on_success()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>())
               .Returns(new WeatherBundle(23.3, 10.0, 20, 0, true));
        var vm = VmWith(gateway);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("23.3 °C", vm.TemperatureDisplay);
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Load_shows_a_friendly_error_and_no_temperature_on_failure()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>())
               .Returns<WeatherBundle>(_ => throw new WeatherUnavailableException("boom"));
        var vm = VmWith(gateway);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.TemperatureDisplay);
        Assert.Equal("Couldn't reach the weather service — check your connection and try again.", vm.ErrorMessage);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task Load_populates_all_current_conditions_displays()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>())
               .Returns(new WeatherBundle(26.5, 12.6, 40, 2, false)); // weatherCode 2 (partly cloudy), is_day night
        var vm = VmWith(gateway);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("40%", vm.ChanceOfRainDisplay);
        Assert.Equal("12.6 km/h", vm.WindSpeedDisplay);
        Assert.Equal("Partly cloudy", vm.ConditionText);
        Assert.Equal("partly_cloudy_night.png", vm.IconSource);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task Load_logs_a_warning_and_shows_Unknown_when_the_weather_code_is_absent()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>())
               .Returns(new WeatherBundle(26.5, 12.6, 40, null, true)); // weatherCode absent -> Unknown; is_day day
        var logger = new CapturingLogger<CurrentConditionsViewModel>();
        var vm = new CurrentConditionsViewModel(gateway, new WeatherConditionMapper(), logger);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Unknown", vm.ConditionText);
        Assert.Equal("unknown.png", vm.IconSource);
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning && (e.Message.Contains("weather_code") || e.Message.Contains("Unknown")));
    }

    [Fact]
    public async Task Load_logs_a_warning_and_shows_the_day_variant_when_is_day_is_absent()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>())
               .Returns(new WeatherBundle(26.5, 12.6, 40, 0, null)); // clear; is_day absent -> day variant
        var logger = new CapturingLogger<CurrentConditionsViewModel>();
        var vm = new CurrentConditionsViewModel(gateway, new WeatherConditionMapper(), logger);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal("clear_day.png", vm.IconSource);
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("is_day"));
    }

    [Fact]
    public async Task Load_clears_every_display_on_failure_so_no_stale_panel_shows()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>())
               .Returns<WeatherBundle>(_ => throw new WeatherUnavailableException("boom: 503 °C 2026-07-22T17:00"));
        var vm = VmWith(gateway);

        await vm.LoadCommand.ExecuteAsync(null);

        // Security AC: all five measures cleared — no stale/partial reading presented as current.
        Assert.Equal(string.Empty, vm.TemperatureDisplay);
        Assert.Null(vm.ChanceOfRainDisplay);
        Assert.Null(vm.WindSpeedDisplay);
        Assert.Null(vm.ConditionText);
        Assert.Null(vm.IconSource);
        // Security AC: friendly copy only — no HTTP status, exception detail, unit string, or hour key leaked.
        Assert.Equal("Couldn't reach the weather service — check your connection and try again.", vm.ErrorMessage);
    }
}
