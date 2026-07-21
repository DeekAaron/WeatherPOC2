# WeatherPOC2 — Project Brief

The original inbound product brief for WeatherPOC2, captured here so it lives in the repo
rather than in any one session. This is **source material** — the seed the PRD is built
from — not an authority. Where anything below diverges from the documented decisions,
`Context.MD` (the domain glossary) and, once written, `PRD.md` are authoritative; this file
is preserved as the starting point, not the current spec.

## The brief, as received

A .NET MAUI desktop weather app. On launch it shows today's weather for the most recently
searched Location; if none is loaded, it shows a search field. The default view is Current
Conditions (temperature, chance of rain, wind speed, weather icon), with an Hourly Forecast
below as a horizontally scrollable list (each entry: time, icon, temperature, chance of
rain). Users search Locations by name, keep a Search History of the last 4, and mark
Favourites. Units are user-selectable (default °C, km/h). All weather data comes from
Open-Meteo.

## Refinements since the brief

A `/grill-with-docs` session stress-tested the brief against the domain model and refined
several points. These refinements live in `Context.MD` (and `docs/adr/`) and take precedence
over the wording above:

- **Location** is a *resolved place* (coordinates + label + Open-Meteo id), not a typed
  string; a search returns **Search Candidates** to pick from, and Search History de-dupes by
  Location identity.
- **Launch** loads the most-recent Search History entry → else the top **Favourite** → else
  the search screen (the brief's "show a search field" is only the last of these).
- **Search** is reached at all times via a magnifying-glass icon that opens the search
  screen — not an inline field on the weather view.
- **Chance of Rain** is an hourly measure; Current Conditions shows the current hour's value
  (Open-Meteo has no current-moment probability).
- **Hourly Forecast** runs from the current hour to the next upcoming 05:00 in the Location's
  local time (a perceptual-day window, not a calendar day).
- **Units** are chosen independently per measure; weather is always fetched fresh and never
  persisted (see `docs/adr/0001-convert-units-locally.md`).
- **Favourites** are capped at 5 (block on overflow); **Search History** is capped at 4.
- Weather data comes solely from **Open-Meteo**; there is no device geolocation.
