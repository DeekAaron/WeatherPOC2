# Request `timezone=auto` so Open-Meteo timestamps are Location-local

_Status: Accepted — 2026-07-24 (David Carron). Governs decision D1 of the Hourly Forecast Feature (ADO #45)._

The Hourly Forecast displays Time in the **Location's local wall clock**, and its window boundary (the next upcoming 05:00) is defined in local time. Until now the Open-Meteo Gateway sent no `timezone` parameter, so the API returned every timestamp in **GMT**; because Current Conditions displays no time labels, this was invisible. We now add **`timezone=auto`** to the shared `GET /v1/forecast` request. Open-Meteo then returns every timestamp already shifted to the Location's local wall clock (offset-less ISO8601 strings), plus `utc_offset_seconds` / `timezone` / `timezone_abbreviation`. The Hourly Window becomes a pure function of the already-local timestamps — no device clock, no in-app timezone database, no DST arithmetic.

This **changes the meaning of timestamps in the shared `WeatherBundle`** (GMT → Location-local), which the already-built Features 1 and 2 consume — a change to a public data-flow contract. Technical-Context Overriding Principle 5 ("no breaking changes without an ADR") requires this decision to be recorded before the change is made; this ADR is that record, and is a hard prerequisite to breaking the Feature into implementation stories. The alternative — keep requesting GMT and convert to Location-local in-app — was rejected: it would reintroduce exactly the device-timezone and DST arithmetic that `timezone=auto` removes, and would make the window rule depend on an in-app timezone database rather than on the timestamps the API already localises.

## Consequences
- The Gateway request gains `timezone=auto` (alongside `forecast_days=2` and the widened `hourly=` set); `current.time` and every `hourly.time[]` entry are Location-local, offset-less ISO8601 strings.
- Local timestamps parse to `DateTime` of `Kind == DateTimeKind.Unspecified` with `CultureInfo.InvariantCulture` + `DateTimeStyles.None` — no `ToLocalTime`/`ToUniversalTime`/`AssumeLocal`, so no device-timezone or locale shift is ever applied; the parsed result is identical on any host (Seam 2).
- Current Conditions and Hourly Forecast come from the same fetch and shift together, so the current-hour rain-chance match stays correct.
- The Feature 1 / Feature 2 recorded test fixtures (captured in GMT) must be re-captured with `timezone=auto` local timestamps; the real captured payload `tests/WeatherPoc2.Core.Tests/Fixtures/openmeteo-tzauto.json` (London, 2026-07-24) is the reference shape and the external-seam recorded-replay fixture.
- Weather is still never persisted; this changes only the on-the-wire request and the in-memory timestamp semantics.
