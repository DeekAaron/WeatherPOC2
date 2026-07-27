using WeatherPoc2.Core.Units;
using Xunit;

namespace WeatherPoc2.Core.Tests.Units;

public class UnitPreferencesTests
{
    [Fact]
    public void Default_is_the_canonical_units_celsius_and_kilometres_per_hour()
    {
        Assert.Equal(TemperatureUnit.Celsius, UnitPreferences.Default.Temperature);
        Assert.Equal(WindSpeedUnit.KilometresPerHour, UnitPreferences.Default.WindSpeed);
    }

    [Fact]
    public void Two_preferences_with_the_same_units_are_equal()
    {
        // Value-equality (record) is the contract later Features rely on to decide a no-op change.
        var a = new UnitPreferences(TemperatureUnit.Fahrenheit, WindSpeedUnit.Knots);
        var b = new UnitPreferences(TemperatureUnit.Fahrenheit, WindSpeedUnit.Knots);
        Assert.Equal(a, b);
    }
}
