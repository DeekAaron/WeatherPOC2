namespace WeatherPoc2.Core.Weather;

/// <summary>
/// Pure, deterministic Hourly Forecast window (Context.MD "Hourly Window"; ADR-free, I/O-free).
/// Given the Location-local hourly series and the Location-local "now", returns the ordered slice
/// from the current hour to the next upcoming 05:00 local, inclusive of the 05:00 hour, never
/// including past hours. Because it filters the actual returned local hours, a DST-transition day
/// (a 23- or 25-hour day) is handled naturally — it never assumes a fixed 24.
/// </summary>
public sealed class HourlyWindow
{
    private const int PerceptualDayCutoffHour = 5; // 05:00 local — the last hour that belongs to "today".

    public IReadOnlyList<HourlyForecastPoint> Compute(
        IReadOnlyList<HourlyForecastPoint> series, DateTime localNow)
    {
        var currentHour = new DateTime(
            localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0, DateTimeKind.Unspecified);
        var cutoff = NextCutoffAtOrAfter(currentHour);

        return series
            .Where(p => p.LocalTime >= currentHour && p.LocalTime <= cutoff)
            .OrderBy(p => p.LocalTime)
            .ToList();
    }

    // The earliest 05:00 that is >= the current hour: today's 05:00 while at/before it, else tomorrow's.
    private static DateTime NextCutoffAtOrAfter(DateTime currentHour)
    {
        var today0500 = new DateTime(
            currentHour.Year, currentHour.Month, currentHour.Day,
            PerceptualDayCutoffHour, 0, 0, DateTimeKind.Unspecified);
        return currentHour <= today0500 ? today0500 : today0500.AddDays(1);
    }
}
