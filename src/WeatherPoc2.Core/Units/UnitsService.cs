using WeatherPoc2.Core.Persistence;
using Microsoft.Extensions.Logging;

namespace WeatherPoc2.Core.Units;

/// <summary>
/// Singleton <see cref="IUnitsService"/>. Starts at <see cref="UnitPreferences.Default"/> so
/// <see cref="Current"/> is always valid even before <see cref="InitializeAsync"/> runs or if the read
/// fails (D5). A setter updates in-memory state and raises <see cref="Changed"/> synchronously (instant
/// re-render), then persists — a save failure is logged by the store, never surfaced (ADR-0001).
/// </summary>
/// <remarks>
/// <para><b>Thread-affinity contract (Spec <i>UI-thread affinity</i> clause).</b> <see cref="Changed"/>
/// is raised <b>synchronously</b> on whatever thread called the mutator — this type does not marshal.
/// Its handlers mutate MAUI data-bound state, so the <i>caller</i> must invoke on the MAUI
/// UI/dispatcher thread: a picker change already runs on the UI thread; the startup
/// <see cref="InitializeAsync"/> raise is marshalled by the App-head hook resuming on the UI thread.</para>
/// </remarks>
public sealed class UnitsService : IUnitsService
{
    private const string StorageKey = "units";

    private readonly IPersistenceStore _store;
    private readonly ILogger<UnitsService> _logger;

    public UnitsService(IPersistenceStore store, ILogger<UnitsService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public UnitPreferences Current { get; private set; } = UnitPreferences.Default;

    public event EventHandler? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await _store.LoadAsync<UnitPreferences>(StorageKey, cancellationToken);
        if (loaded is not null && loaded != Current)
        {
            Current = loaded;
            RaiseChanged();
        }
    }

    public Task SetTemperatureUnitAsync(TemperatureUnit unit, CancellationToken cancellationToken = default)
        => UpdateAsync(Current with { Temperature = unit }, cancellationToken);

    public Task SetWindSpeedUnitAsync(WindSpeedUnit unit, CancellationToken cancellationToken = default)
        => UpdateAsync(Current with { WindSpeed = unit }, cancellationToken);

    private async Task UpdateAsync(UnitPreferences next, CancellationToken cancellationToken)
    {
        if (next == Current)
            return; // value-equality no-op — no raise, no write

        Current = next;
        RaiseChanged();                                              // synchronous → instant re-render (ADR-0001)
        await _store.SaveAsync(StorageKey, next, cancellationToken); // store logs a write failure (D5)
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
