# Changelog

All notable changes to WeatherPOC2 are recorded here. The **why** matters as much as the **what**.

## [Unreleased] - 2026-07-26

### Added
- **DI registration of the Hourly Forecast coordinator graph** (Story #74) — `AddWeatherPoc2Core` now
  wires the ViewModels built over Stories #71–#73, closing the gap those stories deliberately left open.
  Until now the `WeatherViewModel` coordinator and both child view-models were built and Tier-1 tested
  but never registered, so the container could not resolve them.
  - **`HourlyWindow` joins `WeatherConditionMapper` as a pure stateless singleton** — both are I/O-free
    and hold no per-request state, so one shared instance is correct and cheapest.
  - **The coordinator and both display-only children register as transients** — `WeatherViewModel`,
    `CurrentConditionsViewModel`, and `HourlyForecastViewModel`. View-models are per-view scoped; a
    transient lifetime matches how the MAUI head will construct them and keeps no view state alive past
    its page. The container now resolves the `WeatherViewModel` coordinator with **both children
    non-null**, which is the acceptance bar for this story.
  - **Why now:** the coordinator graph was complete but unreachable through DI; this registration makes
    it resolvable ahead of the on-screen wiring (the Hourly Forecast View and the `CurrentConditionsPage`
    rewire) that a later story adds. No behaviour changed inside any view-model — this is wiring only.
- **`WeatherViewModel` screen coordinator owning the single fetch** (Story #73) — the parent
  view-model that ties the Hourly Forecast feature together (Approach A / Spec D2). It owns the one
  `GetWeather` call and distributes the single returned `WeatherBundle` to both child view-models, so
  Current Conditions and the Hourly Forecast are **mutually consistent by construction** — they can
  never show data from two different fetches.
  - **`LoadAsync` (`LoadCommand`)** — the sole fetch: sets `IsLoading`, awaits
    `IWeatherGateway.GetWeatherAsync(Location.LondonGb, ct)` (this Feature loads on page-appear only;
    the load/focus/manual refresh policy is Feature 9), then calls `CurrentConditions.Apply(bundle)`
    and `HourlyForecast.Apply(bundle)`. Async/await with a `CancellationToken` throughout (Principle #4);
    `IsLoading` is cleared in a `finally` on both the success and failure paths.
  - **Fail-visible single error** (Principle #1, Spec D3) — on `WeatherUnavailableException` it clears
    **both** children (`Clear()`) so no stale or partial panel/strip reads as current, and surfaces one
    fixed friendly message (`ErrorMessage`, the Technical-Context user-feedback copy). The Gateway has
    already logged the diagnostic detail, so the coordinator surfaces user-facing copy only.
  - **Not yet wired** — like the two child view-models it is deliberately **not** registered in
    `AddWeatherPoc2Core` and not bound to a View; the DI registration and the on-screen wiring (the
    Hourly Forecast View and the `CurrentConditionsPage` rewire off the dangling Story-#71 bindings)
    land in later stories. **Why build it now anyway:** the coordinator's fetch-and-distribute logic is
    pure Core behaviour testable in the Tier-1 suite ($0, no MAUI SDK) ahead of the desktop wiring the
    AFK runner cannot build.
  - Covered by `WeatherViewModelTests` (Tier-1, $0): one fetch populates both children, and the failure
    path clears both children and shows the friendly error with `IsLoading` cleared.
- **Hourly Forecast strip view-model (`HourlyForecastViewModel` + `HourlyForecastItem`)** (Story #72) —
  the display-only child that turns the fetched hourly series into on-screen strip cells, the presentation
  half of the Hourly Forecast whose pure `HourlyWindow` (Story #68) and Gateway series (Story #69) already
  landed. It follows the same passive-display shape as the Story #71 `CurrentConditionsViewModel`: the
  parent `WeatherViewModel` coordinator (a later story) pushes the shared `WeatherBundle` in via
  `Apply(bundle)`; the view-model never fetches.
  - **`Apply(bundle)`** runs the pure `HourlyWindow` over `bundle.Hourly`/`bundle.LocalNow`, maps each
    windowed hour's icon through the pure `WeatherConditionMapper`, and rebuilds an
    `ObservableCollection<HourlyForecastItem>` (variant A cell: `HH:00` time, `{iconKey}.png` icon,
    whole-degree temperature, chance %). The collection is rebuilt rather than mutated, so the cells stay
    immutable records with no per-item change notification. `Clear()` empties it on the coordinator's
    failure path so no stale strip lingers.
  - **Fail-visible gaps** (Technical-Context Overriding Principle #1) — a null hourly temperature or chance
    renders the "—" placeholder and logs a Warning; an unrecognized/absent `weather_code` or absent
    `is_day` also logs a Warning while the icon leniently falls back. The strip stays contiguous rather
    than dropping a gappy hour. The current hour's cell is flagged `IsNow` for the "Now" treatment.
  - **Not yet wired** — deliberately not registered in `AddWeatherPoc2Core` and not bound to a View; the
    `WeatherViewModel` coordinator that will construct/drive it and the on-screen strip View land in later
    stories. **Why build it now anyway:** landing the passive view-model separately keeps the strip's
    formatting + windowing logic in the Tier-1 Core suite (pure, $0, no MAUI SDK) ahead of the desktop
    wiring the AFK runner cannot build.
  - Covered by `HourlyForecastViewModelTests` (Tier-1, $0): per-hour formatting including the night-icon
    variant, the null-measure placeholder + Warning, entries replaced on each `Apply`, and `Clear`
    emptying the collection.

### Changed
- **`CurrentConditionsViewModel` demoted to display-only** (Story #71) — the Current Conditions
  view-model no longer fetches. It drops its `IWeatherGateway` dependency, the `LoadCommand`, and the
  `ErrorMessage`/`IsLoading` fetch-state, and exposes two synchronous methods instead: `Apply(bundle)`
  populates the five display properties (deriving the condition word + day/night icon via the pure
  `WeatherConditionMapper`, logging a Warning on each lenient fall-back so the derivation stays
  fail-visible per Principle #1), and `Clear()` blanks them so no stale panel reads as current.
  **Why:** the Hourly Forecast feature fetches weather once and shows it in two panels (Current
  Conditions + Hourly); making this view-model a passive display target lets a later `WeatherViewModel`
  coordinator own the single `GetWeather` call and push the shared `WeatherBundle` into both — the
  fetch-coupling the PRD requires — rather than each panel fetching independently. The coordinator, and
  the rewiring of `CurrentConditionsPage` (whose `LoadCommand`/`ErrorMessage`/`IsLoading` bindings are
  now dangling), land in a later story; the desktop head is not built on the AFK runner, so the Core
  Tier-1 suite stays green. The ViewModel's four behavioural tests moved from `LoadCommand.ExecuteAsync`
  to direct `Apply`/`Clear` calls (the NSubstitute gateway fake is no longer needed in that file).

### Added
- **Seam 2 proof completed — local-timestamp parse is culture- and timezone-invariant** (Story #70) —
  a single new Tier-1 recorded-replay test
  (`GetWeatherAsync_local_timestamp_parse_and_window_are_identical_across_cultures`) that closes out the
  Seam 2 contract established by Story #69. Story #69 already asserted the invariant parse; this proves
  it end-to-end for the PRD case a *Location whose local time differs from the device's*: the same
  captured London payload is parsed twice — once under `InvariantCulture`, once under `fr-FR` (whose
  default date formatting differs) — and `current.time`, every `hourly.time[]` element, **and** the
  resulting `HourlyWindow` slice are asserted byte-identical across both cultures, each timestamp
  `Kind=Unspecified`. The `Unspecified` kind is itself the device-timezone-independence proof — any
  `ToLocalTime`/`ToUniversalTime`/`AssumeLocal`/`AdjustToUniversal` in the parse would have produced a
  `Local`/`Utc` kind or a shifted value, both asserted against (ADR-0002). No production code changed;
  this is a test-only ratchet locking the invariance in. $0, every commit.
- **Widened the Open-Meteo seam for the hourly series (`timezone=auto`)** (Story #69) — the second
  Hourly Forecast slice: one `GetWeather` fetch now returns the full hourly series in the Location's
  local wall clock alongside Current Conditions, so the two views are consistent by construction (PRD
  "fetch coupling"). This lands the Gateway half that Story #68's pure `HourlyWindow` was waiting on —
  the window can now be computed from real fetched hours rather than test fixtures.
  - **`WeatherBundle` extended additively** — gains `Hourly` (`IReadOnlyList<HourlyForecastPoint>`,
    never null) and `LocalNow` (the Location's current wall-clock time). No existing field was removed
    or repurposed and `IWeatherGateway`'s signature is unchanged, so every prior caller and Feature
    contract is preserved; existing current-conditions tests moved to the widened 7-arg bundle with
    behaviour unchanged.
  - **`OpenMeteoGateway` requests `timezone=auto&forecast_days=2`** and the four hourly fields
    (`temperature_2m,weather_code,precipitation_probability,is_day`), keeping the current fields and the
    pinned canonical units. `timezone=auto` (ADR-0002) is what makes the returned timestamps the
    Location's own wall clock, so no in-app timezone database or DST arithmetic is needed downstream.
  - **Timestamps parse to `Kind=Unspecified` wall clock (Seam 2)** — `current.time` and each
    `hourly.time[]` are parsed with `InvariantCulture` + `DateTimeStyles.None`, deliberately applying
    **no** device timezone/locale shift, so the same payload yields identical local hours regardless of
    the machine the code runs on. `current.time` becomes the bundle's `LocalNow`.
  - **Fail closed on a malformed hourly series** — the five hourly arrays must all be present and
    equal-length to `time[]`, and the hourly units are pinned on the wire (°C / %); a missing or
    mismatched-length array throws `WeatherUnavailableException` (never an `IndexOutOfRangeException`)
    after an Error log. A `null` element in a value array soft-passes as a null field (never a fetch
    failure); the current-hour rain-chance logic is unchanged.
  - **Security — endpoint-only logging** — every `_logger` call logs `BaseUrl` (scheme+host+path) +
    `Location.Label`, never the coordinate-bearing url; the url is used only for the actual `GetAsync`.
    This keeps the Location's latitude/longitude out of the log sink. Requests are asserted over HTTPS.
  - Covered by widened Tier-1 recorded-replay tests: the widened request fields + HTTPS scheme, full
    series + `LocalNow` projection, fail-closed on short/missing hourly arrays and non-canonical hourly
    units, the null-element soft-passthrough, projection over a committed genuine live `timezone=auto`
    capture (`openmeteo-tzauto.json`), Seam 2 culture-and-device-timezone invariance, and
    no-geolocation-in-logs. $0, every commit.
- **Hourly Forecast — pure `HourlyWindow` + `HourlyForecastPoint`** (Story #68) — the first slice of
  the Hourly Forecast feature (ADO #45), landed as pure Core domain logic ahead of any Gateway or UI
  wiring so the perceptual-day window is nailed down and trivially testable in isolation.
  - **`HourlyForecastPoint`** — a record for one forecast hour in canonical units: `LocalTime` (a
    `DateTimeKind.Unspecified` wall-clock value per **ADR-0002**'s `timezone=auto` timestamps) plus the
    nullable `TemperatureCelsius` / `WeatherCode` / `IsDay` / `ChanceOfRainPercent` measures. Every
    *measure* is nullable because Open-Meteo may null an individual hourly value (a null flows to a "—"
    placeholder + a logged Warning downstream, Spec D3); `LocalTime` itself is never null — a
    mismatched/absent series is the Gateway's fail-closed path, not a null timestamp.
  - **`HourlyWindow.Compute(series, localNow)`** — a pure, I/O-free function returning the ordered slice
    from the current hour to the next upcoming 05:00 local, inclusive of the 05:00 hour and never
    including past hours. It is computed *purely* from the already-local timestamps ADR-0002 guarantees:
    `localNow` is a parameter (no device clock is read), and the window filters the actual returned local
    hours rather than assuming a fixed 24 — so a DST-transition day (a 23- or 25-hour day) is handled
    naturally. Keeping it a pure function of already-local values is exactly what ADR-0002 set up, so no
    in-app timezone database or DST arithmetic is needed.
  - Covered by `HourlyWindowTests` (Tier-1, $0): mid-afternoon reaching into tomorrow's 05:00, the
    pre-dawn short strip, the single-entry 05:00 hour (the settled `>=` boundary), the 06:00 reopen to a
    full day, the never-past-hours invariant, and a UK spring-forward DST day proving the module filters
    the real returned hours (the absent 01:00 is never fabricated).

## [Unreleased] - 2026-07-23

### Added
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
