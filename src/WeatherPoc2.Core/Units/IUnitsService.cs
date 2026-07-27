namespace WeatherPoc2.Core.Units;

/// <summary>
/// The single owner of "what units are we in". Holds the current <see cref="UnitPreferences"/>, loads
/// and saves them through the Persistence Store, and raises <see cref="Changed"/> so the weather
/// ViewModels re-format held canonical data (ADR-0001: no re-fetch, no network, cannot fail).
/// </summary>
public interface IUnitsService
{
    UnitPreferences Current { get; }
    event EventHandler? Changed;
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SetTemperatureUnitAsync(TemperatureUnit unit, CancellationToken cancellationToken = default);
    Task SetWindSpeedUnitAsync(WindSpeedUnit unit, CancellationToken cancellationToken = default);
}
