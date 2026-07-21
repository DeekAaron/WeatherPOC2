# Domain Glossary

## Chance of Rain
The percentage likelihood of precipitation for a given hour. A probability, not a measured rainfall amount. It is an inherently *hourly* measure: it is shown against each Hourly Forecast entry, and the figure Current Conditions shows is the probability for the current hour drawn from that same hourly data — Current Conditions has no separate "right now" rain probability of its own.

## Current Conditions
The weather for the Location right now: its Temperature, Chance of Rain, and Wind Speed, alongside the Weather Condition and Weather Icon. This is the default view when a Location is loaded. Always fetched fresh from Open-Meteo and never persisted — it means the present moment, never a stored earlier reading; when it cannot be fetched (e.g. offline) the app shows an error rather than stale numbers. It (together with the Hourly Forecast, which comes from the same fetch) is re-fetched whenever a Location is loaded and whenever the app window regains focus, plus on an explicit manual refresh — so a window left open does not silently show stale numbers when the user returns to it. Distinct from the Hourly Forecast, which projects the same measures forward across the day rather than describing the present moment.

## Favourites
Locations the user has explicitly marked to keep. "Favourite" is a flag on a Location's identity, not a separate copy — so a Location can be a Favourite *and* sit in the Search History at the same time. Marking or unmarking a Favourite does not add to, remove from, or reorder the Search History, and being a Favourite does not protect a Location from being evicted from the history by more recent loads. Unlike the Search History, Favourites are user-curated and recency never evicts them. Favourites are ordered by when they were marked, most-recently-favourited first; the first in that order is the "top" Favourite, which the app auto-loads on launch when the Search History is empty. At most five Locations may be Favourites; with five already marked, attempting to add another is refused with a message ("Favourites are full — remove one first") rather than evicting an existing one — consistent with recency never evicting a Favourite. A Favourite leaves the list only when the user unmarks it.

