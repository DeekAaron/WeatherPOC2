# WeatherPOC2

A cross-platform .NET MAUI desktop weather app built on the keyless [Open-Meteo](https://open-meteo.com)
API. It opens on the weather for the Location you last loaded, shows Current Conditions and an
Hourly Forecast, and lets you search Locations, keep a recency-ordered Search History and a set of
Favourites, and pick your display Units. Weather is always fetched fresh and never stored; when it
can't be fetched the app says so plainly rather than showing stale numbers. See `PRD.md` for the
full product requirements and `Roadmap.md` for the Feature breakdown.

## Status

Early build. Delivered so far is the **`WeatherPoc2.Core`** library — the Open-Meteo weather seam:
`OpenMeteoGateway` fetches the current temperature for a Location and converts **every** failure
(transport/timeout, oversized response, unparseable body, `error:true` body, non-200 status,
missing `temperature_2m`, or a non-°C unit) into the typed `WeatherUnavailableException`, always
after logging the endpoint and outcome — so a partial, fabricated, or wrong-unit reading never
reaches the app. The MAUI UI shell, ViewModels, and the remaining domain modules are not built yet.

## Build and test

Requires the .NET SDK pinned in `global.json` (`10.0.100`).

```sh
dotnet restore
dotnet build
dotnet test    # Tier-1 recorded-replay tests (xUnit)
```
