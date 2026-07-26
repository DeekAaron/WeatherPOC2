# CLAUDE.md — agent orientation

You are working in **WeatherPOC2**, a product built with the **Enate SDLC Factory**.
This file is the *agent* front door (auto-loaded every session); `README.md` is the human one.

## Read this first — and follow the flow

This product is built by walking the Factory's **HITL → AFK** flow. **Before you act, read
the field guide and follow the flow it describes:**

➡️ **[Using the Enate SDLC Factory](https://github.com/kitcox-dev/enate-claude-skills/blob/main/docs/using-the-sdlc-factory.md)**

The guide is the source of truth for *which skill to fire when*. The single rule it hinges on,
which you must never break: **only a human moves a Story to `Agent Ready`** — that is the HITL→AFK
handoff; the orchestrator owns every other transition.

## Where the Factory skills come from

The Factory skills install as the **`enate-sdlc-factory` plugin** from the `enate-skills`
marketplace declared in this repo's `.claude/settings.json` (source:
`kitcox-dev/enate-claude-skills`) — every session on this repo, desktop or cloud, loads them
automatically. Plugin-loaded skill names carry the `enate-sdlc-factory:` prefix (e.g.
`/enate-sdlc-factory:tdd`); guide references like `/tdd` mean that skill under whatever name
your available-skills list shows.

## The documentation fabric (load before you plan or build)

Authority order (lower wins): **ADR > Technical-Context > Context.MD > PRD > Roadmap > Spec > Plan.**

- **`Technical-Context.MD`** — the engineering contract every code-writing agent must respect
  (principles, secure-coding baseline, branching, and the **Testing & the ratchet** standard).
