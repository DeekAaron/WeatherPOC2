# WeatherPOC2 — Product Requirements Document

> Product requirements for a .NET MAUI desktop weather app built on Open-Meteo. These are
> product-level **Requirements** that feed the Roadmap's Feature breakdown — not the
> orchestrator's atomic **User Story** work items (those come later, via `/enate-to-stories`).
> Vocabulary follows `Context.MD`; where this PRD and the domain glossary diverge, the glossary
> and any ADR win (authority order: ADR > Technical-Context > Context.MD > PRD).

## Problem Statement

I want to check the weather for a place I care about — right now and across the rest of the day —
without fuss. When I open the app it should already be showing me the weather where I last looked,
not make me search again every time. I want to see the essentials at a glance (how warm it is,
whether it'll rain, how windy it is, and a clear picture of the sky), and I want to scan the hours
ahead so I can decide whether to take a coat or move my plans. I search for places by name, but the
same name can mean several different towns, so I need to pick the right one. I keep coming back to a
handful of places, so I don't want to re-type them — the app should remember where I've been recently
and let me pin the ones I care about. And I want temperatures and wind shown in the units I think in,
not whatever the data source happens to use.

## Solution

A cross-platform .NET MAUI desktop app that opens straight onto the weather for the Location I last
loaded. The default view is **Current Conditions** — Temperature, Chance of Rain, Wind Speed, and a
Weather Icon for the current Weather Condition — with an **Hourly Forecast** beneath it as a
horizontally scrollable strip running from the current hour to the next upcoming 05:00 in the
Location's local time. A magnifying-glass icon, available at all times, opens a **Location Search**
screen where I type a name, choose from the **Search Candidates** returned, and load the resolved
**Location**. The app keeps a recency-ordered **Search History** of the last four Locations I loaded
and a user-curated set of up to five **Favourites**. I choose my display **Units** per measure
(Temperature in °C/°F, Wind Speed in km/h, mph, m/s, or knots); Chance of Rain is always a
percentage. All weather comes fresh from **Open-Meteo** and is never stored; my Search History,
Favourites, and Units survive restarts. When the weather can't be fetched, the app tells me plainly
rather than showing stale numbers.

## Requirements

### Launch and app entry

1. As a returning user, I want the app to open showing Current Conditions for the most recent Location in my Search History, so that I don't have to search again for the place I care about most.
2. As a user with no Search History but at least one Favourite, I want the app to open on my top Favourite (the one I most recently marked), so that a fresh install or cleared history still lands me somewhere useful.
3. As a brand-new user with no Search History and no Favourites, I want the app to open on the Location Search screen, so that I have an obvious starting point.
4. As a user whose top Favourite is auto-loaded on launch, I want that load to move the Favourite to most-recent in my Search History, so that the next launch reloads it as history rather than re-deriving it from Favourites.

### Current Conditions

5. As a user, I want to see the current Temperature for the loaded Location, so that I know how warm or cold it is right now.
6. As a user, I want to see the current Chance of Rain (the current hour's probability drawn from the hourly data), so that I know how likely rain is now.
7. As a user, I want to see the current Wind Speed, so that I know how windy it is.
8. As a user, I want to see a Weather Icon and its Weather Condition for the present moment, so that I get an at-a-glance read of the sky.
9. As a user, I want the Weather Icon to show a day or night variant according to the Location's `is_day` flag, so that a clear night shows a moon rather than a sun.
10. As a user, I want Current Conditions to always reflect the present moment fetched fresh, never a stored earlier reading, so that I can trust the numbers I see.
11. As a user, I want an error message rather than stale numbers when Current Conditions can't be fetched (e.g. offline), so that I'm never misled by out-of-date weather.

### Hourly Forecast

12. As a user, I want an Hourly Forecast beneath Current Conditions as a horizontally scrollable list, so that I can scan the hours ahead.
13. As a user, I want each Hourly Forecast entry to show its Time, a Weather Icon, a Temperature, and a Chance of Rain, so that each hour is self-contained.
14. As a user, I want the forecast to run from the current hour to the next upcoming 05:00 in the Location's local time, so that I see the rest of the perceptual day including its overnight hours.
15. As a user, I want the forecast never to show hours that have already passed, so that the strip is always forward-looking.
16. As a user, I want the strip to be short pre-dawn and long in the afternoon (following the 05:00 cutoff), so that "today" always means the hours I still have ahead of me.
17. As a user, I want each entry's Time expressed in the Location's local wall clock rather than my device's, so that the hours read correctly for the place I'm looking at.
18. As a user, I want the Hourly Forecast to come from the same fetch as Current Conditions, so that the two views are always mutually consistent.

### Location Search

19. As a user, I want a magnifying-glass icon available at all times that opens the Location Search screen, so that I can search or switch Locations whenever I want.
20. As a user with no Location loaded, I want the Location Search screen to fill the app, so that I have a clear place to begin.
21. As a user with a Location on screen, I want the search icon to take me to the same search screen, so that switching Locations is one consistent action.
22. As a user, I want to type a place name and receive a set of Search Candidates to choose from, so that I can disambiguate between places that share a name (e.g. London GB vs London Ohio).
23. As a user, I want each Search Candidate to carry enough detail to tell it apart (label, region/country, coordinates), so that I can pick the right place confidently.
24. As a user, I want picking a Search Candidate to mint a resolved Location and load it, so that everything downstream holds a real place, not the text I typed.
25. As a user, I want a plain "No matching places found" message (not an error) when my search matches nothing, with the search screen staying put, so that I can simply try another name.
26. As a user, I want an error message when the search itself can't reach Open-Meteo, so that I can tell "no such place" apart from "couldn't reach the service."

### Search History

27. As a user, I want the app to keep my four most recently loaded Locations, so that I can quickly return to places I've been looking at.
28. As a user, I want "loaded" to be the operative event (picking a Candidate, tapping a history entry, or opening a Favourite), not merely typing a search, so that the history reflects where I actually looked.
29. As a user, I want loading a Location already in the history to move it to most-recent rather than duplicating it, so that the four are always four distinct places.
30. As a user, I want the oldest entry dropped when I load a genuinely new Location and the history already holds four, so that the list stays capped at four by recency.
31. As a user, I want to tap an entry in my Search History to reload that Location, so that returning to a recent place takes one action.
32. As a user, I want my Search History to survive app restarts, so that the app remembers where I've been.

### Favourites

33. As a user, I want to mark a Location as a Favourite, so that I can keep places I care about regardless of recency.
34. As a user, I want Favourite to be a flag on a Location's identity rather than a separate copy, so that a Location can be a Favourite and sit in the Search History at the same time.
35. As a user, I want marking or unmarking a Favourite to leave my Search History unchanged (no add, remove, or reorder), so that the two lists stay independent.
36. As a user, I want recency never to evict a Favourite, so that a place I pinned stays pinned until I unmark it.
37. As a user, I want my Favourites ordered most-recently-marked first, so that my newest pick is the "top" Favourite used for launch fallback.
38. As a user, I want to be blocked with a clear message ("Favourites are full — remove one first") when I try to add a sixth Favourite, so that I consciously choose what to keep rather than silently evicting something.
39. As a user, I want to unmark a Favourite to remove it from the list, so that I control exactly what stays pinned.
40. As a user, I want to open a Favourite to load its Location (which also moves it to most-recent in the Search History), so that opening a pinned place behaves like any other load.
41. As a user, I want my Favourites to survive app restarts, so that my curated places persist.

### Units

42. As a user, I want to choose my Temperature unit (Celsius or Fahrenheit, default Celsius), so that temperatures read the way I think.
43. As a user, I want to choose my Wind Speed unit (km/h, mph, m/s, or knots, default km/h), so that wind reads the way I think.
44. As a user, I want to choose units independently per measure rather than as one metric/imperial system, so that I can mix (e.g. °C with mph) if I prefer.
45. As a user, I want Chance of Rain always shown as a percentage with no unit choice, so that it stays unambiguous.
46. As a user, I want changing a unit to re-render instantly with no loading and no possibility of failure, so that switching units never depends on the network.
47. As a user, I want a unit change to affect only how a value is displayed, never the underlying weather, so that units are purely a display preference.
48. As a user, I want my chosen Units to survive app restarts, so that I set them once.

### Refresh and freshness

49. As a user, I want Current Conditions and the Hourly Forecast re-fetched whenever a Location is loaded, so that I always start from live data.
50. As a user, I want them re-fetched when the app window regains focus, so that a window left open doesn't silently show stale numbers when I come back to it.
51. As a user, I want a manual refresh action, so that I can force fresh data on demand.
52. As a user, I want weather data never persisted across restarts, so that the app never shows me a stored earlier reading as if it were now.

### Cross-cutting

53. As a user, I want all failures surfaced in friendly, plain language (no stack traces, HTTP codes, or exception names), so that I understand what happened and what to do next.
54. As a user, I want the app to behave identically on each desktop OS it supports, so that my experience doesn't depend on the platform.

## Implementation Decisions

The build decomposes into the following deep modules (simple, testable interfaces hiding their
internals). Boundaries and test scope were confirmed with the developer in this session. No file
paths or code are pinned here — those live in per-Feature Specs and Plans.

- **Open-Meteo Gateway** — the single external seam. Two responsibilities behind one boundary:
  geocoding (`Search(name)` → Search Candidates) and weather (`GetWeather(Location)` → a bundle of
  Current Conditions + Hourly Forecast, always in canonical units). Hides HTTP, endpoint URLs, and
  JSON deserialization from the rest of the app. Confirmed to stay one module rather than splitting
  the two endpoints. Uses `IHttpClientFactory` (never a per-call `new HttpClient`) and
  `System.Text.Json`; all calls `async`/`await`.
- **Unit Conversion** — pure, deterministic conversion from canonical units (Celsius, km/h) to the
  user's chosen display units (temperature formula; wind-speed factors for mph, m/s, knots).
  Per **ADR-0001**, weather is always fetched and held in canonical units and converted in-app for
  display; a unit change re-renders held data and never triggers or can fail a network call.
- **Weather Condition Mapper** — pure mapping from Open-Meteo's numeric WMO weather code to the
  app's curated small set of Weather Conditions, and selection of the day or night Weather Icon
  variant from the `is_day` flag. The condition word is unchanged by `is_day`; only the icon varies.
- **Hourly Window** — pure computation of the forecast window: from the current hour to the next
  upcoming 05:00, both in the **Location's local time** (its wall clock, not the device's), never
  including past hours. Given a clock and the Location's timezone it is fully deterministic.
- **Search History** — a pure state machine over up to four Locations, recency-ordered and keyed by
  **Location identity** (Open-Meteo id / coordinates). Loading a Location moves it to most-recent
  (de-duping by identity); a genuinely new load at capacity evicts the oldest.
- **Favourites** — a pure state machine over up to five user-curated Locations, ordered
  most-recently-marked first. Adding at capacity is refused (block-on-overflow) with the message
  "Favourites are full — remove one first"; unmarking removes; recency never evicts. Favourite is a
  flag on Location identity, independent of the Search History.
- **Launch Resolver** — a pure decision function: most-recent Search History entry → else top
  Favourite → else the Location Search screen. Because any load reorders the history, an auto-loaded
  top Favourite becomes the most-recent history entry, so the following launch resolves via history.
- **Persistence Store** — the durable-state seam. Persists Search History, Favourites, and Units
  across restarts; deliberately never persists weather data (Current Conditions / Hourly Forecast).
- **ViewModels (MVVM)** — one per screen area (Current Conditions, Hourly Forecast, Location Search),
  orchestrating the modules above. Per Technical-Context **MVVM-only**: all UI logic lives in
  ViewModels bound to Views; code-behind holds no business logic. ViewModels own the refresh policy —
  re-fetch on Location load, on window focus regain, and on explicit manual refresh — and surface all
  failures as friendly in-app copy, never swallowing them (fail-visible).

Architectural decisions carried in from the documentation fabric:

- **Canonical-units-in, convert-locally** (ADR-0001) is the governing data-flow decision for weather
  and units. Any change to it requires a new ADR (Technical-Context: no breaking changes without an ADR).
- **Open-Meteo is the sole weather source**; keyless, no account, HTTPS. There is **no device
  geolocation** — every Location originates from a Location Search.
- **Location is a resolved place** (coordinates + label + Open-Meteo id), never a bare query string;
  identity is by resolved place, not by the query that found it.
- **Fetch coupling:** Current Conditions and the Hourly Forecast always come from a single
  `GetWeather` call, so they are mutually consistent by construction.
- **Stack:** .NET MAUI on .NET 10 / C#; `CommunityToolkit.Mvvm`; `Microsoft.Extensions.Logging`
  (`ILogger<T>` from the DI host) logging every outbound Open-Meteo call and every failure; SDK pinned
  via `global.json`.

## Testing Decisions

**What makes a good test here:** it asserts on external, observable behaviour — the shape of the
weather bundle, a state transition (e.g. "loading a duplicate Location moves it to front, count
unchanged"), or a side-effect (an error surfaced, a refresh fired) — never on internal fields,
private helpers, or implementation detail. Per the Testing & the ratchet standard, tests target the
deterministic envelope, and **every seam gets a real-IO test on at least one side** (mock-on-both-sides
only proves the mocks agree). The Open-Meteo boundary is the primary seam.

**Modules the PRD mandates tests for** (confirmed scope: pure logic + the seam):

- **Unit Conversion** — table-driven cases across every temperature and wind-speed target unit,
  including canonical-passthrough and boundary values. This is the correctness the app owns per ADR-0001.
- **Weather Condition Mapper** — WMO code → curated Weather Condition, and `is_day` → day/night icon
  variant, including codes that collapse to the same condition and an unknown/edge code.
- **Hourly Window** — the perceptual-day window across the hard cases: mid-afternoon (reaching into
  tomorrow's early hours), pre-dawn (a short strip to this morning's 05:00), exactly at 05:00, and
  across a Location whose local time differs from the device's; asserts no past hours are ever included.
- **Search History** — recency ordering, de-dupe/move-to-front by Location identity, eviction of the
  oldest at capacity four, and the "loaded (not typed)" trigger semantics.
- **Favourites** — most-recently-marked ordering, block-on-overflow at five with the exact refusal
  message, unmark removal, and independence from the Search History (marking/unmarking never reorders
  history; recency never evicts a Favourite).
- **Launch Resolver** — the three-way fallback (history → top Favourite → search) and the
  reorder-follows-load consequence (auto-loaded Favourite resolves via history on the next launch).
- **Open-Meteo Gateway** — a **Tier-1 recorded-replay** test against captured Open-Meteo fixtures for
  both geocoding and weather: the real deserialization/parsing path runs against real recorded payloads
  (fakes only at the HTTP transport), asserting the produced Search Candidates and weather bundle shape,
  plus the "no matching places" (empty result, not an error) and transport-failure (surfaced error) cases.
- **ViewModels** — covered where they carry non-trivial orchestration logic (refresh-on-focus/load/
  manual, error-surfacing, load→history→resolver wiring); thin binding-only ViewModels are not padded
  with tests for their own sake.

**Tier declaration (Feature-level, per the ratchet standard):** the primary need is **Tier 1**
(recorded-replay, real local I/O with fakes at the Open-Meteo seam, replayed fixtures — $0, every
commit). A scheduled **Tier 2** live check against the real Open-Meteo endpoints guards against the
recorded fixtures drifting from the live API contract; each Feature that touches the seam declares its
Tier-2 need and a cost ceiling at kickoff. The ratchet applies: any live (Tier-2/3) defect lands with a
cheap-tier regression bounded by the Open-Meteo seam where it surfaced.

**Prior art:** none yet — this is the first code in the repo, so these tests establish the pattern
(xUnit as the framework, a lightweight substitute library such as NSubstitute for the HTTP-transport
fake at the Gateway seam). Subsequent Features should follow the shapes these first tests set.

## Out of Scope

- **Device geolocation / "detect my location"** — the app has no location detection; every Location
  comes from a Location Search. Explicitly excluded by the domain model.
- **Weather sources other than Open-Meteo** — no multi-provider abstraction, no fallback provider.
- **Persisting weather data** — Current Conditions and the Hourly Forecast are never cached across
  restarts; offline shows an error, not stale numbers.
- **Multi-day / calendar-day forecast** — the Hourly Forecast is the perceptual-day window only; no
  7-day, daily-summary, or calendar-day view.
- **Historical or past-hours weather** — the forecast is strictly forward-looking.
- **Severe-weather alerts, radar/maps, air quality, UV, sunrise/sunset, and other measures** beyond
  Temperature, Chance of Rain, Wind Speed, and the Weather Condition/Icon.
- **Accounts, sync, or cloud backup** of Search History / Favourites / Units — all state is local to
  the device.
- **Mobile/phone form factors and touch-first layouts** — this is a desktop app; the platform matrix
  is the desktop OSes MAUI targets for this POC, not iOS/Android phones.
- **Reordering or manual editing of the Search History** — it is recency-managed automatically.
- **Localization / internationalization** of UI copy, and full accessibility conformance — not part
  of this POC's requirements (though friendly plain-language copy is required).

## Further Notes

- **Authority order** (ADR > Technical-Context > Context.MD > PRD) governs any conflict; if
  implementation reveals this PRD diverging from Context.MD or an ADR, the higher artefact wins and
  this PRD should be corrected via `/sync-project-docs`.
- **Chance of Rain has no current-moment source** in Open-Meteo — Current Conditions borrows the
  current hour's probability from the hourly data. This is a deliberate resolution of a brief-level
  ambiguity, not an approximation to be "fixed" later.
- **The 05:00 cutoff is a product decision, not a technical one** — it models the perceptual day
  boundary (~06:00, when people wake), so the window shows the rest of "today" including overnight
  hours. Do not silently change it to a calendar-day boundary.
- **Next step in the Factory flow:** this PRD feeds `/roadmap`, which breaks it into an ordered list
  of Features; each Feature then gets its own `/brainstorming` → Spec → Plan before any code is written.
- **The Roadmap lives in ADO, not a file:** the ordered Feature list is delivered in stack-rank
  order, with inter-Feature dependencies as native Predecessor links and lifecycle status on each
  work item — all held in the project's **ADO Feature work items** (ADR-0018 / ADR-0019). The former
  `Roadmap.md` has been retired; **Roadmap** keeps its slot in the authority order as the artefact
  name, independent of where it is stored.
- **First-code caveat:** because the repo has no production code yet, the first Feature will also
  stand up the MAUI DI host, `IHttpClientFactory` registration, logging wiring, `global.json` pin, and
  the xUnit test project — establishing the substrate the Testing Decisions above assume.
