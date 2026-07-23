using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.Navigation;
using WeatherPoc2.Core.Tests.Support;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class CurrentConditionsViewModelTests
{
    // Feature 3 changes ONLY the Location source: the ViewModel reads ILoadedLocation.Current instead
    // of Location.LondonGb, and gains OpenSearchCommand. The Feature-2 five-display panel and the real
    // (pure) WeatherConditionMapper are preserved and still exercised real, not faked.
    private static readonly Location AnyLoaded =
        new(51.50853, -0.12574, "London, England, United Kingdom", 2643743);

    private static ILoadedLocation LoadedWith(Location location)
    {
        var holder = new LoadedLocation();
        holder.Set(location);
        return holder;
    }

    private static CurrentConditionsViewModel VmWith(IWeatherGateway gateway)
        => new(gateway, LoadedWith(AnyLoaded), new WeatherConditionMapper(),
               Substitute.For<INavigator>(), NullLogger<CurrentConditionsViewModel>.Instance);

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
    public async Task Load_uses_the_loaded_location()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>())
               .Returns(new WeatherBundle(23.3, 10.0, 20, 0, true));
        var vm = VmWith(gateway);

        await vm.LoadCommand.ExecuteAsync(null);

        await gateway.Received(1).GetWeatherAsync(AnyLoaded, Arg.Any<CancellationToken>());
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
        var vm = new CurrentConditionsViewModel(gateway, LoadedWith(AnyLoaded), new WeatherConditionMapper(),
                                                Substitute.For<INavigator>(), logger);

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
        var vm = new CurrentConditionsViewModel(gateway, LoadedWith(AnyLoaded), new WeatherConditionMapper(),
                                                Substitute.For<INavigator>(), logger);

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

        // All five measures cleared — no stale/partial reading presented as current.
        Assert.Equal(string.Empty, vm.TemperatureDisplay);
        Assert.Null(vm.ChanceOfRainDisplay);
        Assert.Null(vm.WindSpeedDisplay);
        Assert.Null(vm.ConditionText);
        Assert.Null(vm.IconSource);
        // Friendly copy only — no HTTP status, exception detail, unit string, or hour key leaked.
        Assert.Equal("Couldn't reach the weather service — check your connection and try again.", vm.ErrorMessage);
    }

    [Fact]
    public async Task Load_does_not_fetch_when_no_location_is_loaded()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        // Current == null (launch state — search shows first)
        var vm = new CurrentConditionsViewModel(gateway, new LoadedLocation(), new WeatherConditionMapper(),
                                                Substitute.For<INavigator>(), NullLogger<CurrentConditionsViewModel>.Instance);

        await vm.LoadCommand.ExecuteAsync(null);

        await gateway.DidNotReceive().GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>());
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task OpenSearch_requests_navigation_to_the_search_screen()
    {
        var navigator = Substitute.For<INavigator>();
        var vm = new CurrentConditionsViewModel(Substitute.For<IWeatherGateway>(), new LoadedLocation(),
                                                new WeatherConditionMapper(), navigator,
                                                NullLogger<CurrentConditionsViewModel>.Instance);

        await vm.OpenSearchCommand.ExecuteAsync(null);

        await navigator.Received(1).GoToSearchAsync();
    }
}
