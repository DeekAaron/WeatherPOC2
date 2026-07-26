using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class SearchCandidateTests
{
    [Fact]
    public void Label_is_Name_Region_Country_when_region_is_present()
    {
        var c = new SearchCandidate(2643743, "London", "England", "United Kingdom", 51.50853, -0.12574);
        Assert.Equal("London, England, United Kingdom", c.Label);
    }

    [Fact]
    public void Label_collapses_to_Name_Country_when_region_is_absent()
    {
        var c = new SearchCandidate(1880252, "Singapore", null, "Singapore", 1.28967, 103.85007);
        Assert.Equal("Singapore, Singapore", c.Label);
    }
}
