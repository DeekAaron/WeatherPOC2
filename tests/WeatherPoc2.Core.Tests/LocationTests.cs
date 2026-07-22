using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class LocationTests
{
    [Fact]
    public void LondonGb_has_resolved_coordinates_and_label_and_no_geocoded_id()
    {
        var london = Location.LondonGb;

        Assert.Equal(51.5074, london.Latitude);
        Assert.Equal(-0.1278, london.Longitude);
        Assert.Equal("London, GB", london.Label);
        Assert.Null(london.OpenMeteoId); // geocoding mints the id in Feature 3
    }
}
