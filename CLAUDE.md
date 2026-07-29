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

### Platform verification (HITL desktop head) — no Visual Studio needed

The MAUI **app head is never built by the AFK/CI runner** (it is Linux; the desktop TFMs
`net10.0-windows10.0.19041.0` and `net10.0-maccatalyst` build only on Windows / macOS). So each
HITL platform-verification Story is a human building and running the head once on the right OS. That
build needs **only the .NET 10 SDK plus the MAUI workload — NOT Visual Studio, and no VS licence.**
VS is an optional convenience (F5 + debugger); do **not** send a human hunting for a VS install or
licence for platform verification. The minimal, canonical setup (proven by the #38 and #88
platform-verification Stories) is:

- **One-time (Windows), CLI only:** confirm a `10.0.1xx` SDK with `dotnet --list-sdks`, then
  `dotnet workload install maui` (verify with `dotnet workload list`).
- **Build the Windows head:**
  `dotnet build src/WeatherPoc2.App/WeatherPoc2.App.csproj -f net10.0-windows10.0.19041.0`
  (the `-f` is required — the head multi-targets, and its Windows TFM is added only on a Windows host).
- **Run it (unpackaged .exe, no VS):** append `-t:Run` to the build command. The head is unpackaged
  (`WindowsPackageType=None`, no AppxManifest), so `-t:Run` is the launch path.
- **macOS head** is the same shape with `-f net10.0-maccatalyst` on a Mac.

Building the head is the human's job; **authoring/fixing any compile error is the machine's** — feed the
build output back rather than hand-editing the code on the verification box.

Built so far:

