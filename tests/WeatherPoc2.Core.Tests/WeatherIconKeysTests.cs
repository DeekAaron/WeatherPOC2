using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class WeatherIconKeysTests
{
    [Fact]
    public void All_contains_exactly_the_fifteen_declared_icon_keys()
    {
        var expected = new HashSet<string>
        {
            "clear_day", "clear_night", "partly_cloudy_day", "partly_cloudy_night",
            "cloudy", "fog", "drizzle", "rain", "rain_showers_day", "rain_showers_night",
            "snow", "snow_showers_day", "snow_showers_night", "thunderstorm", "unknown",
        };

        Assert.Equal(expected, WeatherIconKeys.All);
    }
}
