# WeatherPOC2

A cross-platform .NET MAUI desktop weather app built on the keyless [Open-Meteo](https://open-meteo.com)
API. It opens on the weather for the Location you last loaded, shows Current Conditions and an
Hourly Forecast, and lets you search Locations, keep a recency-ordered Search History and a set of
Favourites, and pick your display Units. Weather is always fetched fresh and never stored; when it
can't be fetched the app says so plainly rather than showing stale numbers. See `PRD.md` for the
full product requirements and `Roadmap.md` for the Feature breakdown.

## Status

Early build. Delivered so far:

- **`WeatherPoc2.Core`** — the Open-Meteo weather seam: `OpenMeteoGateway` fetches the current
  temperature for a Location and converts **every** failure (transport/timeout, oversized response,
  unparseable body, `error:true` body, non-200 status, missing `temperature_2m`, or a non-°C unit)
  into the typed `WeatherUnavailableException`, always after logging the endpoint and outcome — so a
  partial, fabricated, or wrong-unit reading never reaches the app. Core also carries the
  `CurrentConditionsViewModel` (CommunityToolkit.Mvvm) and the OS-agnostic `AddWeatherPoc2Core` DI
  extension (named `HttpClient` with a 15 s timeout and 1 MB response cap, singleton gateway,
  transient ViewModel).
- **`WeatherPoc2.App`** — the thin .NET MAUI app head: a `MauiProgram` DI host that calls
  `AddWeatherPoc2Core` and registers the page + shell, and an `AppShell` that routes to a single
  Current Conditions page which fetches London's temperature on launch (fetch-on-load is the only
  refresh trigger for now) and renders it, or a friendly error, via MVVM bindings. Targets Mac
  Catalyst always, with the Windows head built only on a Windows host.

The remaining domain modules (Hourly Forecast, Location Search, Search History, Favourites, Units,
persistence, launch resolver) are not built yet. The desktop build/launch proof is owned by a
follow-on platform-verification story; the automated suite is Core Tier-1 only.

## Build and test

Requires the .NET SDK pinned in `global.json` (`10.0.100`).

```sh
dotnet restore
dotnet build
dotnet test    # Tier-1 recorded-replay tests (xUnit)
```
