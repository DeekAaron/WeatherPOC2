using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WeatherPoc2.Core.Units;
using WeatherPoc2.Core.Weather;

namespace WeatherPoc2.Core.ViewModels;

/// <summary>
/// Display-only ViewModel for the Current Conditions panel. It no longer fetches — the parent
/// <c>WeatherViewModel</c> coordinator owns the single GetWeather call and pushes the bundle in via
/// <see cref="Apply"/> (or blanks the panel via <see cref="Clear"/> on failure). Derives the
/// condition word + day/night icon through the pure mapper (ADR-0001 derive-for-display).
///
/// Units (Feature 5): Temperature and Wind Speed are formatted through <see cref="UnitFormatter"/> +
/// <see cref="IUnitsService.Current"/>. The VM retains the applied canonical bundle and re-formats it
/// on <see cref="IUnitsService.Changed"/> — no re-fetch, no network, cannot fail (ADR-0001). Chance of
/// Rain, condition text, and the icon are not units-affected (Chance of Rain stays a percentage).
/// </summary>
public sealed partial class CurrentConditionsViewModel : ObservableObject, IDisposable
{
    private readonly WeatherConditionMapper _mapper;
    private readonly IUnitsService _units;
    private readonly UnitFormatter _formatter;
    private readonly ILogger<CurrentConditionsViewModel> _logger;

    // The retained canonical bundle, re-formatted on a units change. Null before the first Apply and
    // after Clear (so a later Changed raise has nothing to re-format — no stale panel reappears).
    private WeatherBundle? _current;

    // The Changed handler held in a field so it is removable in Dispose. This VM is transient while
    // IUnitsService is a singleton, so a throwaway lambda would root every dead instance forever.
    private readonly EventHandler _onUnitsChanged;
    private bool _disposed;

    public CurrentConditionsViewModel(
        WeatherConditionMapper mapper,
        IUnitsService units,
        UnitFormatter formatter,
        ILogger<CurrentConditionsViewModel> logger)
    {
        _mapper = mapper;
        _units = units;
        _formatter = formatter;
        _logger = logger;
        _onUnitsChanged = (_, _) => FormatMeasures(); // re-render held data; no re-fetch (ADR-0001)
        _units.Changed += _onUnitsChanged;
    }

    [ObservableProperty] private string _temperatureDisplay = string.Empty;
    [ObservableProperty] private string? _chanceOfRainDisplay;
    [ObservableProperty] private string? _windSpeedDisplay;
    [ObservableProperty] private string? _conditionText;
    [ObservableProperty] private string? _iconSource;

    /// <summary>
    /// Populates the five display properties from the shared bundle the coordinator fetched. Retains the
    /// canonical bundle and formats Temperature/Wind Speed through the current units. Derives the
    /// condition word + day/night icon via the pure mapper; each lenient fall-back (unrecognized/absent
    /// weather_code, absent is_day) is logged as a Warning — never silent (Principle #1).
    /// </summary>
    public void Apply(WeatherBundle bundle)
    {
        _current = bundle;
        FormatMeasures();
        ChanceOfRainDisplay = $"{bundle.CurrentChanceOfRainPercent}%"; // not units-affected (PRD-45)

        var condition = _mapper.Map(bundle.CurrentWeatherCode, bundle.IsDay);
        ConditionText = condition.DisplayName;
        IconSource = $"{condition.IconKey}.png";

        // Fail-visible (Principle #1): lenient fall-backs are logged as Warnings, never silent.
        if (!condition.Recognized)
            _logger.LogWarning(
                "Current Conditions: unrecognized/absent weather_code {Code} → Unknown icon", bundle.CurrentWeatherCode);
        if (bundle.IsDay is null)
            _logger.LogWarning("Current Conditions: is_day absent → defaulting to the day icon variant");
    }

    /// <summary>
    /// Blanks every display so no stale/partial panel reads as current. The coordinator calls this on
    /// <c>WeatherUnavailableException</c>, alongside surfacing the single friendly error itself. Drops
    /// the retained bundle so a later units change does not repopulate a cleared panel.
    /// </summary>
    public void Clear()
    {
        _current = null;
        TemperatureDisplay = string.Empty;
        ChanceOfRainDisplay = null;
        WindSpeedDisplay = null;
        ConditionText = null;
        IconSource = null;
    }

    /// <summary>
    /// Formats the retained bundle's Temperature and Wind Speed through the current units. Called from
    /// <see cref="Apply"/> and on every <see cref="IUnitsService.Changed"/> — a no-op with no bundle held.
    /// </summary>
    private void FormatMeasures()
    {
        if (_current is null)
            return;
        TemperatureDisplay = _formatter.FormatTemperature(_current.CurrentTemperatureCelsius, _units.Current.Temperature);
        WindSpeedDisplay = _formatter.FormatWindSpeed(_current.CurrentWindSpeedKmh, _units.Current.WindSpeed);
    }

    /// <summary>
    /// Detaches the <see cref="IUnitsService.Changed"/> subscription so the singleton service no longer
    /// roots this transient panel once its page tears down (no leak, no re-formatting a dead instance).
    /// Idempotent — safe to call more than once.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _units.Changed -= _onUnitsChanged;
    }
}
