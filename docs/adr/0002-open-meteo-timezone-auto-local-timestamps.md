# Request `timezone=auto` from Open-Meteo so timestamps are Location-local

The Hourly Forecast (Feature 4, ADO #45) displays **Time in the Location's local wall clock**, and its
window boundary — the next upcoming 05:00 — is defined in local time. Today the Gateway sends no
`timezone` parameter, so Open-Meteo returns every timestamp in **GMT**; this has been invisible only
because Current Conditions displays no time labels. We add **`timezone=auto`** to the Open-Meteo
request. Open-Meteo then returns every timestamp already shifted to the Location's local wall clock
(alongside `utc_offset_seconds`, `timezone`, and `timezone_abbreviation`), so the Hourly Window becomes
a pure function of the already-local timestamps — no device clock, no in-app timezone database, and no
DST arithmetic in our code.

This changes the **meaning of the timestamps carried on the shared `WeatherBundle`** (GMT →
Location-local), which is a change to a data-flow contract that Features 1–2 already consume. Under
**Technical-Context Principle #5 ("No breaking changes without an ADR")**, that governing decision must
be recorded here and land before the change is built — which is the purpose of this ADR. The change is
deliberately **additive, not a reshape**: `WeatherBundle` gains an hourly series and a parsed
`LocalNow`; no existing field is removed or repurposed. Current Conditions stays correct because the
`current.time` and `hourly.time[]` values now shift together, so current-hour rain-chance matching is
unaffected.

Accepted 2026-07-24 (David Carron). Governs decision **D1** of the Hourly Forecast Spec/Plan (ADO #45).

## Consequences

- The Open-Meteo request gains `timezone=auto`. Responses carry local wall-clock timestamps with **no
  offset designator** (e.g. `"2026-07-24T16:00"`), plus `utc_offset_seconds` / `timezone` /
  `timezone_abbreviation`. The pinned canonical units (`temperature_unit=celsius`,
  `wind_speed_unit=kmh`) are unchanged — this ADR governs timestamp semantics only, not units
  (units remain governed by ADR-0001).
- Offset-less local strings MUST be parsed to a `DateTime` of `Kind == DateTimeKind.Unspecified` using
  `CultureInfo.InvariantCulture` and `DateTimeStyles.None` — with **no** `ToLocalTime()`,
  `ToUniversalTime()`, `AssumeLocal`, `AssumeUniversal`, or `AdjustToUniversal` applied — so the
  device's own timezone and locale never shift the value. The parsed result is identical on any host.
- The Hourly Window (current hour → next local 05:00, inclusive, never past hours) is computed purely
  from these already-local values. Because it filters the actual returned local hours, a
  DST-transition day (a 23- or 25-hour day) is handled naturally — the code never assumes a fixed 24.
- **The Feature 1/2 recorded fixtures (captured as GMT) must be re-captured** with `timezone=auto`
  local timestamps; the widened seam is proven against a real captured `timezone=auto` payload
  (London, 2026-07-24) plus the culture-/device-timezone-invariance test.
- This ADR is the highest-authority artefact for the `timezone=auto` decision (per the doc-fabric
  authority order) and is a hard prerequisite for the Feature 4 (#45) breakdown and implementation.
