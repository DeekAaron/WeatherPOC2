using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherPoc2.Core.ViewModels;
using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class CurrentConditionsViewModelTests
{
    private static CurrentConditionsViewModel VmWith(IWeatherGateway gateway)
        => new(gateway, NullLogger<CurrentConditionsViewModel>.Instance);

    [Fact]
    public async Task Load_shows_the_temperature_and_no_error_on_success()
    {
        var gateway = Substitute.For<IWeatherGateway>();
        gateway.GetWeatherAsync(Arg.Any<Location>(), Arg.Any<CancellationToken>())
               .Returns(new WeatherBundle(23.3));
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
}
