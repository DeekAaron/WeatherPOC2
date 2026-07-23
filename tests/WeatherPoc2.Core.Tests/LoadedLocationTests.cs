using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class LoadedLocationTests
{
    [Fact]
    public void Current_is_null_before_anything_is_loaded()
    {
        ILoadedLocation holder = new LoadedLocation();
        Assert.Null(holder.Current);
    }

    [Fact]
    public void Set_makes_Current_return_the_loaded_location()
    {
        ILoadedLocation holder = new LoadedLocation();
        var london = new Location(51.50853, -0.12574, "London, England, United Kingdom", 2643743);

        holder.Set(london);

        Assert.Equal(london, holder.Current);
    }
}
