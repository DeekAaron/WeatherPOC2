# WeatherPOC2

A cross-platform .NET MAUI desktop weather app built on the keyless [Open-Meteo](https://open-meteo.com)
API. It opens on the weather for the Location you last loaded, shows Current Conditions and an
Hourly Forecast, and lets you search Locations, keep a recency-ordered Search History and a set of
Favourites, and pick your display Units. Weather is always fetched fresh and never stored; when it
can't be fetched the app says so plainly rather than showing stale numbers. See `PRD.md` for the
full product requirements and `Roadmap.md` for the Feature breakdown.

## Status

Early build. Delivered so far:

- **`WeatherPoc2.Core`** — the Open-Meteo weather seam: `OpenMeteoGateway` fetches the full Current
  Conditions bundle for a Location — Temperature and Wind Speed in canonical units (°C, km/h) and the
  current hour's Chance of Rain, plus the raw `weather_code`/`is_day` icon hints — and converts
  **every** failure (transport/timeout, oversized response, unparseable body, `error:true` body,
  non-200 status; a missing or non-°C temperature, a missing wind speed or non-km/h wind unit, or a
  current-hour Chance of Rain that is unmatched, null, or backed by a mismatched-length hourly array)
  into the typed `WeatherUnavailableException`, always after logging the endpoint and outcome — so a
  partial, fabricated, or wrong-unit reading never reaches the app. The icon hints are lenient: an
  absent `weather_code`/`is_day` flows through as `null` (resolved downstream by the mapper) rather
  than failing the fetch. The Gateway also carries the **geocoding half** of the seam —
  `SearchAsync(name)` resolves a typed name against Open-Meteo's geocoding endpoint into a list of
  `SearchCandidate`s (label, region/country, coordinates), returning an empty list when nothing matches
  (a plain "no matching places", not an error) and converting every failure into the typed
  `LocationSearchUnavailableException` after logging — so the app can tell "no such place" apart from
  "couldn't reach the service". Core also carries the
  display-only `CurrentConditionsViewModel` (CommunityToolkit.Mvvm): `Apply(bundle)` composes the
  bundle and the Weather Condition Mapper into the full displayable panel — temperature, chance of
  rain, wind speed, condition text, and a day/night icon — and `Clear()` blanks every field so no
  stale panel lingers. It no longer fetches: the `WeatherViewModel` coordinator (below) owns the single
  fetch and calls `Apply`/`Clear` (surfacing the one friendly error itself on failure). It now formats
  Temperature and Wind Speed in the user's chosen Units and **re-renders instantly when the Units change**
  — with no re-fetch and no possibility of failure (ADR-0001) — by retaining the fetched canonical bundle
  and re-formatting it on a units change (Chance of Rain stays a percentage). Core also
  carries the **`LocationSearchViewModel`** — search, no-match and error handling, and (as of Story #86)
  both selecting a candidate and tapping a **Recent** entry load through a single load coordinator and then
  navigate; the view-model exposes a `Recent` list mirroring the Search History and detaches its history
  subscription on dispose. It sits over three small MAUI-free seams: `ILoadedLocation` (an in-memory holder
  of the one currently loaded Location), `INavigator` (a navigation abstraction the app head implements over
  Shell), and the new **Search History** pair — `SearchHistory` (a pure in-memory state machine over the
  four most-recently-loaded Locations: de-dupe by identity, move-to-front, cap four) and `ILocationLoader`/
  `LocationLoader`, the single load choke point every load passes through (record to history -> set the
  loaded-Location holder -> persist, in that order), which also hydrates the history from the
  `search-history` document at startup. The OS-agnostic `AddWeatherPoc2Core` DI extension wires the whole
  graph up (named `HttpClient` with a 15 s timeout and 1 MB response cap, singleton gateway, the pure
  stateless singletons mapper and `HourlyWindow`, singletons `ILoadedLocation`, `SearchHistory`, and
  `ILocationLoader`, and the `WeatherViewModel` coordinator plus its two display-only children and the
  `LocationSearchViewModel` as transients).
- **`WeatherPoc2.App`** — the thin .NET MAUI app head: a `MauiProgram` DI host that calls
  `AddWeatherPoc2Core`, supplies the `INavigator` Shell implementation (`MauiNavigator`), and registers
  the pages + shell, and an `AppShell` that routes between a **Location Search** screen (the launch
  default — with nothing loaded the app opens on Search) and the Current Conditions page that fetches
  the currently loaded Location's conditions on appearing (fetch-on-load is the only refresh trigger for
  now), carries an always-available magnifying-glass toolbar action back to Search, and renders the
  Layout C panel plus the horizontal Hourly Forecast strip — a weather icon, condition text and
  temperature header above stacked chance-of-rain and wind-speed rows — or a friendly error, via
  MVVM bindings. (The page's original fetch-on-launch wiring binds `LoadCommand`/`ErrorMessage`/
  `IsLoading`, which the now display-only ViewModel no longer exposes as of Story #71; it is rewired to
  the shared-fetch `WeatherViewModel` coordinator in a later story. This desktop head is not built on
  the AFK runner.) The 15 weather-condition icons ship as self-authored SVGs under `Resources/Images/`
  (one per `WeatherIconKeys` member, registered as `MauiImage` and rasterized to `{key}.png` at
  build), so the mapper's icon key resolves to a bundled asset at runtime. Targets Mac Catalyst
  always, with the Windows head built only on a Windows host.
- **Weather Condition Mapper** — a pure, deterministic Core component (`WeatherConditionMapper`)
  that collapses Open-Meteo's numeric WMO weather codes (plus the `is_day` flag) onto the app's
  curated `WeatherCondition` set, each carrying a human display name and a day/night icon-asset key
  drawn from the fixed 15-key `WeatherIconKeys` set. It does no I/O and no logging; an unrecognized
  or absent code falls back to `Unknown` and is flagged `Recognized: false` for the caller to log.
- **Hourly Window** — the first slice of the Hourly Forecast, as pure Core logic: an
  `HourlyForecastPoint` record (one forecast hour in canonical units, with a local wall-clock time and
  nullable measures) and a pure `HourlyWindow.Compute(series, localNow)` that returns the hours from
  now to the next upcoming 05:00 local — inclusive of 05:00, never past hours. It reads no device clock
  and assumes no fixed 24 hours, so a daylight-saving transition day is handled by simply filtering the
  hours the forecast actually returned. The Gateway now emits that hourly series: one `GetWeather` fetch
  requests `timezone=auto` and returns the full local-wall-clock series (`WeatherBundle.Hourly` plus a
  `LocalNow` for the Location's current hour) alongside Current Conditions, so the two are consistent by
  construction. A display-only `HourlyForecastViewModel` now turns that series into the strip: `Apply(bundle)`
  runs the window, maps each hour's day/night icon, and builds one immutable cell per hour (time, icon,
  whole-degree temperature, chance of rain), flagging the current hour and rendering "—" for any absent
  measure; `Clear()` empties it. Each cell's temperature is formatted in the user's chosen Units, and the
  strip **rebuilds instantly when the Units change** (no re-fetch, ADR-0001) — only the temperature moves;
  time, icon, and chance are unchanged, and a null hour keeps its "—".
- **`WeatherViewModel` screen coordinator** — the parent view-model that owns the single `GetWeather`
  fetch and distributes the one returned bundle to both child view-models (`CurrentConditions.Apply` /
  `HourlyForecast.Apply`), so Current Conditions and the Hourly Forecast are consistent by construction.
  On a fetch failure it clears both children and surfaces one friendly error, with `IsLoading` tracking
  the in-flight fetch. As of Story #74 the coordinator and both children are DI-registered in
  `AddWeatherPoc2Core` (so the container resolves the coordinator with both children non-null); it also
  disposes both children on teardown, detaching their units-change subscriptions from the singleton units
  service (the children are transient, so an un-detached subscription would leak them). Still to come are
  the on-screen Hourly Forecast strip View and the Current Conditions page rewire onto this coordinator.
- **Units** — the first pure Core slices of the Units feature (`WeatherPoc2.Core.Units`): the
  `TemperatureUnit`/`WindSpeedUnit` enums (canonical member first — °C, km/h), a `UnitPreferences`
  record holding the per-measure choice with a canonical `Default` and value-equality, the pure
  `UnitConversion` (canonical → display unit, a number only — no rounding, no suffix, no I/O, and no
  failure path, so a unit change can never hit the network per ADR-0001), and the thin `UnitFormatter`
  that composes conversion with whole-number rounding and the unit suffix into the display string
  (`18°C`, `12 km/h`, rendered with `InvariantCulture`). These are now consumed: the two display
  ViewModels format through `UnitFormatter` + the shared `IUnitsService` and re-render on a units change
  (no re-fetch, ADR-0001), and `IUnitsService`/`UnitFormatter` are DI-registered in `AddWeatherPoc2Core`.
  The user's `UnitPreferences` persist across restart through the Persistence Store (below). What remains
  is the MAUI head supplying the app-data path provider and initialising the units service at startup, plus
  the on-screen Settings/Units screen View — deferred to the platform-verification story.
- **Persistence store** — the durable-state seam (`WeatherPoc2.Core.Persistence`, per ADR-0003):
  `IPersistenceStore` (`LoadAsync<T>(key)` / `SaveAsync<T>(key, value)`) backed by
  `JsonPersistenceStore`, one `System.Text.Json` document per key under an injected
  `IAppDataPathProvider` base directory (the MAUI head supplies `FileSystem.AppDataDirectory`; Core
  stays MAUI-free). Reads fail soft (absent → defaults with no log; corrupt/unreadable → defaults + a
  Warning, never crashing the view); writes are atomic and serialized per key, and a key that contains
  a separator, `..`, or an absolute path is rejected before any file access. The seam is now
  DI-registered in `AddWeatherPoc2Core` and consumed (the units service persists `UnitPreferences`
  through it); the only remaining gap is the MAUI-head `IAppDataPathProvider` implementation, which
  `AddWeatherPoc2Core` deliberately leaves host-supplied and arrives with the platform-verification story.

**Search History** is now built in Core (the state machine, the load coordinator, startup hydration, and
the search view-model's Recent list); only the MAUI-head startup-hydration call and on-screen Recent list
remain. The remaining domain modules (Favourites, launch resolver) are
not built yet. The desktop build/launch proof is owned by a
follow-on platform-verification story. The automated suite is Core Tier-1 recorded-replay plus a
single trait-gated Tier-2 live drift-guard test (`LiveOpenMeteoTests`) that runs only on the
scheduled path, never per-commit.

## Build and test

Requires the .NET SDK pinned in `global.json` (`10.0.100`).

```sh
dotnet restore
dotnet build
dotnet test --filter "Tier!=2-Live"   # per-commit: Tier-1 recorded-replay only, no network
dotnet test --filter "Tier=2-Live"    # scheduled (daily): live Open-Meteo drift guard
```

The Tier-2 test makes one real call to `api.open-meteo.com` for London through the real
`OpenMeteoGateway` to guard against the recorded fixtures drifting from the live API contract
(cost ceiling: ≤ 5 live calls per scheduled run, once per day). It is excluded from the
per-commit run so a plain `dotnet test` has no network dependency; the actual schedule wiring
lands with the Feature's CI setup.
