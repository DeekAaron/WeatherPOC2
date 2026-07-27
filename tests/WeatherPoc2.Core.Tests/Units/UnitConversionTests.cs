using WeatherPoc2.Core.Units;
using Xunit;

namespace WeatherPoc2.Core.Tests.Units;

public class UnitConversionTests
{
    [Theory]
    [InlineData(0.0, TemperatureUnit.Celsius, 0.0)]        // canonical pass-through
    [InlineData(23.3, TemperatureUnit.Celsius, 23.3)]
    [InlineData(0.0, TemperatureUnit.Fahrenheit, 32.0)]    // freezing boundary
    [InlineData(100.0, TemperatureUnit.Fahrenheit, 212.0)] // boiling boundary
    [InlineData(-40.0, TemperatureUnit.Fahrenheit, -40.0)] // the crossover
    [InlineData(-10.0, TemperatureUnit.Fahrenheit, 14.0)]  // negative celsius, positive fahrenheit
    public void ToDisplayTemperature_converts_from_canonical_celsius(double celsius, TemperatureUnit unit, double expected)
        => Assert.Equal(expected, UnitConversion.ToDisplayTemperature(celsius, unit), precision: 5);

    [Theory]
    [InlineData(0.0, WindSpeedUnit.KilometresPerHour, 0.0)]     // canonical pass-through, zero
    [InlineData(36.0, WindSpeedUnit.KilometresPerHour, 36.0)]
    [InlineData(36.0, WindSpeedUnit.MilesPerHour, 22.369356)]
    [InlineData(36.0, WindSpeedUnit.MetresPerSecond, 10.0)]
    [InlineData(36.0, WindSpeedUnit.Knots, 19.438452)]
    public void ToDisplayWindSpeed_converts_from_canonical_kmh(double kmh, WindSpeedUnit unit, double expected)
        => Assert.Equal(expected, UnitConversion.ToDisplayWindSpeed(kmh, unit), precision: 5);
}
