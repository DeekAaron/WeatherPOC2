# Convert units locally rather than re-fetching from Open-Meteo

Units (Temperature, Wind Speed) are user-selectable and change is a frequent, purely-presentational action. We always request weather from Open-Meteo in canonical units (Celsius, km/h) and convert to the user's chosen units in-app for display, rather than passing the units to Open-Meteo and re-requesting on every change. This keeps unit switching instant and offline-safe (no network call to change °C to °F), and localises unit conversion into a small deterministic component that is trivially unit-testable — at the cost of the app owning conversion correctness instead of delegating it to the API.

## Consequences
- A unit change never triggers a network request and cannot fail; it re-renders already-held data.
- Conversion logic (temperature formula; wind-speed factors for mph, m/s, knots) lives in-app and must be covered by unit tests.
- The cached/loaded weather for a Location is always stored in canonical units; only the display layer applies the user's chosen units.

## Lineage note — WeatherBundle incremental extension (2026-07-22)

The `WeatherBundle` record is the in-app carrier of that canonical data. It is **extended, not reshaped**, as each Feature adds a canonical measure (F1: temperature; F2: wind speed, current-hour chance of rain, and the raw `weather_code`/`is_day` hints). Widening this record is source-breaking to its (internal-only) consumers, which Technical-Context Principle #5 would normally gate behind an ADR. This note records the standing decision (taken at F2's `/feature-doc-gauntlet` fix pass, 2026-07-22): **incremental widening of `WeatherBundle` in canonical units, with all consumers internal to `WeatherPoc2.Core` + `WeatherPoc2.App` and updated in the same Feature, is planned additive evolution under F1's "extend the full-shape seam, do not reshape" approach and does not require a fresh ADR per Feature.** A change that *reshaped* the record (removed/renamed a field, or changed a canonical unit) would still require its own ADR.
