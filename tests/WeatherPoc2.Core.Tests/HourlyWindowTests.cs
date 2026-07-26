using WeatherPoc2.Core.Weather;
using Xunit;

namespace WeatherPoc2.Core.Tests;

public class HourlyWindowTests
{
    private static readonly HourlyWindow Window = new();

    // Build a contiguous hourly series of `count` points starting at `start` (one per hour).
    private static IReadOnlyList<HourlyForecastPoint> Series(DateTime start, int count)
    {
        var list = new List<HourlyForecastPoint>();
        for (var i = 0; i < count; i++)
            list.Add(new HourlyForecastPoint(start.AddHours(i), 15.0, 1, true, 0));
        return list;
    }

    private static DateTime Local(int y, int mo, int d, int h, int mi = 0)
        => new(y, mo, d, h, mi, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Mid_afternoon_runs_from_current_hour_to_tomorrow_0500_inclusive()
    {
        // now 16:20 -> first entry 16:00, last entry tomorrow 05:00 inclusive.
        var series = Series(Local(2026, 7, 22, 0), 48);                 // two full local days
        var result = Window.Compute(series, Local(2026, 7, 22, 16, 20));

        Assert.Equal(Local(2026, 7, 22, 16), result[0].LocalTime);
        Assert.Equal(Local(2026, 7, 23, 5), result[^1].LocalTime);
        Assert.Equal(14, result.Count);                                 // 16:00..23:00 (8) + 00:00..05:00 (6)
    }

    [Fact]
    public void Pre_dawn_is_a_short_strip_to_this_mornings_0500()
    {
        var series = Series(Local(2026, 7, 22, 0), 48);
        var result = Window.Compute(series, Local(2026, 7, 22, 4, 10));

        Assert.Equal(Local(2026, 7, 22, 4), result[0].LocalTime);
        Assert.Equal(Local(2026, 7, 22, 5), result[^1].LocalTime);
        Assert.Equal(2, result.Count);                                  // 04:00, 05:00
    }

    [Fact]
    public void At_the_0500_hour_the_strip_is_a_single_entry()
    {
        // The settled >= rule: while it IS the 05:00 hour, today has one hour left.
        var series = Series(Local(2026, 7, 22, 0), 48);
        var result = Window.Compute(series, Local(2026, 7, 22, 5, 40));

        Assert.Single(result);
        Assert.Equal(Local(2026, 7, 22, 5), result[0].LocalTime);
    }

    [Fact]
    public void At_0600_the_strip_reopens_to_a_full_day()
    {
        var series = Series(Local(2026, 7, 22, 0), 48);
        var result = Window.Compute(series, Local(2026, 7, 22, 6, 5));

        Assert.Equal(Local(2026, 7, 22, 6), result[0].LocalTime);
        Assert.Equal(Local(2026, 7, 23, 5), result[^1].LocalTime);
        Assert.Equal(24, result.Count);                                 // 06:00..05:00 next day
    }

    [Fact]
    public void Never_includes_past_hours()
    {
        var series = Series(Local(2026, 7, 22, 0), 48);
        var result = Window.Compute(series, Local(2026, 7, 22, 16, 20));

        Assert.All(result, p => Assert.True(p.LocalTime >= Local(2026, 7, 22, 16)));
    }

    [Fact]
    public void A_dst_short_day_is_handled_by_filtering_the_actual_returned_hours()
    {
        // UK spring-forward: 2026-03-29 has no 01:00 local hour (clocks jump 00:59 -> 02:00).
        // The module must not assume a fixed 24 — it filters whatever local hours were returned.
        var series = new List<HourlyForecastPoint>
        {
            new(Local(2026, 3, 29, 0), 5.0, 1, false, 0),
            // 01:00 does not exist this day — Open-Meteo omits it
            new(Local(2026, 3, 29, 2), 5.0, 1, false, 0),
            new(Local(2026, 3, 29, 3), 6.0, 1, true, 0),
            new(Local(2026, 3, 29, 4), 7.0, 1, true, 0),
            new(Local(2026, 3, 29, 5), 8.0, 1, true, 0),
        };
        var result = Window.Compute(series, Local(2026, 3, 29, 0, 30));

        // 00:00 -> 05:00 inclusive. The series carries the 5 real hours this DST day returned
        // (00,02,03,04,05); the 01:00 hour is genuinely absent and is never fabricated. The
        // module returns exactly those 5 — proving it filters the actual returned hours rather
        // than assuming a fixed 24/its own hour count.
        Assert.Equal(5, result.Count);
        Assert.DoesNotContain(result, p => p.LocalTime == Local(2026, 3, 29, 1));
        Assert.Equal(Local(2026, 3, 29, 5), result[^1].LocalTime);
    }
}
