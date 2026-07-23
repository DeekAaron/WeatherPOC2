# Changelog

All notable changes to WeatherPOC2 are recorded here. The **why** matters as much as the **what**.

## [Unreleased] - 2026-07-23

### Added
- **Geocoding search seam** (Story #64) — the second half of the Open-Meteo Gateway boundary the PRD
  always described (`Search(name)` → Search Candidates), now built. `IWeatherGateway` gains
  `SearchAsync(name, ct)` returning `IReadOnlyList<SearchCandidate>`, and `OpenMeteoGateway` implements it
  against the geocoding endpoint (`geocoding-api.open-meteo.com/v1/search`, fixed
  `count=10&language=en&format=json`).
  - **`SearchCandidate`** — a new record (`Id`, `Name`, `Region`, `Country`, `Latitude`, `Longitude`) whose
    `Label` reads "Name, Region, Country", collapsing to "Name, Country" when the Open-Meteo `admin1`
    (Region) is absent. It is a Search Candidate, not yet a Location — per Context.MD it becomes one only
    when the user picks it (PRD reqs 22–24).
  - **Typed, fail-visible failure** — every geocoding failure mode (transport/timeout, unparseable body,
    non-2xx status) is logged at Error with endpoint + outcome and converted to the new
    `LocationSearchUnavailableException`, kept distinct from `WeatherUnavailableException` so the two failure
    domains stay independently worded. This is what lets the app tell "no such place" from "couldn't reach
    the service" (PRD req 26; Technical-Context Overriding Principle 1).
  - **No match is not an error** — a 200 with the `results` key absent (proven live 2026-07-23) maps to an
    empty list, never an exception, so the search screen can render a plain "No matching places found" and
    stay put (PRD req 25).
  - **Query-parameter injection guarded** (security AC) — the untrusted `name` is percent-encoded via
    `Uri.EscapeDataString` inside the `name` value only, so a crafted query can neither inject an extra query
    parameter nor override the fixed count/format/language; locked by a regression test.
  - **Tier-2 live geocoding drift guard** — `LiveOpenMeteoTests` gains
    `Live_London_geocoding_returns_candidates`: one real geocoding call for "London" asserting the GB result
    resolves, guarding the recorded geocoding fixtures against live-contract drift. Trait-gated
    (`[Trait("Tier","2-Live")]`), never per-commit, within F1's ≤ 5 live calls/day ceiling.
  - Covered by three Tier-1 recorded-replay fixtures (`geocoding-london-200`, `geocoding-no-match-200`,
    `geocoding-singapore-admin1-absent-200`) plus `OpenMeteoGeocodingTests` / `SearchCandidateTests` —
    candidate mapping, label composition with and without Region, empty-on-no-match, each typed-failure path,
    request shape, the injection-encoding regression, and endpoint+outcome logging. $0, every commit.

- **Current Conditions Layout C panel + bundled weather icon assets** (Story #57) — the App-head
  presentation slice. `Views/CurrentConditionsPage` becomes the Layout C panel: a weather `Image`
  (bound to `IconSource`) + `ConditionText` + `TemperatureDisplay` header grid above stacked
  `ChanceOfRainDisplay` / `WindSpeedDisplay` rows, keeping all state in the ViewModel (MVVM-only,
  no code-behind logic added).
  - **15 self-authored SVG icons** land under `src/WeatherPoc2.App/Resources/Images/` — one per
    `WeatherIconKeys` member — registered with a `<MauiImage Include="Resources/Images/*.svg" />`
    glob so the resizetizer rasterizes each to a `{key}.png` the `Image.Source` binding resolves at
    runtime. Self-authored (not third-party) keeps the asset set license-clean and exactly aligned
    to the mapper's key set.
  - **Per-commit icon-asset guard** — `WeatherIconAssetsTests` asserts every declared
    `WeatherIconKeys.All` key has a matching source SVG in the tree. It is pure source-tree file I/O
    with no MAUI SDK dependency, so it runs in the Tier-1 per-commit suite ($0) on the AFK runner that
    cannot build a desktop head; actual build/rasterization/render proof stays deferred to the HITL
    platform-verification story (Spec Seam 2/4).
- **Current Conditions ViewModel mapper wiring + DI registration** (Story #56) — joins the two prior
  slices into displayable state. `CurrentConditionsViewModel` gains a `WeatherConditionMapper` ctor
  dependency (alongside F1's `IWeatherGateway` + `ILogger`) and four new display properties —
  `ChanceOfRainDisplay`, `WindSpeedDisplay`, `ConditionText`, and `IconSource` — so the panel now
  renders the full Current Conditions payload, not just temperature. On a successful fetch the VM maps
  `CurrentWeatherCode`/`IsDay` to the condition word and a day/night icon key (`{iconKey}.png`).
  - **Fail-visible fall-backs** (Technical-Context Overriding Principle 1) — the mapper's lenient
    fall-backs are logged, never silent: an unrecognized/absent `weather_code` and a null `is_day` each
    emit a `Warning`, so a degraded read is observable rather than swallowed.
  - **No stale/partial panel on failure** (security AC) — on `WeatherUnavailableException` every one of
    the five displays is cleared and only the fixed friendly copy is surfaced; no upstream or internal
    detail leaks, and no earlier reading lingers as if current.
  - **`WeatherConditionMapper` registered as a singleton** in `AddWeatherPoc2Core` (pure + stateless),
    so a real container with `validateScopes: true` resolves the ViewModel with the mapper injected;
    `MauiProgram` is unchanged. Covered by new VM and service-registration tests (Tier-1, $0); F1's
    existing tests were updated for the new ctor parameter.

- **Widened Current Conditions at the Gateway seam** (Story #55) — `OpenMeteoGateway` now requests the
  full Current Conditions payload (`current=temperature_2m,wind_speed_10m,weather_code,is_day`,
  `hourly=precipitation_probability`) and pins **both** canonical units explicitly on the wire
  (`temperature_unit=celsius&wind_speed_unit=kmh`, never relying on API defaults), and `WeatherBundle`
  is **extended, not reshaped** — it gains `CurrentWindSpeedKmh`, `CurrentChanceOfRainPercent`, and the
  nullable `CurrentWeatherCode`/`IsDay` icon hints alongside F1's `CurrentTemperatureCelsius`. The
  `IWeatherGateway` signature is unchanged, so F1's contract is preserved.
  - **Strict numeric measures fail closed** — wind speed plus a new `current_units.wind_speed_10m == "km/h"`
    assertion (belt-and-suspenders, mirroring F1's °C pin so the km/h guarantee is proven on the wire),
    and the current-hour Chance of Rain: `current.time` is truncated to the top of the hour, matched
    exactly against `hourly.time[]`, and the parallel `precipitation_probability[]` read at that index.
    An absent series, an unmatched hour, or a null probability throws `WeatherUnavailableException` after
    an Error log — `0` is a valid probability, never a fallback.
  - **Lenient icon hints flow through** — absent/null `weather_code` / `is_day` do not fail the fetch;
    they land as nullable bundle fields the Weather Condition Mapper resolves downstream (Unknown / day).
  - **Array-bounds safety on the untrusted parallel read** (security acceptance criterion) — the resolved
    current-hour index is guarded against `precipitation_probability[].Length`, so a degenerate Open-Meteo
    response whose `hourly.time[]` outruns its probability array fails closed with
    `WeatherUnavailableException` rather than an unhandled `IndexOutOfRangeException`.
  - Covered by widened Tier-1 recorded-replay fixtures and gateway tests (full-bundle mapping, the widened
    request string, minute-truncation hour match, and each strict failure path including the
    mismatched-array bounds guard); F1's existing gateway tests still pass. $0, every commit.

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

### Changed
- **Tier-2 live drift guard extended to the widened bundle** (Story #58) — F1's
  `Live_London_fetch_returns_a_celsius_bundle` is replaced by
  `Live_London_fetch_returns_a_full_current_conditions_bundle`, which makes the same single real
  Open-Meteo call for London but now asserts the **full** widened `WeatherBundle` comes back
  (temperature, wind speed, current-hour chance of rain), not just Celsius. This matters because the
  live guard exists to catch the recorded Tier-1 fixtures drifting from the live contract, and that
  contract widened at the Gateway seam (Story #55) — a °C-only assertion would no longer notice a
  server-side drift in the km/h units or the current-hour precipitation shape. No looser plausibility
  band is introduced: the widened Gateway throws `WeatherUnavailableException` unless both unit pins
  (°C, km/h) hold and the current hour resolves in `hourly.time[]`, so a returned full bundle *is* the
  unit-aware + current-hour assertion, with `InRange` sanity bands sitting atop that guarantee. Stays
  one trait-gated (`[Trait("Tier","2-Live")]`) call — excluded from the per-commit run
  (`dotnet test --filter "Tier!=2-Live"`), selected only by `--filter "Tier=2-Live"` — within F1's
  ≤ 5 live calls/day ceiling. No new fixture, no pipeline/schedule wiring.

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