- `WeatherPoc2.Core` — the Open-Meteo seam (`OpenMeteoGateway`, `IWeatherGateway`,
  `WeatherUnavailableException`, `LocationSearchUnavailableException`, `Location`, `WeatherBundle`,
  `SearchCandidate`), the **display-only** `CurrentConditionsViewModel` (CommunityToolkit.Mvvm —
  `Apply(bundle)`/`Clear()`, no fetch of its own, per Story #71; as of Story #81 it retains the applied
  canonical bundle and formats Temperature/Wind Speed through `UnitFormatter` + `IUnitsService.Current`,
  re-formatting on `IUnitsService.Changed` — no re-fetch, cannot fail per ADR-0001 — and is `IDisposable`
  to detach that subscription; Chance of Rain stays a percentage), the parent **`WeatherViewModel`**
  coordinator (Story #73, now integrated with Feature 3) that owns the single `GetWeather` call — for
  the **loaded Location** read from `ILoadedLocation.Current` rather than a hard-coded constant,
  no-opping when nothing is loaded (launch shows search first) — distributes the one bundle to both
  display children, and exposes an `OpenSearchCommand` routing to search via `INavigator` (and, as of
  Story #81, is `IDisposable`, propagating `Dispose` to both transient children so they detach from the
  singleton `IUnitsService.Changed` on page teardown; the coordinator holds no subscription itself); the
  **`LocationSearchViewModel`** (search / no-match / error, and — as of Story #86 — both select-a-candidate
  and tap-a-Recent-entry load through the single `ILocationLoader` coordinator (record -> set holder ->
  persist) then navigate, exposing a `Recent` list that mirrors `SearchHistory`; as of Story #94 it also
  takes `IFavouritesService`, exposes a `Favourites` list mirroring it, and an `OpenFavouriteCommand` that
  loads through the same `ILocationLoader` choke point — behaviourally identical to tapping a Recent entry —
  and is `IDisposable` to detach both its `SearchHistory.Changed` and `IFavouritesService.Changed`
  subscriptions); and the `AddWeatherPoc2Core` DI extension
  (`ServiceCollectionExtensions` — named `HttpClient` with a 15 s timeout / 1 MB response cap, singleton
  `IWeatherGateway`, the pure stateless singletons `WeatherConditionMapper` and `HourlyWindow`, singleton
  `ILoadedLocation`->`LoadedLocation`, singleton `SearchHistory` + `ILocationLoader`->`LocationLoader` (the
  single load choke point), singleton `Favourites` + `IFavouritesService`->`FavouritesService` (Story #94,
  both singletons locked behind `ServiceRegistrationTests` as of Story #95 — `IFavouritesService` resolves
  twice to the same `FavouritesService`, and the pure `Favourites` machine is a shared singleton so the star
  and the list share one owner), and the `WeatherViewModel` coordinator + both display-only children +
  `LocationSearchViewModel` as transients; `INavigator` is supplied by the MAUI head). Tested by the
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
  empties it). As of Story #81 it retains the windowed canonical points and formats each entry's
  Temperature through `UnitFormatter` + `IUnitsService.Current`, **rebuilding** the strip on
  `IUnitsService.Changed` (cells are immutable records) — only Temperature is units-affected (Time, icon,
  Chance unchanged); a null hour temperature keeps the "—" placeholder — and is `IDisposable` to detach
  the subscription. Core also carries the **Location Search** orchestration: **`LocationSearchViewModel`**
  (`Query`, `Candidates`, `StatusMessage`/`ErrorMessage`, a `SearchCommand` that no-ops on blank input
  and shows "No matching places found" on an empty result, and — as of Story #86 — a `SelectCandidateCommand`
  that mints a `Location` and a `SelectRecentCommand` that takes an existing one, both loading through the
  single `ILocationLoader` coordinator (the VM no longer sets `ILoadedLocation` itself) then navigating,
  plus a `Recent` `ObservableCollection<Location>` mirroring `SearchHistory.Entries` most-recent-first and
  rebuilt on every `SearchHistory.Changed`; and — as of Story #94 — a third load path, an
  `OpenFavouriteCommand` that takes an existing Location and loads it through the same `ILocationLoader`
  coordinator then navigates (never touching `ILoadedLocation` and never calling the gateway; the opened
  Favourite becomes the most-recent Search History entry for free, PRD-40), plus a `Favourites`
  `ObservableCollection<Location>` mirroring `IFavouritesService.Entries` most-recently-marked-first and
  rebuilt on every `IFavouritesService.Changed` (empty when there are no Favourites, so the page renders no
  list section); the VM is `IDisposable` to detach **both** the `SearchHistory.Changed` and the
  `IFavouritesService.Changed` subscriptions — it is transient while both are singletons, so an un-detached
  handler would root every dead page, mirroring the Story #81 `CurrentConditionsViewModel` pattern), plus
  the MAUI-free seams it sits over —
  **`ILoadedLocation`**/`LoadedLocation` (in-memory holder of the one loaded Location, no persistence),
  **`INavigator`** (`GoToCurrentConditionsAsync`/`GoToSearchAsync`, implemented by the app head over
  Shell), and (Story #86) the **Search History** pair: **`SearchHistory`** — the pure in-memory state
  machine over the four most-recently-*loaded* Locations (dedupe-by-identity -> move-to-front -> cap 4,
  keyed by Open-Meteo id else coordinates, `Label` never part of identity — as of Story #96 its private
  `SameLocation` predicate delegates to the shared `LocationIdentity.Same`, so there is exactly one identity
  definition Search History and Favourites both key on, with byte-for-byte the same #47 semantics; `Record`
  for a load, `Seed` to hydrate/normalise a stored list, `Changed` raised on any mutation) — and **`ILocationLoader`**/`LocationLoader`
  — the single load choke point every load passes through (`LoadAsync`: record -> set holder -> persist,
  in that order; `HydrateAsync`: read the `search-history` document once at startup and `Seed` the machine),
  a singleton owning the Search History persistence read/write via `IPersistenceStore` so the pure state
  machine stays I/O-free and a save failure (fail-soft inside the store, ADR-0003) never blocks the load or
  navigation. Covered by `HourlyWindowTests`, `HourlyForecastViewModelTests`, `LocationSearchViewModelTests`,
  `LoadedLocationTests`, `SearchHistoryTests`, `LocationLoaderTests` (Tier-1, $0).
  Core also carries the first pure slices of **Units** (`WeatherPoc2.Core.Units`, Story #77) — the
  `TemperatureUnit`/`WindSpeedUnit` enums (canonical member first — °C, km/h), the `UnitPreferences`
  record (per-measure choice, value-equality, a canonical `Default` used on first run or any
  failed/absent read), the pure `UnitConversion` (canonical → display unit, number only — no rounding,
  no suffix, no I/O, total over the closed enums so a unit re-render can never fail or hit the network
  per ADR-0001), and the thin `UnitFormatter` (composes `UnitConversion` with whole-number
  away-from-zero rounding + the unit suffix into the display string — `18°C` unspaced, `12 km/h`
  spaced, digits via `InvariantCulture`). These are pure Core types landed ahead of wiring: none is
  DI-registered in `AddWeatherPoc2Core` or consumed by a ViewModel yet (the weather ViewModels still
  format inline; the rewire onto `UnitFormatter` and the persistence of `UnitPreferences` land in later
  stories). Covered by `UnitConversionTests`, `UnitFormatterTests`, `UnitPreferencesTests` (Tier-1, $0).
  Core also carries the **Persistence Store** seam (`WeatherPoc2.Core.Persistence`, Story #78, per
  ADR-0003) — the durable-state contract the PRD's module decomposition names, landed ahead of wiring.
  `IPersistenceStore` (`LoadAsync<T>(key)` / `SaveAsync<T>(key, value)`) is backed by
  `JsonPersistenceStore`: one `System.Text.Json` document per key (`{key}.json`) under an injected
  `IAppDataPathProvider` base directory (the MAUI head will return `FileSystem.AppDataDirectory`; Core
  stays MAUI-free and is Tier-1 testable against a temp dir), enums stored **by name** via
  `JsonStringEnumConverter` (stable across enum reordering). Read is **fail-soft + fail-visible**: an
  absent file returns `default` with no log (normal first run); a malformed / unreadable / unknown-enum
  / hostile deep-nesting document returns `default` + a Warning, never throwing to the caller (ADR-0001 /
  Principle 1). Writes are **atomic** (serialize to `{key}.json.tmp`, then `File.Replace` when the live
  file exists else `File.Move`) so an interrupted write never truncates the live file, guarded by a
  per-key `SemaphoreSlim` gate that serializes concurrent writers; a write failure is Warning-logged and
  the change kept in memory only, never thrown. `Directory.CreateDirectory` runs before writing (the
  app-data dir is not assumed to pre-exist). A `ValidateKey` **security guard** rejects an empty,
  separator-bearing, `..`-traversal, or rooted/absolute key (`ArgumentException`) before any file access,
  so a key can never escape the injected base directory (arbitrary read/overwrite). The `units` key is the
  first consumer (via `UnitsService`); the **`search-history` document seam is now proven end-to-end**
  (Story #84, test-only — no production code): a `List<Location>` round-trips through the store
  most-recent-first as a camelCase JSON array (`latitude`/`longitude`/`label`/`openMeteoId`) with a
  nullable `openMeteoId` round-tripping as `null`, an absent file loads as `null` with no log, a malformed
  document loads as `null` + a Warning, a parseable over-length/duplicate document is normalised by
  `SearchHistory.Seed`, and a save failure is caught + Warning-logged and never thrown to the caller — all
  against a real temp directory through the Feature-5 store. As of Story #86 the `search-history` document
  is wired end-to-end: `LocationLoader` persists `SearchHistory.Entries` under the `search-history` key on
  every load and `HydrateAsync` reads it back at startup, both DI-registered. As of Story #88 the MAUI-head
  wiring is complete and **human-verified on the Windows head**: `App` dispatches `HydrateAsync` on the UI
  thread at startup and the search page binds the `Recent` list — so Search History is now shipped end-to-end,
  nothing remains. The **`favourites` document seam is now proven end-to-end** the same way (Story #90,
  test-only — no persistence production code): a `List<Location>` round-trips through the same Feature-5
  store under the new `favourites` key as a camelCase JSON array (`latitude`/`longitude`/`label`/`openMeteoId`,
  order preserved most-recently-marked-first, nullable `openMeteoId` as `null`) with the same ADR-0003
  fail-soft recovery (absent → `null` no log; malformed → `null` + Warning; save failure caught +
  Warning-logged, never thrown) — proving the raw round-trip only; the Favourites domain invariant (dedupe +
  cap five) is `Favourites.Seed`'s job, which has now landed (Story #91, see below). Covered by
  `JsonPersistenceStoreTests` + `JsonPersistenceStoreSecurityTests`, `SearchHistoryPersistenceTests`,
  `SearchHistoryTests`, `LocationLoaderTests`, and `JsonPersistenceStoreFavouritesTests` (Tier-1, $0, real
  file I/O against a temp directory).
  Core also carries the pure **Favourites** state machine (`WeatherPoc2.Core.Weather`, Story #91) — the
  `Favourites` in-memory state machine over up to five user-curated Locations, most-recently-marked-first
  and distinct by the shared `LocationIdentity` predicate (`Label` never part of identity), and the
  `MarkResult` enum (`Marked` / `AlreadyFavourite` / `RefusedFull`). `Mark` inserts at the front, no-ops
  with a signal when already present (`AlreadyFavourite`), and **refuses at capacity five rather than
  evicting** (`RefusedFull` — recency never drops a pinned Favourite, Spec D3); `Unmark` removes the
  identity-equal entry (returns false, no change, when absent); `IsFavourite` tests membership; a `Changed`
  event fires only on a real mutation (never on a no-op). `Seed` **normalises rather than trusts** at the
  persistence trust boundary — total/never-throws for any input, dropping null elements, deduping by
  identity keeping the front-most occurrence, then capping to the first five — so a parseable-but-invalid
  `favourites` document can never violate the invariant. Pure and I/O-free: the friendly `RefusedFull` copy
  ("Favourites are full — remove one first") stays the ViewModel's job (deferred). Persistence is now owned
  by the **`IFavouritesService`**/`FavouritesService` coordinator (`WeatherPoc2.Core.Weather`, Story #94) —
  the singleton that wraps the pure machine and its persistence under the `favourites` key via
  `IPersistenceStore`: `Entries`/`IsFavourite` delegate to the machine, `Changed` forwards the machine's
  event synchronously (no marshalling — the UI-thread caller owns affinity, mirroring `UnitsService`),
  `HydrateAsync` reads the document once and `Seed`s (null on absent/malformed -> empty), and
  `MarkAsync`/`UnmarkAsync` persist **only on a real mutation** (a save failure is logged inside the store,
  never surfaced, ADR-0003 / Principle 1). The service's own logger emits nothing on the persistence path —
  no coordinate or `Label` reaches the sink (Story security AC). Covered by `FavouritesTests` and
  `FavouritesServiceTests` (Tier-1, $0).
- `WeatherPoc2.App` — the thin .NET MAUI app head: `MauiProgram` (the DI host — registers the
  host-supplied `IAppDataPathProvider`->`MauiAppDataPathProvider` **before** `AddWeatherPoc2Core` so the
  persistence graph — `JsonPersistenceStore` -> `LocationLoader` / `IUnitsService`, and hence
  `LocationSearchViewModel` — resolves at runtime, then calls `AddWeatherPoc2Core`, supplies the
  `INavigator`->`MauiNavigator` Shell implementation, and registers `CurrentConditionsPage` +
  `LocationSearchPage` + `AppShell` as transients), `App` (on construction dispatches
  `ILocationLoader.HydrateAsync()` onto the MAUI UI thread via `Dispatcher.DispatchAsync` — fire-and-forget
  Search-History startup hydration; the store read yields for I/O per Principle #4 and fails soft per
  ADR-0003, so it cannot block or throw), `AppShell` routing between the launch-default **Location Search**
  screen and the Current Conditions page. `Views/LocationSearchPage` is the search screen — a `SearchBar`
  (submit-only), the candidate list, and a **Recent** (Search History) list whose "Recent" header is hidden
  when history is empty via `CountToBoolConverter` (an `IValueConverter`, count>0 -> visible; bindings only,
  no code-behind, Principle #2), each Recent row tapping through `SelectRecentCommand`. `Views/CurrentConditionsPage`
  is the **Layout C panel**: an `Image` (`IconSource`) + condition (`ConditionText`) + temperature
  (`TemperatureDisplay`) header grid above stacked `ChanceOfRainDisplay` / `WindSpeedDisplay` rows, plus the
  Hourly Forecast strip and a friendly error, driven by the shared-fetch `WeatherViewModel` coordinator via
  MVVM bindings (no code-behind logic). The 15 weather-condition icons are self-authored SVGs under
  `Resources/Images/` (one per `WeatherIconKeys` member) registered via a `MauiImage` glob; the resizetizer
  rasterizes each to a `{key}.png` the `Image.Source` binding resolves at runtime. `WeatherIconAssetsTests`
  (in `WeatherPoc2.Core.Tests`) is the per-commit Tier-1 guard — pure source-tree file I/O, no MAUI SDK —
  asserting every declared `WeatherIconKeys.All` key has a matching source SVG. The head is **human-verified
  on the Windows head** for Feature 47 (Story #88 platform-verification): the Recent list, recency/cap-of-4,
  move-to-front-with-no-refetch, and persistence across a full relaunch (PRD-32) all confirmed on device.
  Targets `net10.0-maccatalyst` always; the Windows TFM (`net10.0-windows10.0.19041.0`, unpackaged) is built
  only on a Windows host.

The desktop build/launch verification is deferred to a HITL platform-verification story (the AFK
runner cannot build either desktop head), so the automated suite is Core Tier-1 recorded-replay
(every commit) plus the single Tier-2 live drift guard (scheduled, never per-commit). No pipeline or
schedule wiring lives in the repo yet — the trait makes the split possible; the schedule lands with
the Feature's CI setup. Features 1–2 (Current Temperature, Current Conditions), Feature 4 (Hourly
Forecast) and Feature 3 (Location Search) are built end-to-end — including the MAUI app-head **Location
Search screen** + `MauiNavigator` `INavigator` implementation and the Current Conditions page (Layout C
panel + Hourly strip + the always-available magnifying-glass toolbar). **Search History (Feature 47) is now
built and human-verified end-to-end**: the pure `SearchHistory` state machine, the `ILocationLoader`/
`LocationLoader` load-coordinator with startup `HydrateAsync`, and the `LocationSearchViewModel` `Recent`
list in Core (Story #86), plus the MAUI-head wiring — `MauiAppDataPathProvider`, the startup
`HydrateAsync` dispatch in `App`, and the on-screen Recent list on `LocationSearchPage` (Story #88,
platform-verified on the Windows head). **Favourites (Feature 48) is now under way in Core, not yet built
end-to-end**: **Seam 1 landed** (Story #90) — the `favourites` persistence document seam proven end-to-end
(test-only) and the shared `LocationIdentity` predicate (`WeatherPoc2.Core.Weather` — equal non-null
`OpenMeteoId` else exact lat/long equality, `Label` never part of identity, total/never-throws, also an
`IEqualityComparer<Location>` with a deliberately constant hash; the single predicate Spec D2 has both
Favourites and Search History key on — and as of Story #96 `SearchHistory` actually delegates to it, so the
"one identity definition" is now real rather than aspirational and the two machines cannot silently drift) —
the **pure `Favourites` state machine + `MarkResult` enum landed**
(Story #91): dedupe + block-on-overflow at five (recency never evicts), `Mark`/`Unmark`/`IsFavourite`/`Seed`
with a `Changed` event, `Seed` normalising at the persistence trust boundary; and now the **`IFavouritesService`/
`FavouritesService` persistence coordinator + the open-a-favourite path on `LocationSearchViewModel` have
landed** (Story #94, Core-only): the coordinator wraps the machine and persists under the `favourites` key
(persist-only-on-mutation, fail-soft), and the search VM exposes a `Favourites` bound list + an
`OpenFavouriteCommand` routing through the single `ILocationLoader` choke point. Still deferred: the
Favourites UI (the mark/unmark star + the friendly `RefusedFull` copy), the app-head wiring (binding the
`Favourites` list on `LocationSearchPage` and the startup `IFavouritesService.HydrateAsync` dispatch), and
the launch resolver. **Units** is wired into the display layer in Core: the two display ViewModels
format Temperature/Wind Speed through `UnitFormatter` + `IUnitsService` and re-render on a units change
(Story #81, ADR-0001 — no re-fetch, cannot fail), and `IUnitsService`/`UnitFormatter` are DI-registered in
`AddWeatherPoc2Core`; the user's `UnitPreferences` persist through the **Persistence Store**
(`IPersistenceStore` / `JsonPersistenceStore`, per ADR-0003), also DI-registered and consumed transitively
via `UnitsService`. `AddWeatherPoc2Core` deliberately leaves `IAppDataPathProvider` host-supplied — and as
of Story #88 `MauiProgram` **now supplies it** (`MauiAppDataPathProvider`), so the desktop graph resolves
`IUnitsService` at runtime. What still remains for Units is the MAUI head calling `IUnitsService.InitializeAsync`
at startup (Story #88 wired only the Search-History `HydrateAsync` call, not the units init) and the
Settings/Units screen View — both still deferred.
