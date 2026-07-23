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
  `SearchCandidate`), the `CurrentConditionsViewModel`
  (CommunityToolkit.Mvvm, fetch-on-load for `Location.LondonGb`, friendly fail-visible error), and
  the `AddWeatherPoc2Core` DI extension (`ServiceCollectionExtensions` — named `HttpClient` with a
  15 s timeout / 1 MB response cap, singleton `IWeatherGateway`, singleton `WeatherConditionMapper`,
  transient ViewModel). The ViewModel now composes the widened `WeatherBundle` and the
  `WeatherConditionMapper` (a third ctor dependency alongside `IWeatherGateway` + `ILogger`) into the
  full displayable panel — `TemperatureDisplay`, `ChanceOfRainDisplay`, `WindSpeedDisplay`,
  `ConditionText`, and `IconSource` (`{iconKey}.png`) — mapping `CurrentWeatherCode`/`IsDay` on
  success and logging a Warning on each lenient fall-back (unrecognized/absent code, null `is_day`);
  on `WeatherUnavailableException` all five displays are cleared (no stale/partial panel) and only the
  fixed friendly copy is surfaced. Tested by the
  xUnit project `WeatherPoc2.Core.Tests`, which also carries `LiveOpenMeteoTests` — the trait-gated
  (`[Trait("Tier","2-Live")]`) Tier-2 live drift guard that makes one real Open-Meteo call for London
  asserting the full widened `WeatherBundle` deserializes (temperature, wind speed, current-hour chance
  of rain); because the widened Gateway already asserts both the °C and km/h unit pins and resolves the
  current hour in `hourly.time[]`, a returned full bundle is itself the unit-aware + current-hour
  assertion (the `InRange` checks are sanity bands atop that guarantee).
  `WeatherBundle` now carries the **full Current Conditions payload** — Temperature and Wind Speed in
  canonical units (°C, km/h) and the current-hour Chance of Rain as strict fail-closed measures, plus
  the nullable `CurrentWeatherCode`/`IsDay` icon hints — and the Gateway widens its request
  accordingly (`current=temperature_2m,wind_speed_10m,weather_code,is_day&hourly=precipitation_probability`,
  both units pinned on the wire), asserts the km/h unit alongside the °C pin, matches the current-hour
  precipitation by top-of-hour truncation, and fails closed (never `IndexOutOfRangeException`) on a
  mismatched-length hourly array.
  The Gateway now also carries the **geocoding half** of the seam (Story #64): `IWeatherGateway`
  gains `SearchAsync(name, ct)` → `IReadOnlyList<SearchCandidate>`, implemented against
  `geocoding-api.open-meteo.com/v1/search` with a fixed `count=10&language=en&format=json` and the
  untrusted `name` percent-encoded (`Uri.EscapeDataString`) inside the `name` value only (no
  query-parameter injection). `SearchCandidate` (`Id`, `Name`, `Region`, `Country`, `Latitude`,
  `Longitude`) exposes a `Label` — "Name, Region, Country", collapsing to "Name, Country" when the
  Open-Meteo `admin1` (Region) is absent. A no-match 200 (the `results` key absent) returns an empty
  list — never an exception — so callers tell "no matching places" from a real failure; every failure
  mode (transport/timeout, unparseable body, non-2xx) is logged at Error with endpoint + outcome and
  converted to the typed `LocationSearchUnavailableException` (distinct from
  `WeatherUnavailableException`). Covered by `OpenMeteoGeocodingTests` / `SearchCandidateTests`
  (Tier-1 recorded-replay, three geocoding fixtures) and a `LiveOpenMeteoTests` Tier-2 geocoding
  drift guard (`Live_London_geocoding_returns_candidates`).
  Core also carries the pure **Weather Condition Mapper** (`WeatherConditionMapper`,
  `WeatherConditionResult`, the `WeatherCondition` enum, and `WeatherIconKeys`) — a deterministic,
  I/O-free `Map(weatherCode, isDay)` that collapses Open-Meteo's numeric WMO codes onto the curated
  `WeatherCondition` set with a display name and a day/night icon-asset key from the fixed 15-key
  `WeatherIconKeys.All` set; freezing-precipitation codes (56/57/66/67) fold into Snow, and an
  unlisted or null code returns `Unknown` with `Recognized: false` (the caller logs the fallback).
- `WeatherPoc2.App` — the thin .NET MAUI app head: `MauiProgram` (the DI host — calls
  `AddWeatherPoc2Core` and registers `CurrentConditionsPage` + `AppShell`), `App`/`AppShell` shell
  routing to a single Current Conditions page, and `Views/CurrentConditionsPage` — the **Layout C
  panel**: an `Image` (`IconSource`) + condition (`ConditionText`) + temperature (`TemperatureDisplay`)
  header grid above stacked `ChanceOfRainDisplay` / `WindSpeedDisplay` rows, plus the `IsLoading`
  indicator and `ErrorMessage`, firing `LoadCommand` on `OnAppearing` (MVVM-only, no code-behind
  logic). The 15 weather-condition icons are self-authored SVGs under `Resources/Images/` (one per
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
the Feature's CI setup. The remaining domain modules from `PRD.md` (Hourly Forecast, Location Search —
its Gateway geocoding seam is built, but the search screen and its ViewModel are not — Search History,
Favourites, Units, persistence, launch resolver) are not built yet.