## Hourly Forecast
The weather ahead for the current Location, broken down hour by hour, presented as a horizontally scrollable list beneath the Current Conditions. It is a forward-looking window: it runs from the current hour to the next upcoming 05:00 — so from mid-afternoon it reaches into the early hours of tomorrow, and pre-dawn it is a short strip to this morning's 5am. It never shows hours that have already passed. Both "the current hour" and the 05:00 cutoff are in the Location's local time (the Location's wall clock), not the device's. Each entry carries a Time, a Weather Icon, a Temperature, and a Chance of Rain. Distinct from Current Conditions, which describes only the present.

## Location
A *resolved* geographic place the app shows weather for — a fixed pair of coordinates and a display label (and Open-Meteo's stable place identifier), not the text the user typed. A Location is minted when the user picks one of the Search Candidates a Location Search returns; from then on nothing downstream holds a bare name — Current Conditions, the Hourly Forecast, the Search History and Favourites all hold resolved Locations. Two Locations are the same Location when they resolve to the same place (by Open-Meteo id / coordinates), regardless of the query that found them. Every view of Current Conditions and the Hourly Forecast is for exactly one Location at a time.

## Location Search
The action of finding a Location by name. The user types a name and the app returns a set of Search Candidates to choose from; picking one mints the Location and enters it into the Search History. When a name matches no places, the search stays put and shows a plain "No matching places found" message — not an error. Shown as the entry point when no Location is loaded.

## Search Candidate
One of the possible places a Location Search turns up for a typed name — e.g. "London" yields London (GB), London (Canada), London (Ohio). Each carries enough to tell it apart (label, region/country, coordinates). A Candidate is not yet a Location; it becomes one only when the user picks it.

## Open-Meteo
The external weather service that supplies all Current Conditions and Hourly Forecast data. Keyless and free; the single source of weather truth for the app. Not a domain concept the user sees — the origin of the data behind every other weather term here.

## Search History
The four most recently *loaded* Locations, ordered by recency and managed automatically. "Loaded" is the operative event, not "typed": any act that makes a Location the one on screen — picking a Search Candidate, tapping an entry in the history, or opening a Favourite — moves that Location to most-recent in the history. Keyed by Location identity, not by event: loading a place already in the history moves that entry to most-recent rather than adding a duplicate, so the four are always four *distinct* Locations. When a genuinely new Location is loaded and the history already holds four, the oldest is dropped. On launch the app loads the most recent of these; if the history is empty it falls back to the top Favourite, and only if there are no Favourites either does it show the Location Search as the entry point. (Because any load reorders the history, an auto-loaded top Favourite immediately becomes the most-recent history entry, so the following launch loads it as history rather than as a Favourite fallback.) Distinct from Favourites, which the user curates deliberately and which recency never evicts.

## Temperature
How warm or cold it is, shown for both Current Conditions and each Hourly Forecast entry. Displayed in the user's chosen temperature Unit (default Celsius).

## Time
The hour a given Hourly Forecast entry describes, expressed in the Location's local time (the Location's wall clock, not the device's). Distinguishes one forecast entry from the next; not part of Current Conditions, which is always "now".

## Units
The user's chosen measurement units for display, selected independently per measure rather than as a single metric/imperial system: a Temperature unit (Celsius or Fahrenheit; default Celsius) and a Wind Speed unit (km/h, mph, m/s or knots; default km/h). Chance of Rain is always a percentage and has no selectable unit. Units are a display preference only — changing them changes how a value is shown, never what the weather actually is.

## Weather Condition
The qualitative state of the weather at a moment — clear, cloudy, fog, rain, snow, thunderstorm, and the like — as opposed to the numeric measures (Temperature, Chance of Rain, Wind Speed). Derived by the app from Open-Meteo's numeric WMO weather code, collapsed into a small curated set of conditions rather than mirroring every code. Represented visually by a Weather Icon.

## Weather Icon
The graphic that represents a Weather Condition for a time period, shown in Current Conditions and against each Hourly Forecast entry. The visual form of a Weather Condition, not a distinct concept from it. The same Weather Condition may show a day or a night variant of its icon (e.g. sun vs moon for clear) depending on Open-Meteo's day/night (`is_day`) flag for that moment — the condition word is unchanged; only the icon varies.

## Wind Speed
How fast the wind is blowing, shown for Current Conditions and part of the weather measures. Displayed in the user's chosen Unit (default kilometres per hour).

## Relationships

- A **Location Search** returns zero or more **Search Candidates**; picking one mints exactly one **Location**.
- A **Location** is identified by its resolved place (Open-Meteo id / coordinates), not by the query that found it — two searches that resolve to the same place yield the same Location.
- **Current Conditions** and the **Hourly Forecast** are always for exactly one **Location** at a time, and come from a single Open-Meteo fetch.
- A **Location** has one **Weather Condition** at a given moment, shown as one **Weather Icon** (day or night variant per `is_day`).
- The **Search History** holds up to **4** distinct Locations (recency-ordered, auto-managed); **Favourites** holds up to **5** (user-curated, most-recently-marked first).
- A **Location** may sit in the Search History, in Favourites, in both, or in neither — the two are independent views over the same identity.
- **Launch** loads: most-recent Search History entry → else top **Favourite** → else the **Location Search**.
- **Persistence:** Search History, Favourites, and **Units** survive restarts; **weather data** (Current Conditions, Hourly Forecast) never does — it is always fetched fresh.

## Flagged ambiguities

- **"searched" vs "loaded"** — the Search History is keyed on *loading* a Location (picking a Candidate, tapping a history entry, opening a Favourite), not on typing a search. Resolved: it is a most-recently-loaded list, not a most-recently-typed list.
- **"chance of rain now"** — Open-Meteo has no current-moment rain probability. Resolved: Chance of Rain is an hourly measure; Current Conditions shows the current hour's value from the hourly data.
- **Location = a name vs a place** — resolved: a Location is a *resolved place* (coords + label + id), never a bare query string.
- **"the day's" hourly window** — resolved: forward-looking from the current hour to the next upcoming 05:00 (Location local time), never showing past hours; it is not a calendar day.

## Example dialogue

> **Dev:** If I search "London", pick London GB, then later search "London" again and pick London GB again — two entries in the **Search History**?
> **Domain expert:** No. Same **Location** — same place, same id. The second load just moves it back to most-recent. The four are always four distinct places.
>
> **Dev:** And if London GB is also a **Favourite**?
> **Domain expert:** Then it's in both lists at once. Favouriting doesn't pull it out of the history, and it doesn't shield it from being pushed out either — the history is pure recency.
>
> **Dev:** Fresh install, no history, but they've got Favourites. What loads?
> **Domain expert:** The top Favourite — the one they marked most recently. Only if there are no Favourites either do we show the search field.
