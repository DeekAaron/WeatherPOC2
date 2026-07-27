using WeatherPoc2.Core.Units;
using Xunit;

namespace WeatherPoc2.Core.Tests.Units;

public class UnitFormatterTests
{
    private readonly UnitFormatter _formatter = new();

    [Theory]
    [InlineData(23.3, TemperatureUnit.Celsius, "23°C")]     // rounds down
    [InlineData(26.5, TemperatureUnit.Celsius, "27°C")]     // .5 rounds away from zero
    [InlineData(0.0, TemperatureUnit.Fahrenheit, "32°F")]
    [InlineData(-0.4, TemperatureUnit.Celsius, "0°C")]      // negative near zero collapses to 0, no sign
    [InlineData(-40.0, TemperatureUnit.Fahrenheit, "-40°F")] // the crossover, negative preserved
    public void FormatTemperature_is_a_whole_number_with_the_degree_suffix_and_no_space(
        double celsius, TemperatureUnit unit, string expected)
        => Assert.Equal(expected, _formatter.FormatTemperature(celsius, unit));

    [Theory]
    [InlineData(12.6, WindSpeedUnit.KilometresPerHour, "13 km/h")]  // rounds up, spaced suffix
    [InlineData(36.0, WindSpeedUnit.MilesPerHour, "22 mph")]
    [InlineData(36.0, WindSpeedUnit.MetresPerSecond, "10 m/s")]     // whole number, m/s included
    [InlineData(36.0, WindSpeedUnit.Knots, "19 kn")]
    [InlineData(0.0, WindSpeedUnit.KilometresPerHour, "0 km/h")]    // calm
    public void FormatWindSpeed_is_a_whole_number_with_a_spaced_suffix(
        double kmh, WindSpeedUnit unit, string expected)
        => Assert.Equal(expected, _formatter.FormatWindSpeed(kmh, unit));
}
