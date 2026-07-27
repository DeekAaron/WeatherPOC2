using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Units;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.ViewModels;

/// <summary>
/// Display-only ViewModel for the Hourly Forecast strip (Approach A child). The parent
/// <c>WeatherViewModel</c> coordinator pushes the shared bundle in via <see cref="Apply"/>. Runs the
/// pure <see cref="HourlyWindow"/>, maps each windowed point through the pure
/// <see cref="WeatherConditionMapper"/>, and formats the variant-A cell. A null measure renders "—"
/// and is logged as a Warning (Spec D3, Principle #1 fail-visible); the strip stays contiguous.
///
/// Units (Feature 5): each entry's Temperature is formatted through <see cref="UnitFormatter"/> +
/// <see cref="IUnitsService.Current"/>. Hourly entries carry Time · Icon · Temperature · Chance — no
/// Wind Speed — so only Temperature is units-affected. The VM retains the windowed canonical points and
/// <b>rebuilds</b> the collection on <see cref="IUnitsService.Changed"/> (items are immutable records) —
/// no re-fetch, cannot fail (ADR-0001). Time, icon, and Chance are unchanged across a units switch.
/// </summary>
public sealed class HourlyForecastViewModel : IDisposable
{
    private const string Placeholder = "—";

    private readonly WeatherConditionMapper _mapper;
    private readonly HourlyWindow _window;
    private readonly IUnitsService _units;
    private readonly UnitFormatter _formatter;
    private readonly ILogger<HourlyForecastViewModel> _logger;

    // The retained windowed canonical points + the current hour, re-projected on a units change. Empty
    // before the first Apply and after Clear (so a later Changed raise rebuilds nothing — no stale strip).
    private IReadOnlyList<HourlyForecastPoint> _points = Array.Empty<HourlyForecastPoint>();
    private DateTime _currentHour;

    // The Changed handler held in a field so it is removable in Dispose. This VM is transient while
    // IUnitsService is a singleton, so a throwaway lambda would root every dead instance forever.
    private readonly EventHandler _onUnitsChanged;
    private bool _disposed;

    public HourlyForecastViewModel(
        WeatherConditionMapper mapper,
        HourlyWindow window,
        IUnitsService units,
        UnitFormatter formatter,
        ILogger<HourlyForecastViewModel> logger)
    {
        _mapper = mapper;
        _window = window;
        _units = units;
        _formatter = formatter;
        _logger = logger;
        _onUnitsChanged = (_, _) => Rebuild(); // re-format held data; no re-fetch (ADR-0001)
        _units.Changed += _onUnitsChanged;
    }

    /// <summary>The ordered hourly strip cells for the current window. Rebuilt each <see cref="Apply"/>.</summary>
    public ObservableCollection<HourlyForecastItem> Entries { get; } = new();

    /// <summary>
    /// Rebuilds the strip from the shared bundle: runs the pure window over the local hourly series and
    /// retains the windowed points, then projects each cell. Replaces any prior entries. A null
    /// temperature/chance renders "—" + a logged Warning; an unrecognized/absent weather_code or absent
    /// is_day also logs a Warning (fail-visible, Principle #1). The current hour's cell is flagged "Now".
    /// </summary>
    public void Apply(WeatherBundle bundle)
    {
        _currentHour = new DateTime(
            bundle.LocalNow.Year, bundle.LocalNow.Month, bundle.LocalNow.Day,
            bundle.LocalNow.Hour, 0, 0, DateTimeKind.Unspecified);
        _points = _window.Compute(bundle.Hourly, bundle.LocalNow);
        Rebuild();
    }

    /// <summary>Empties the strip (used on the coordinator's failure path so no stale strip shows).
    /// Drops the retained points so a later units change does not repopulate a cleared strip.</summary>
    public void Clear()
    {
        _points = Array.Empty<HourlyForecastPoint>();
        Entries.Clear();
    }

    /// <summary>
    /// Projects the retained windowed points into the strip, formatting each entry's Temperature through
    /// the current units. Called from <see cref="Apply"/> and on every <see cref="IUnitsService.Changed"/>.
    /// Rebuilds the collection (items are immutable records) rather than mutating; a no-op strip when no
    /// points are retained. Time, icon, and Chance formatting are unchanged across a units switch.
    /// </summary>
    private void Rebuild()
    {
        Entries.Clear();

        foreach (var point in _points)
        {
            var condition = _mapper.Map(point.WeatherCode, point.IsDay);
            if (!condition.Recognized)
                _logger.LogWarning("Hourly Forecast {Time}: unrecognized/absent weather_code {Code} → Unknown icon",
                    point.LocalTime, point.WeatherCode);
            if (point.IsDay is null)
                _logger.LogWarning("Hourly Forecast {Time}: is_day absent → defaulting to the day icon variant", point.LocalTime);

            var temp = point.TemperatureCelsius is double t
                ? _formatter.FormatTemperature(t, _units.Current.Temperature)
                : Warn(point.LocalTime, "temperature");
            var chance = point.ChanceOfRainPercent is int c
                ? c.ToString(CultureInfo.InvariantCulture) + "%"
                : Warn(point.LocalTime, "chance of rain");

            Entries.Add(new HourlyForecastItem(
                TimeDisplay: point.LocalTime.ToString("HH", CultureInfo.InvariantCulture) + ":00",
                IconSource: $"{condition.IconKey}.png",
                TemperatureDisplay: temp,
                ChanceOfRainDisplay: chance,
                IsNow: point.LocalTime == _currentHour));
        }
    }

    private string Warn(DateTime time, string measure)
    {
        _logger.LogWarning("Hourly Forecast {Time}: {Measure} absent → showing placeholder", time, measure);
        return Placeholder;
    }

    /// <summary>
    /// Detaches the <see cref="IUnitsService.Changed"/> subscription so the singleton service no longer
    /// roots this transient strip once its page tears down (no leak, no rebuild/warning logs on a dead
    /// instance). Idempotent — safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _units.Changed -= _onUnitsChanged;
    }
}
