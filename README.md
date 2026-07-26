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
  than failing the fetch. Core also carries the
  display-only `CurrentConditionsViewModel` (CommunityToolkit.Mvvm): `Apply(bundle)` composes the
  bundle and the Weather Condition Mapper into the full displayable panel — temperature, chance of
  rain, wind speed, condition text, and a day/night icon — and `Clear()` blanks every field so no
  stale panel lingers. It no longer fetches: a later `WeatherViewModel` coordinator owns the single
  fetch and calls `Apply`/`Clear` (surfacing the one friendly error itself on failure). The OS-agnostic
  `AddWeatherPoc2Core` DI extension wires it all up (named `HttpClient` with a
  15 s timeout and 1 MB response cap, singleton gateway, singleton mapper, transient ViewModel).
- **`WeatherPoc2.App`** — the thin .NET MAUI app head: a `MauiProgram` DI host that calls
  `AddWeatherPoc2Core` and registers the page + shell, and an `AppShell` that routes to a single
  Current Conditions page that renders the Layout C panel — a weather icon, condition text and
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
  measure; `Clear()` empties it. It is a passive display target, so the shared-fetch coordinator and the
  on-screen strip View that binds to it come in a later slice.

The remaining domain modules (the rest of the Hourly Forecast, Location Search, Search History,
Favourites, Units, persistence, launch resolver) are not built yet. The desktop build/launch proof is owned by a
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