- **`Context.MD`** — the domain glossary (the project's language).
- **`docs/project-brief.md`** — the original inbound product brief (source material the PRD
  is built from; not an authority — Context.MD and PRD.md win where they diverge).
- **`PRD.md`** · **`Roadmap.md`** — product requirements; the ordered Feature list.
- **`docs/adr/`** — architectural decisions (highest authority).
- **`docs/superpowers/specs/`** · **`plans/`** — per-Feature Spec and Plan (the Plan carries
  the **Context references** an agent loads).

## Dev commands

Stack is **.NET 10 / C#** (SDK pinned via `global.json` at `10.0.100`). Solution: `WeatherPoc2.sln`.

- **Restore:** `dotnet restore`
- **Build:** `dotnet build`
- **Test (Tier 1, recorded-replay, every commit):** `dotnet test --filter "Tier!=2-Live"`
- **Test (Tier 2, live Open-Meteo drift guard, scheduled/daily):** `dotnet test --filter "Tier=2-Live"`
  — one real call to `api.open-meteo.com`; excluded from the per-commit run (no network there).

Built so far:

- `WeatherPoc2.Core` — the Open-Meteo seam (`OpenMeteoGateway`, `IWeatherGateway`,
  `WeatherUnavailableException`, `LocationSearchUnavailableException`, `Location`, `WeatherBundle`,
  `SearchCandidate`), the **display-only** `CurrentConditionsViewModel` (CommunityToolkit.Mvvm —
  `Apply(bundle)`/`Clear()`, no fetch of its own, per Story #71), the parent **`WeatherViewModel`**
  coordinator (Story #73, now integrated with Feature 3) that owns the single `GetWeather` call — for
  the **loaded Location** read from `ILoadedLocation.Current` rather than a hard-coded constant,
  no-opping when nothing is loaded (launch shows search first) — distributes the one bundle to both
  display children, and exposes an `OpenSearchCommand` routing to search via `INavigator`; the
  **`LocationSearchViewModel`** (search / no-match / error, and select-a-candidate -> set the shared
  holder -> navigate); and the `AddWeatherPoc2Core` DI extension (`ServiceCollectionExtensions` — named
  `HttpClient` with a 15 s timeout / 1 MB response cap, singleton `IWeatherGateway`, the pure stateless
  singletons `WeatherConditionMapper` and `HourlyWindow`, singleton `ILoadedLocation`->`LoadedLocation`,
  and the `WeatherViewModel` coordinator + both display-only children + `LocationSearchViewModel` as
  transients; `INavigator` is supplied by the MAUI head). Tested by the
  xUnit project `WeatherPoc2.Core.Tests`, which also carries `LiveOpenMeteoTests` — the trait-gated
  (`[Trait("Tier","2-Live")]`) Tier-2 live drift guard that makes one real Open-Meteo call for London
  asserting the full widened `WeatherBundle` deserializes (temperature, wind speed, current-hour chance
  of rain); because the widened Gateway already asserts both the °C and km/h unit pins and resolves the
  current hour in `hourly.time[]`, a returned full bundle is itself the unit-aware + current-hour
  assertion (the `InRange` checks are sanity bands atop that guarantee).
  `WeatherBundle` now carries the **full Current Conditions payload plus the hourly series** — Temperature
  and Wind Speed in canonical units (°C, km/h) and the current-hour Chance of Rain as strict fail-closed
  measures, the nullable `CurrentWeatherCode`/`IsDay` icon hints, and (Story #69, added additively — no
  field removed or repurposed) `Hourly` (`IReadOnlyList<HourlyForecastPoint>`, never null) and `LocalNow`
  (the Location's wall clock parsed from `current.time`, `Kind=Unspecified` per ADR-0002). The Gateway
  widens its request accordingly (`current=temperature_2m,wind_speed_10m,weather_code,is_day`,
  `hourly=temperature_2m,weather_code,precipitation_probability,is_day`, `timezone=auto&forecast_days=2`,
  both units pinned on the wire), asserts the km/h unit alongside the °C pin, matches the current-hour
precipitation by top-of-hour truncation, parses `current.time`/`hourly.time[]` invariantly to
  `Kind=Unspecified` wall-clock DateTimes (no device tz/locale shift), validates the five hourly arrays
  are present and equal-length and pins the hourly units (°C / %) on the wire — failing closed as
  `WeatherUnavailableException` (never `IndexOutOfRangeException`) on a mismatched-length or missing
  hourly array — and projects the `HourlyForecastPoint` list (a null element in a value array
  soft-passes as a null field). A **security control** keeps every `_logger` call to the endpoint only
  — `BaseUrl` (scheme+host+path) + `Location.Label`, never the coordinate-bearing url. The Gateway also
  carries the **geocoding half** of the seam (Story #64): `IWeatherGateway.SearchAsync(name, ct)` ->
  `IReadOnlyList<SearchCandidate>` against `geocoding-api.open-meteo.com/v1/search` (fixed
  `count=10&language=en&format=json`, the untrusted `name` percent-encoded), returning an empty list on
  a no-match 200 and converting every failure to the typed `LocationSearchUnavailableException`;
  `SearchCandidate` exposes a `Label` ("Name, Region, Country", collapsing to "Name, Country" when
  `admin1` is absent). Covered by `OpenMeteoGeocodingTests` / `SearchCandidateTests` and a Tier-2
  geocoding drift guard; the `IWeatherGateway` signature carries both `GetWeatherAsync` and `SearchAsync`.
  Core also carries the pure **Weather Condition Mapper** (`WeatherConditionMapper`,
  `WeatherConditionResult`, the `WeatherCondition` enum, and `WeatherIconKeys`) — a deterministic,
  I/O-free `Map(weatherCode, isDay)` that collapses Open-Meteo's numeric WMO codes onto the curated
  `WeatherCondition` set with a display name and a day/night icon-asset key from the fixed 15-key
  `WeatherIconKeys.All` set; freezing-precipitation codes (56/57/66/67) fold into Snow, and an
  unlisted or null code returns `Unknown` with `Recognized: false` (the caller logs the fallback).
Core also carries the first pure slices of the **Hourly Forecast** — `HourlyForecastPoint` (one
  forecast hour in canonical units, `LocalTime` as `Kind=Unspecified` per ADR-0002) and the pure,
  I/O-free **`HourlyWindow`** (`Compute(series, localNow)` returns the current-hour -> next-05:00-local
  slice, inclusive of 05:00, never past hours, DST-safe) — the widened Gateway emits the series
  (`WeatherBundle.Hourly` + `LocalNow`), and the display-only **`HourlyForecastViewModel`** consumes it
  (`Apply(bundle)` runs the window, maps each hour's icon, rebuilds an
  `ObservableCollection<HourlyForecastItem>` strip; null measures render "—" + a Warning; `Clear()`
  empties it). Core also carries the **Location Search** orchestration: **`LocationSearchViewModel`**
  (`Query`, `Candidates`, `StatusMessage`/`ErrorMessage`, a `SearchCommand` that no-ops on blank input
  and shows "No matching places found" on an empty result, and a `SelectCandidateCommand` that mints a
  `Location`, sets the shared holder, then navigates), plus the two MAUI-free seams it introduces —
  **`ILoadedLocation`**/`LoadedLocation` (in-memory holder of the one loaded Location, no persistence)
  and **`INavigator`** (`GoToCurrentConditionsAsync`/`GoToSearchAsync`, implemented by the app head over
  Shell). Covered by `HourlyWindowTests`, `HourlyForecastViewModelTests`, `LocationSearchViewModelTests`,
  `LoadedLocationTests` (Tier-1, $0).
- `WeatherPoc2.App` — the thin .NET MAUI app head: `MauiProgram` (the DI host — calls
  `AddWeatherPoc2Core` and registers `CurrentConditionsPage` + `AppShell`), `App`/`AppShell` shell
  routing to a single Current Conditions page, and `Views/CurrentConditionsPage` — the **Layout C
  panel**: an `Image` (`IconSource`) + condition (`ConditionText`) + temperature (`TemperatureDisplay`)
  header grid above stacked `ChanceOfRainDisplay` / `WindSpeedDisplay` rows, plus the `IsLoading`
  indicator and `ErrorMessage`, firing `LoadCommand` on `OnAppearing` (MVVM-only, no code-behind
  logic). ⚠️ As of Story #71 the ViewModel is display-only, so the page's `LoadCommand`/`ErrorMessage`/
  `IsLoading` bindings are now dangling — the page is rewired to the shared-fetch `WeatherViewModel`
  coordinator (which fetches once and calls `Apply`/`Clear`) in a later story; this desktop head is not
  built on the AFK runner (HITL platform-verification), so the transient break does not touch the Core
  suite. The 15 weather-condition icons are self-authored SVGs under `Resources/Images/` (one per
  `WeatherIconKeys` member) registered via a `MauiImage` glob; the resizetizer rasterizes each to a
  `{key}.png` the `Image.Source` binding resolves at runtime. `WeatherIconAssetsTests` (in
  `WeatherPoc2.Core.Tests`) is the per-commit Tier-1 guard — pure source-tree file I/O, no MAUI SDK —
  asserting every declared `WeatherIconKeys.All` key has a matching source SVG; build/rasterization/
  render stay deferred to the HITL platform-verification story. Targets `net10.0-maccatalyst` always;
  the Windows TFM is built only on a Windows host.

The desktop build/launch verification is deferred to a HITL platform-verification story (the AFK
runner cannot build either desktop head), so the automated suite is Core Tier-1 recorded-replay
(every commit) plus the single Tier-2 live drift guard (scheduled, never per-commit). No pipeline or
schedule wiring lives in the repo yet — the trait makes the split possible; the schedule lands with
the Feature's CI setup. Features 1–2 (Current Temperature, Current Conditions), Feature 4 (Hourly
Forecast) and Feature 3 (Location Search) are built end-to-end — including the MAUI app-head **Location
Search screen** + `MauiNavigator` `INavigator` implementation and the Current Conditions page (Layout C
panel + Hourly strip + the always-available magnifying-glass toolbar). The remaining domain modules from
`PRD.md` (Search History, Favourites, Units, persistence, launch resolver) are not built yet.
