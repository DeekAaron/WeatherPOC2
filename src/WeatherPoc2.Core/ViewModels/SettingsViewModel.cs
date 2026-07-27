using CommunityToolkit.Mvvm.ComponentModel;
using WeatherPoc2.Core.Units;

namespace WeatherPoc2.Core.ViewModels;

/// <summary>
/// The dedicated Settings/Units screen ViewModel (MVVM-only, Principle #2). Binds the two per-measure
/// selections to pickers; a selection routes to <see cref="IUnitsService"/>, which owns the state,
/// persistence, and the instant re-render. Holds no formatting or storage logic of its own.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IUnitsService _units;

    public SettingsViewModel(IUnitsService units)
    {
        _units = units;
        _temperatureUnit = units.Current.Temperature;
        _windSpeedUnit = units.Current.WindSpeed;
    }

    public IReadOnlyList<TemperatureUnit> TemperatureUnits { get; } = Enum.GetValues<TemperatureUnit>();
    public IReadOnlyList<WindSpeedUnit> WindSpeedUnits { get; } = Enum.GetValues<WindSpeedUnit>();

    [ObservableProperty] private TemperatureUnit _temperatureUnit;
    [ObservableProperty] private WindSpeedUnit _windSpeedUnit;

    // A picker change routes to the service (which owns state, persistence, and the instant re-render).
    // Fire-and-forget from the setter: the service updates Current synchronously before its first await
    // and logs any store-write failure — a unit change can't fail the user (D5 / ADR-0001).
    partial void OnTemperatureUnitChanged(TemperatureUnit value) => _ = _units.SetTemperatureUnitAsync(value);
    partial void OnWindSpeedUnitChanged(WindSpeedUnit value) => _ = _units.SetWindSpeedUnitAsync(value);
}
