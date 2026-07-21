# Convert units locally rather than re-fetching from Open-Meteo

Units (Temperature, Wind Speed) are user-selectable and change is a frequent, purely-presentational action. We always request weather from Open-Meteo in canonical units (Celsius, km/h) and convert to the user's chosen units in-app for display, rather than passing the units to Open-Meteo and re-requesting on every change. This keeps unit switching instant and offline-safe (no network call to change °C to °F), and localises unit conversion into a small deterministic component that is trivially unit-testable — at the cost of the app owning conversion correctness instead of delegating it to the API.

## Consequences
- A unit change never triggers a network request and cannot fail; it re-renders already-held data.
- Conversion logic (temperature formula; wind-speed factors for mph, m/s, knots) lives in-app and must be covered by unit tests.
- The cached/loaded weather for a Location is always stored in canonical units; only the display layer applies the user's chosen units.
