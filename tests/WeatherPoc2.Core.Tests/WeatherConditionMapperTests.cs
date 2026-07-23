using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class WeatherConditionMapperTests
{
    private static readonly WeatherConditionMapper Mapper = new();

    [Theory]
    [InlineData(0, WeatherCondition.Clear)]
    [InlineData(1, WeatherCondition.Clear)]
    [InlineData(2, WeatherCondition.PartlyCloudy)]
    [InlineData(3, WeatherCondition.Cloudy)]
    [InlineData(45, WeatherCondition.Fog)]
    [InlineData(48, WeatherCondition.Fog)]
    [InlineData(51, WeatherCondition.Drizzle)]
    [InlineData(53, WeatherCondition.Drizzle)]
    [InlineData(55, WeatherCondition.Drizzle)]
    [InlineData(61, WeatherCondition.Rain)]
    [InlineData(63, WeatherCondition.Rain)]
    [InlineData(65, WeatherCondition.Rain)]
    [InlineData(80, WeatherCondition.RainShowers)]
    [InlineData(81, WeatherCondition.RainShowers)]
    [InlineData(82, WeatherCondition.RainShowers)]
    [InlineData(71, WeatherCondition.Snow)]
    [InlineData(73, WeatherCondition.Snow)]
    [InlineData(75, WeatherCondition.Snow)]
    [InlineData(77, WeatherCondition.Snow)]
    [InlineData(56, WeatherCondition.Snow)]   // freezing drizzle folds into Snow
    [InlineData(57, WeatherCondition.Snow)]
    [InlineData(66, WeatherCondition.Snow)]   // freezing rain folds into Snow
    [InlineData(67, WeatherCondition.Snow)]
    [InlineData(85, WeatherCondition.SnowShowers)]
    [InlineData(86, WeatherCondition.SnowShowers)]
    [InlineData(95, WeatherCondition.Thunderstorm)]
    [InlineData(96, WeatherCondition.Thunderstorm)]
    [InlineData(99, WeatherCondition.Thunderstorm)]
    public void Map_collapses_each_WMO_code_to_its_curated_condition(int code, WeatherCondition expected)
    {
        Assert.Equal(expected, Mapper.Map(code, isDay: true).Condition);
        Assert.True(Mapper.Map(code, isDay: true).Recognized);
    }

    [Theory]
    [InlineData(0, true, WeatherIconKeys.ClearDay)]
    [InlineData(0, false, WeatherIconKeys.ClearNight)]
    [InlineData(0, null, WeatherIconKeys.ClearDay)]      // null is_day => day variant
    [InlineData(2, true, WeatherIconKeys.PartlyCloudyDay)]
    [InlineData(2, false, WeatherIconKeys.PartlyCloudyNight)]
    [InlineData(80, false, WeatherIconKeys.RainShowersNight)]
    [InlineData(85, true, WeatherIconKeys.SnowShowersDay)]
    [InlineData(3, true, WeatherIconKeys.Cloudy)]        // single-variant: is_day ignored
    [InlineData(3, false, WeatherIconKeys.Cloudy)]
    [InlineData(61, false, WeatherIconKeys.Rain)]
    public void Map_selects_the_day_or_night_icon_variant(int code, bool? isDay, string expectedKey)
    {
        Assert.Equal(expectedKey, Mapper.Map(code, isDay).IconKey);
    }

    [Theory]
    [InlineData(4)]      // gap in the WMO table
    [InlineData(100)]    // above the table
    [InlineData(null)]   // absent on the wire
    public void Map_falls_back_to_Unknown_for_an_unlisted_or_null_code(int? code)
    {
        var result = Mapper.Map(code, isDay: true);
        Assert.Equal(WeatherCondition.Unknown, result.Condition);
        Assert.Equal(WeatherIconKeys.Unknown, result.IconKey);
        Assert.False(result.Recognized);
    }

    [Fact]
    public void Map_supplies_a_human_display_name()
    {
        Assert.Equal("Partly cloudy", Mapper.Map(2, isDay: true).DisplayName);
        Assert.Equal("Rain showers", Mapper.Map(80, isDay: true).DisplayName);
        Assert.Equal("Unknown", Mapper.Map(null, isDay: true).DisplayName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    [InlineData(null)]
    public void Every_emitted_icon_key_is_a_declared_WeatherIconKey(int? code)
    {
        foreach (var isDay in new bool?[] { true, false, null })
            Assert.Contains(Mapper.Map(code, isDay).IconKey, WeatherIconKeys.All);
    }
}
