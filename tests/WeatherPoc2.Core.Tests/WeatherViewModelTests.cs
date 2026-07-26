using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.Navigation;
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
        INavigator? navigator = null)
    {
        // Feature 3: the coordinator fetches for the loaded Location — default a resolved one so the
        // fetch-path tests still exercise a real fetch (they no longer depend on a hard-coded London).
        loaded ??= LoadedWith(Location.LondonGb);
        var current = new CurrentConditionsViewModel(new WeatherConditionMapper(), NullLogger<CurrentConditionsViewModel>.Instance);
        var hourly = new HourlyForecastViewModel(new WeatherConditionMapper(), new HourlyWindow(), NullLogger<HourlyForecastViewModel>.Instance);
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
        Assert.Equal("26.5 °C", vm.CurrentConditions.TemperatureDisplay);
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
    public async Task OpenSearch_navigates_to_the_search_screen()
    {
        var navigator = Substitute.For<INavigator>();
        var vm = Vm(Substitute.For<IWeatherGateway>(), navigator: navigator);

        await vm.OpenSearchCommand.ExecuteAsync(null);

        await navigator.Received(1).GoToSearchAsync();
    }
}
