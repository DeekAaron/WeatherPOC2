using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.ViewModels;

/// <summary>
/// Display-only ViewModel for the Hourly Forecast strip (Approach A child). The parent
/// <c>WeatherViewModel</c> coordinator pushes the shared bundle in via <see cref="Apply"/>. Runs the
/// pure <see cref="HourlyWindow"/>, maps each windowed point through the pure
/// <see cref="WeatherConditionMapper"/>, and formats the variant-A cell. A null measure renders "—"
/// and is logged as a Warning (Spec D3, Principle #1 fail-visible); the strip stays contiguous.
/// </summary>
public sealed class HourlyForecastViewModel
{
    private const string Placeholder = "—";

    private readonly WeatherConditionMapper _mapper;
    private readonly HourlyWindow _window;
    private readonly ILogger<HourlyForecastViewModel> _logger;

    public HourlyForecastViewModel(
        WeatherConditionMapper mapper,
        HourlyWindow window,
        ILogger<HourlyForecastViewModel> logger)
    {
        _mapper = mapper;
        _window = window;
        _logger = logger;
    }

    /// <summary>The ordered hourly strip cells for the current window. Rebuilt each <see cref="Apply"/>.</summary>
    public ObservableCollection<HourlyForecastItem> Entries { get; } = new();

    /// <summary>
    /// Rebuilds the strip from the shared bundle: runs the pure window over the local hourly series,
    /// maps each windowed point's icon via the mapper, and formats each cell. Replaces any prior
    /// entries. A null temperature/chance renders "—" + a logged Warning; an unrecognized/absent
    /// weather_code or absent is_day also logs a Warning (fail-visible, Principle #1). The current
    /// hour's cell is flagged for the "Now" treatment.
    /// </summary>
    public void Apply(WeatherBundle bundle)
    {
        Entries.Clear();

        var currentHour = new DateTime(
            bundle.LocalNow.Year, bundle.LocalNow.Month, bundle.LocalNow.Day,
            bundle.LocalNow.Hour, 0, 0, DateTimeKind.Unspecified);

        foreach (var point in _window.Compute(bundle.Hourly, bundle.LocalNow))
        {
            var condition = _mapper.Map(point.WeatherCode, point.IsDay);
            if (!condition.Recognized)
                _logger.LogWarning("Hourly Forecast {Time}: unrecognized/absent weather_code {Code} → Unknown icon",
                    point.LocalTime, point.WeatherCode);
            if (point.IsDay is null)
                _logger.LogWarning("Hourly Forecast {Time}: is_day absent → defaulting to the day icon variant", point.LocalTime);

            var temp = point.TemperatureCelsius is double t
                ? ((int)Math.Round(t)).ToString(CultureInfo.InvariantCulture) + "°"
                : Warn(point.LocalTime, "temperature");
            var chance = point.ChanceOfRainPercent is int c
                ? c.ToString(CultureInfo.InvariantCulture) + "%"
                : Warn(point.LocalTime, "chance of rain");

            Entries.Add(new HourlyForecastItem(
                TimeDisplay: point.LocalTime.ToString("HH", CultureInfo.InvariantCulture) + ":00",
                IconSource: $"{condition.IconKey}.png",
                TemperatureDisplay: temp,
                ChanceOfRainDisplay: chance,
                IsNow: point.LocalTime == currentHour));
        }
    }

    /// <summary>Empties the strip (used on the coordinator's failure path so no stale strip shows).</summary>
    public void Clear() => Entries.Clear();

    private string Warn(DateTime time, string measure)
    {
        _logger.LogWarning("Hourly Forecast {Time}: {Measure} absent → showing placeholder", time, measure);
        return Placeholder;
    }
}
