# Roadmap

**Product:** WeatherPOC2 — a cross-platform .NET MAUI desktop weather app built on Open-Meteo.

## Sequencing

Features are listed in delivery order. Each Feature gets its own `/brainstorming` session, Spec, and Plan.

---

## Feature 1: Current Temperature for a fixed Location 🔫 *tracer bullet*

**Status:** **Published to ADO** — Feature [#33](https://dev.azure.com/EnateInternal/DCWeatherApp2/_workitems/edit/33), 2026-07-21.

The app launches, fetches weather from Open-Meteo for a single hard-coded Location, and displays
the current **Temperature** in canonical °C. If the fetch fails, it shows a friendly in-app error
instead of a number. This Feature also stands up the whole substrate the rest of the build assumes:
the MAUI DI host, `IHttpClientFactory` registration, `ILogger<T>` wiring, the `global.json` SDK pin,
and the xUnit test project carrying the first **Tier-1 recorded-replay** test at the Open-Meteo
Gateway seam. It establishes the fetch-fresh / never-persist-weather and fail-visible patterns every
later Feature upholds.

**Out of scope:** Location Search, Chance of Rain, Wind Speed, the Weather Icon/Condition mapping,
the Hourly Forecast, units, history, favourites, launch fallback, refresh-on-focus/manual. The
Location is a constant in code — nothing user-selectable.

**Dependencies:** None (this is the tracer bullet).

**Why first:** It is the thinnest vertical slice that exercises every layer end-to-end (UI →
ViewModel → Gateway → HTTP/JSON → render) and produces something a user can see working, while
standing up the substrate and the Tier-1 test pattern that de-risk everything after it. The specific
desktop-OS platform matrix (and the Tier-2 cost ceiling) is pinned at this Feature's kickoff.

---

## Feature 2: Complete Current Conditions

Extend the tracer's single-Temperature view into the full **Current Conditions** panel for the
(still fixed) Location: **Chance of Rain** (the current hour's probability drawn from the hourly
data in the same fetch), **Wind Speed** (canonical km/h), and the **Weather Condition** with its
**Weather Icon** — day or night variant selected from Open-Meteo's `is_day` flag. This brings in the
pure **Weather Condition Mapper** (WMO code → curated condition; `is_day` → day/night icon variant).
Fetch failure surfaces a friendly error rather than a partial or stale panel. The load→fetch
coupling (re-fetch whenever a Location is loaded) is established in the Current Conditions ViewModel
here and exercised by every later load path.

**Out of scope:** Location Search (Location stays hard-coded), the Hourly Forecast strip itself,
unit conversion (values shown in canonical units), history, favourites, launch fallback,
refresh-on-focus/manual.

**Dependencies:** Feature 1 (the Gateway `GetWeather` call, the Current Conditions ViewModel/View,
and the substrate).

---

## Feature 3: Location Search

A magnifying-glass icon, available at all times, opens the **Location Search** screen. The user
types a name, the app calls Open-Meteo geocoding and returns **Search Candidates** (each with label,
region/country, coordinates); picking one mints a resolved **Location** and loads it into Current
Conditions. This retires the hard-coded Location from Features 1–2 — from here on every Location
originates from a search. Empty matches show a plain "No matching places found" message (not an
error; the screen stays put); a geocoding transport failure shows a friendly error, so the user can
tell "no such place" apart from "couldn't reach the service." When no Location is loaded, the search
screen fills the app; when one is on screen, the icon returns to the same search screen.

**Out of scope:** Search History and Favourites (a picked Location loads but nothing is remembered
yet — closing the app loses it), Hourly Forecast, units, launch fallback, refresh-on-focus/manual.

**Dependencies:** Feature 2 (loads the resolved Location into the existing Current Conditions view).
Extends the **Open-Meteo Gateway** with its geocoding responsibility (`Search(name)` → Candidates) —
the second half of the single external seam introduced in Feature 1.

---

## Feature 4: Hourly Forecast

Render the **Hourly Forecast** as a horizontally scrollable strip beneath Current Conditions, for
the loaded Location. Each entry shows its **Time** (in the Location's local wall clock), a **Weather
Icon**, a **Temperature**, and a **Chance of Rain**. The strip runs from the current hour to the
**next upcoming 05:00 in the Location's local time**, never including past hours — so it is short
pre-dawn and long in the afternoon. This brings in the pure **Hourly Window** module (given a clock
and the Location's timezone, deterministically computes the window). Forecast and Current Conditions
come from the *same* `GetWeather` fetch, so they are mutually consistent by construction.

**Out of scope:** units (Temperatures shown canonical), history, favourites, launch fallback,
refresh-on-focus/manual. Multi-day / calendar-day views remain permanently out of scope per the PRD.

**Dependencies:** Feature 3 (a resolved Location to forecast for) and Feature 2 (the shared
`GetWeather` bundle and the Weather Icon mapping, reused per entry).

---

## Feature 5: Units

Let the user choose display **Units** independently per measure — **Temperature** (°C/°F, default °C)
and **Wind Speed** (km/h, mph, m/s, knots, default km/h) — and re-render every temperature and wind
value (Current Conditions *and* every Hourly entry) instantly, with no network call and no
possibility of failure. Chance of Rain stays a percentage with no unit choice. This brings in the
pure **Unit Conversion** module (temperature formula; wind-speed factors) and, per **ADR-0001**,
converts the held canonical data for display only — never re-fetching. Chosen Units survive
restarts, which stands up the **Persistence Store** seam (scoped here to Units; History and
Favourites extend it later).

**Out of scope:** persisting history or favourites (the Persistence Store is introduced here but
only stores Units), launch fallback, refresh-on-focus/manual.

**Dependencies:** Feature 4 (both temperature displays — Current Conditions and Hourly — exist to
re-render) and Feature 2. Introduces the **Persistence Store**.

---

## Feature 6: Search History

Keep the **four most recently loaded Locations**, recency-ordered and keyed by Location identity
(Open-Meteo id / coordinates). "Loaded" is the trigger — picking a Search Candidate or tapping a
history entry — never merely typing. Loading a Location already present moves it to most-recent (no
duplicate); a genuinely new load at capacity evicts the oldest. History entries are tappable to
reload, and the list **survives restarts** (extending the Persistence Store from Feature 5). This
brings in the pure **Search History** state machine.

**Out of scope:** Favourites and the "open a Favourite" load path, the Launch Resolver /
launch-fallback behaviour (auto-opening on the most-recent entry is Feature 8),
refresh-on-focus/manual, and manual reordering or editing of history (permanently out of scope).

**Dependencies:** Feature 3 (loading a resolved Location from a search is the event that feeds
history) and Feature 5 (the Persistence Store it extends).

---

## Feature 7: Favourites

Let the user **mark/unmark a Location as a Favourite** — a flag on Location identity, not a separate
copy, so a Location can be a Favourite *and* sit in the Search History at once. Favourites are
ordered most-recently-marked first, capped at **five**; adding a sixth is refused with the exact
message "Favourites are full — remove one first" (block-on-overflow, never silent eviction).
Marking/unmarking leaves the Search History unchanged, and recency never evicts a Favourite. Opening
a Favourite loads its Location (which — via Feature 6 — moves it to most-recent in the Search
History). Favourites **survive restarts** (extending the Persistence Store). Brings in the pure
**Favourites** state machine.

**Out of scope:** the Launch Resolver / launch-fallback (auto-loading the top Favourite on an empty
history is Feature 8), refresh-on-focus/manual.

**Dependencies:** Feature 6 (opening a Favourite is a "load" that feeds the Search History, and
Favourite is a flag on the same Location identity History keys on) and Feature 5 (the Persistence
Store it extends).

---

## Feature 8: Launch behaviour (Launch Resolver)

On startup, resolve where the app opens via the three-way fallback: **most-recent Search History
entry → else top Favourite → else the Location Search screen**. When the top Favourite is
auto-loaded (empty history), that load moves it to most-recent in the Search History — so the *next*
launch resolves via history rather than re-deriving from Favourites. Brings in the pure **Launch
Resolver** decision function. This is what finally makes the app "open straight onto the weather
where I last looked."

**Out of scope:** refresh-on-focus and manual refresh (Feature 9). No new persisted state — it reads
the History and Favourites the Persistence Store already holds.

**Dependencies:** Feature 6 (Search History — the primary launch source and the reorder-on-load
consequence) and Feature 7 (Favourites — the fallback and the "top Favourite" ordering).

---

## Feature 9: Refresh & freshness

Complete the freshness policy the domain model requires: re-fetch Current Conditions and the Hourly
Forecast when the **app window regains focus** (a window left open never silently shows stale
numbers), and provide an explicit **manual refresh** action to force fresh data on demand.
Re-fetch-on-Location-load already exists from Feature 2 onward; this Feature adds the focus-regain
and manual triggers, with the refresh policy living in the ViewModels. Weather is never persisted
across restarts (only History, Favourites, and Units are).

**Out of scope:** any new data source or view — this is purely about *when* the existing
`GetWeather` fetch fires. Nothing new is persisted.

**Dependencies:** Feature 2 (the Current Conditions fetch/ViewModel it re-triggers) and Feature 4
(the Hourly Forecast that re-fetches with it). Benefits from Feature 8 being in place but does not
strictly require it.

---

## PRD coverage matrix

Every requirement in the PRD, mapped to the Feature that owns it. Requirements 53 and 54 are
cross-cutting non-functional properties — established in Feature 1 and upheld by every Feature
(friendly-language failures; identical behaviour across the desktop-OS platform matrix via the
Tier-1/Tier-2 testing standard) — so they are marked *cross-cutting* rather than owned by a single
Feature. All functional requirements have exactly one owning Feature: **0 UNOWNED**.

| PRD requirement | Owning Feature |
|---|---|
| Req 1 — open on most-recent Search History Location (Launch and app entry) | Feature 8 |
| Req 2 — open on top Favourite when history empty | Feature 8 |
| Req 3 — brand-new user opens on Location Search screen | Feature 8 |
| Req 4 — auto-loaded top Favourite becomes most-recent in history | Feature 8 |
| Req 5 — show current Temperature (Current Conditions) | Feature 1 |
| Req 6 — show current Chance of Rain (current hour's hourly probability) | Feature 2 |
| Req 7 — show current Wind Speed | Feature 2 |
| Req 8 — show Weather Icon + Weather Condition for the present moment | Feature 2 |
| Req 9 — Weather Icon day/night variant per `is_day` | Feature 2 |
| Req 10 — Current Conditions always fresh, never a stored earlier reading | Feature 1 |
| Req 11 — error rather than stale numbers when Current Conditions can't be fetched | Feature 1 |
| Req 12 — Hourly Forecast as a horizontally scrollable list (Hourly Forecast) | Feature 4 |
| Req 13 — each entry shows Time, Weather Icon, Temperature, Chance of Rain | Feature 4 |
| Req 14 — window runs current hour → next upcoming 05:00 (Location local time) | Feature 4 |
| Req 15 — never show hours already passed | Feature 4 |
| Req 16 — short pre-dawn, long in the afternoon (05:00 cutoff) | Feature 4 |
| Req 17 — each Time in the Location's local wall clock | Feature 4 |
| Req 18 — Hourly Forecast from the same fetch as Current Conditions | Feature 4 |
| Req 19 — magnifying-glass icon available at all times (Location Search) | Feature 3 |
| Req 20 — with no Location loaded, the search screen fills the app | Feature 3 |
| Req 21 — with a Location on screen, the icon opens the same search screen | Feature 3 |
| Req 22 — type a name, receive Search Candidates to choose from | Feature 3 |
| Req 23 — each Candidate carries label, region/country, coordinates | Feature 3 |
| Req 24 — picking a Candidate mints a resolved Location and loads it | Feature 3 |
| Req 25 — plain "No matching places found" message (not an error) on no match | Feature 3 |
| Req 26 — error when the search itself can't reach Open-Meteo | Feature 3 |
| Req 27 — keep the four most recently loaded Locations (Search History) | Feature 6 |
| Req 28 — "loaded" (not "typed") is the operative event | Feature 6 |
| Req 29 — loading an existing entry moves it to most-recent (no duplicate) | Feature 6 |
| Req 30 — oldest entry dropped when a new load exceeds capacity four | Feature 6 |
| Req 31 — tap a history entry to reload that Location | Feature 6 |
| Req 32 — Search History survives app restarts | Feature 6 |
| Req 33 — mark a Location as a Favourite (Favourites) | Feature 7 |
| Req 34 — Favourite is a flag on identity, not a separate copy | Feature 7 |
| Req 35 — marking/unmarking leaves the Search History unchanged | Feature 7 |
| Req 36 — recency never evicts a Favourite | Feature 7 |
| Req 37 — Favourites ordered most-recently-marked first | Feature 7 |
| Req 38 — block adding a sixth with "Favourites are full — remove one first" | Feature 7 |
| Req 39 — unmark a Favourite to remove it | Feature 7 |
| Req 40 — open a Favourite to load it (also moves it to most-recent in history) | Feature 7 |
| Req 41 — Favourites survive app restarts | Feature 7 |
| Req 42 — choose Temperature unit (°C/°F, default °C) (Units) | Feature 5 |
| Req 43 — choose Wind Speed unit (km/h, mph, m/s, knots, default km/h) | Feature 5 |
| Req 44 — choose units independently per measure | Feature 5 |
| Req 45 — Chance of Rain always a percentage, no unit choice | Feature 5 |
| Req 46 — changing a unit re-renders instantly, no loading, cannot fail | Feature 5 |
| Req 47 — a unit change affects display only, never the underlying weather | Feature 5 |
| Req 48 — chosen Units survive app restarts | Feature 5 |
| Req 49 — re-fetch whenever a Location is loaded (Refresh and freshness) | Feature 2 |
| Req 50 — re-fetch when the app window regains focus | Feature 9 |
| Req 51 — a manual refresh action | Feature 9 |
| Req 52 — weather data never persisted across restarts | Feature 1 |
| Req 53 — all failures surfaced in friendly, plain language (Cross-cutting) | *Cross-cutting* — established Feature 1, upheld by every Feature |
| Req 54 — identical behaviour on each supported desktop OS | *Cross-cutting* — established Feature 1, upheld by every Feature via the platform-matrix testing standard |

> **Note (surfaced at roadmap creation):** Req 54 requires OS parity but the PRD does not name the
> specific desktop OSes in the POC matrix ("the desktop OSes MAUI targets for this POC"). That OS
> list and the Tier-2 cost ceiling are pinned at Feature 1 kickoff, not deferred to gauntlet time.
