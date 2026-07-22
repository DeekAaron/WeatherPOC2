## What to build

Complete Seam 1's failure clause: add the typed `WeatherUnavailableException` and make `OpenMeteoGateway` convert **every** failure mode into it — always after logging endpoint + outcome at Error — so the app never surfaces a partial, fabricated, or wrong-unit Temperature (fail-visible, Reqs 11/53).

The six failure modes, each with its own Tier-1 recorded-replay test: transport failure (`HttpRequestException` / `TaskCanceledException` — the real-IO-on-one-side proof for the seam), HTTP non-200 reached via the **status guard** (the test body must be well-formed JSON so it does not trip the `JsonException` branch first), an `error:true` body (live-captured 400 fixture), an unparseable body (`JsonException` branch, named distinctly), a missing `temperature_2m`, and the **°C unit assertion** — `current_units.temperature_2m` != `"°C"` is a failure path, proven by the wrong-unit `"°F"` fixture, so a non-°C payload is never mapped through as a plausible-but-wrong number (the gauntlet's finding 2, fixed in the Plan).

Covers Plan Tasks 3 and 5. Red-phase note (corrected in gauntlet fix pass 1): four of the six tests are true reds; the `error:true` and missing-field cases are already green from the happy-path story's missing-temperature guard.

**Known-issue carried from the gauntlet (informational):** A5 — the tests constructing `HttpClient` directly around the stub handler are sanctioned test idiom; the `IHttpClientFactory` rule is a production-code constraint.

**Security pass (/check-security-design):** the transport failure mode gains two sibling tests, extending the Gateway suite from 7 to 9 tests — no new Gateway branch is added; both prove paths through the **existing** transport catch:

- a `TaskCanceledException` test — the catch clause names this exception (it is how a request-timeout expiry surfaces) but only `HttpRequestException` had a test; the timeout half of the fail-closed promise was unproven.
- an oversized-response test — a 200 body larger than 1 MB, read through an `HttpClient` whose `MaxResponseContentBufferSize` is 1,048,576 bytes (the test constructs this client directly, sanctioned per A5; the production named client gets the same cap in the ViewModel + DI story), surfaces as `HttpRequestException` and converts to `WeatherUnavailableException` — a hostile or misbehaving service can never make the app buffer an unbounded body.
## Context references

The docs the AFK Developer Agent must load before implementing this story — carried through from the Plan. Every one is a file in the checkout the Developer Agent already has; it loads them from disk and never queries the tracker:

- **Plan**: `docs/superpowers/plans/2026-07-21-feature1-current-temperature-fixed-location.md`
- **Spec**: `docs/superpowers/specs/2026-07-21-feature1-current-temperature-fixed-location-design.md`
- `Context.MD`
- `Technical-Context.MD` (Overriding Principles that apply: fail-visible-not-silent, MVVM-only, async-I/O-only, no-secrets-in-source, no-breaking-changes-without-an-ADR; plus Packages in use, Instrumentation, and Testing & the ratchet)
- `PRD.md` (Reqs 5, 10, 11, 52, 53, 54 — owned/established by this Feature)
- `Roadmap.md` → Feature 1: Current Temperature for a fixed Location (tracer bullet)
- `docs/adr/0001-convert-units-locally.md`

## Acceptance criteria (ADO Acceptance Criteria field — authoritative)

- [ ] `WeatherUnavailableException` exists and is the single typed failure the app layer catches.
- [ ] Six Tier-1 failure tests green (Plan Task 5): non-200 with a **well-formed** JSON body (reaches the status guard), `error:true` 400 fixture, unparseable body at 200 (`JsonException` branch), transport `HttpRequestException`, missing `temperature_2m`, and wrong-unit `"°F"` fixture (the unit-assertion proof).
- [ ] Branch order per Plan Task 5 Step 5: transport catch → `JsonException` catch → `error:true` → non-200 status guard → missing `temperature_2m` → °C unit assertion.
- [ ] Every failure logs endpoint (URL) + diagnostic at Error **before** throwing; no failure is swallowed (fail-visible; auth/secret material never logged — Open-Meteo has none).
- [ ] The happy-path test still passes; the full Gateway suite (happy path + six failure modes + the two security-pass tests below = 9 tests) is green via `dotnet test`.

### Security acceptance criteria
<!-- Added by /check-security-design (threat: a slow, cancelled, or arbitrarily large Open-Meteo response — the app's only trust boundary — hangs the load or exhausts memory; the timeout/cancellation branch existed but was untested). Each is independently
     testable; the post-PR /review-implementation-security gate checks the merged diff
     against these. -->
- [ ] A Tier-1 test whose stub transport throws `TaskCanceledException` asserts `GetWeatherAsync` throws `WeatherUnavailableException` — proving the timeout/cancellation half of the transport catch (previously only `HttpRequestException` was tested).
- [ ] A Tier-1 test whose stub returns a 200 response with a body larger than 1 MB, through an `HttpClient` whose `MaxResponseContentBufferSize` is 1,048,576 bytes, asserts `GetWeatherAsync` throws `WeatherUnavailableException` — the oversized read surfaces as `HttpRequestException` and converts through the existing transport catch (no new Gateway branch).

## Plan and Spec (orchestrator-projected — authoritative for this Run)

- **Plan**: `docs/superpowers/plans/2026-07-21-feature1-current-temperature-fixed-location.md`
- **Spec**: `docs/superpowers/specs/2026-07-21-feature1-current-temperature-fixed-location-design.md`
