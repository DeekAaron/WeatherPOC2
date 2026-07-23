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
  `CurrentConditionsViewModel` (CommunityToolkit.Mvvm), which composes the bundle and the Weather
  Condition Mapper into the full displayable panel — temperature, chance of rain, wind speed,
  condition text, and a day/night icon — or, on failure, clears every field and shows one friendly
  error. The OS-agnostic `AddWeatherPoc2Core` DI extension wires it all up (named `HttpClient` with a
  15 s timeout and 1 MB response cap, singleton gateway, singleton mapper, transient ViewModel).
- **`WeatherPoc2.App`** — the thin .NET MAUI app head: a `MauiProgram` DI host that calls
  `AddWeatherPoc2Core` and registers the page + shell, and an `AppShell` that routes to a single
  Current Conditions page which fetches London's conditions on launch (fetch-on-load is the only
  refresh trigger for now) and renders the Layout C panel — a weather icon, condition text and
  temperature header above stacked chance-of-rain and wind-speed rows — or a friendly error, via
  MVVM bindings. The 15 weather-condition icons ship as self-authored SVGs under `Resources/Images/`
  (one per `WeatherIconKeys` member, registered as `MauiImage` and rasterized to `{key}.png` at
  build), so the mapper's icon key resolves to a bundled asset at runtime. Targets Mac Catalyst
  always, with the Windows head built only on a Windows host.
- **Weather Condition Mapper** — a pure, deterministic Core component (`WeatherConditionMapper`)
  that collapses Open-Meteo's numeric WMO weather codes (plus the `is_day` flag) onto the app's
  curated `WeatherCondition` set, each carrying a human display name and a day/night icon-asset key
  drawn from the fixed 15-key `WeatherIconKeys` set. It does no I/O and no logging; an unrecognized
  or absent code falls back to `Unknown` and is flagged `Recognized: false` for the caller to log.

The remaining domain modules (Hourly Forecast, Location Search — its Gateway geocoding seam is built,
but the search screen and its ViewModel are not — Search History, Favourites, Units, persistence,
launch resolver) are not built yet. The desktop build/launch proof is owned by a
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
