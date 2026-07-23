# Changelog

All notable changes to WeatherPOC2 are recorded here. The **why** matters as much as the **what**.

## [Unreleased] - 2026-07-23

### Added
- **Weather Condition Mapper** (`WeatherPoc2.Core`) — a pure, deterministic `WeatherConditionMapper`
  whose `Map(weatherCode, isDay)` collapses Open-Meteo's numeric WMO weather codes onto the curated
  `WeatherCondition` enum and returns a `WeatherConditionResult` (condition, display name,
  icon-asset key, and a `Recognized` flag). Icon keys come from the new `WeatherIconKeys` — the
  single source of truth for the finite 15-key icon-asset set (four conditions carry day/night
  variants, six a single icon, plus the neutral `unknown`). The component does no I/O and no logging
  so it stays trivially unit-testable; a caller logs the lenient fall-back.
  - **Lenient fall-back, fail-visible at the caller** — an unlisted or `null` WMO code maps to
    `WeatherCondition.Unknown` (icon `unknown`) with `Recognized: false`, and a `null` `is_day`
    selects the day variant. The mapper never throws on unexpected input; it surfaces the
    unrecognized case via the `Recognized` flag so the caller can log it (Technical-Context
    Overriding Principle 1, fail-visible), rather than swallowing it silently.
  - **Freezing precipitation folds into Snow** — WMO 56/57 (freezing drizzle) and 66/67 (freezing
    rain) map to `Snow`, a deliberate curation of the WMO table onto the app's small condition set.
  - Covered by `WeatherConditionMapperTests` (every WMO code → condition, day/night icon selection,
    unknown/null fall-back, display names) and `WeatherIconKeysTests` (the icon-key set is exactly
    the 15 declared keys), Tier-1 and $0.

## [Unreleased] - 2026-07-22

### Added
- `LiveOpenMeteoTests` — a trait-gated (`[Trait("Tier","2-Live")]`) Tier-2 live drift-guard test
  that makes one real call to `api.open-meteo.com/v1/forecast` for `Location.LondonGb` through the
  real `OpenMeteoGateway`. It guards against the recorded Tier-1 fixtures drifting from the live
  Open-Meteo contract. The assertion is unit-aware by construction: the Gateway throws
  `WeatherUnavailableException` unless `current_units.temperature_2m == "°C"`, so a returned bundle
  proves the live response is in canonical Celsius — a server-side unit-default change fails the
  test rather than slipping past a loose plausibility band (a −60…60 sanity band sits on top).

### Changed
- Test runs are now split by trait so the live test never runs per-commit:
  `dotnet test --filter "Tier!=2-Live"` is the per-commit command (no network dependency) and
  `dotnet test --filter "Tier=2-Live"` is the scheduled (daily) job. Cost ceiling recorded in-file:
  ≤ 5 live calls per scheduled run, once per day — Open-Meteo is free and keyless, so the ceiling is
  call-volume, not money.

### Fixed
- `WeatherPoc2.App` desktop heads now have the MAUI platform scaffolding required to build and
  launch. The app head previously carried only shared code (`App`, `AppShell`, `MauiProgram`,
  `Views/`) with **no `Platforms/` or `Resources/` folders and no MAUI NuGet reference** — it had
  never been compiled, because the Linux AFK runner cannot build either desktop head. Surfaced by the
  HITL platform-verification story (#38), building on Windows exposed three gaps, now fixed:
  - **`WindowsPackageType=None`** (Windows-conditioned) so the head builds unpackaged (plain `.exe`,
    launched via `dotnet build -t:Run`); without it the WindowsAppSDK failed with *"no AppxManifest
    is specified, but WindowsPackageType is not set to MSIX."*
  - **`Platforms/Windows/`** (`App.xaml`/`App.xaml.cs` WinUI host + `app.manifest`) and
    **`Platforms/MacCatalyst/`** (`Program.cs` entry point, `AppDelegate.cs`, `Info.plist`) — the
    per-platform boot files. Without the Windows host the build failed with *"CS5001: Program does
    not contain a static 'Main'"*.
  - **`<PackageReference Include="Microsoft.Maui.Controls" />`** (implicit since .NET 8, warning
    MA002) plus a minimal `Resources/AppIcon` + `Resources/Splash`; without the package the build
    failed with *"ILoggingBuilder does not contain a definition for 'AddDebug'"*.

### Decisions
- No pipeline or schedule wiring is included — explicitly out of scope for this story and this
  Feature per the Plan. The trait is what makes the per-commit/scheduled split possible; the actual
  schedule lands later with the Feature's CI setup. This keeps the ratchet's seam-drift guard in the
  test suite now, without coupling the story to CI infrastructure that isn't this Feature's concern.
