using NSubstitute;
using WeatherPoc2.Core.Units;
using WeatherPoc2.Core.ViewModels;
using Xunit;

namespace WeatherPoc2.Core.Tests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public void Initial_selections_reflect_the_current_units()
    {
        var units = Substitute.For<IUnitsService>();
        units.Current.Returns(new UnitPreferences(TemperatureUnit.Fahrenheit, WindSpeedUnit.Knots));

        var vm = new SettingsViewModel(units);

        Assert.Equal(TemperatureUnit.Fahrenheit, vm.TemperatureUnit);
        Assert.Equal(WindSpeedUnit.Knots, vm.WindSpeedUnit);
    }

    [Fact]
    public void Options_expose_every_enum_value()
    {
        var units = Substitute.For<IUnitsService>();
        units.Current.Returns(UnitPreferences.Default);

        var vm = new SettingsViewModel(units);

        Assert.Equal(Enum.GetValues<TemperatureUnit>(), vm.TemperatureUnits);
        Assert.Equal(Enum.GetValues<WindSpeedUnit>(), vm.WindSpeedUnits);
    }

    [Fact]
    public void Changing_the_temperature_selection_calls_the_service()
    {
        var units = Substitute.For<IUnitsService>();
        units.Current.Returns(UnitPreferences.Default);
        var vm = new SettingsViewModel(units);

        vm.TemperatureUnit = TemperatureUnit.Fahrenheit;

        units.Received(1).SetTemperatureUnitAsync(TemperatureUnit.Fahrenheit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Changing_the_wind_selection_calls_the_service()
    {
        var units = Substitute.For<IUnitsService>();
        units.Current.Returns(UnitPreferences.Default);
        var vm = new SettingsViewModel(units);

        vm.WindSpeedUnit = WindSpeedUnit.MilesPerHour;

        units.Received(1).SetWindSpeedUnitAsync(WindSpeedUnit.MilesPerHour, Arg.Any<CancellationToken>());
    }
}
