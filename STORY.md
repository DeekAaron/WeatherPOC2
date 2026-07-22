## What to build

The Current Conditions ViewModel and the OS-agnostic DI graph — the app-layer behaviour a user's launch will trigger, fully provable per-commit in Core.

- `CurrentConditionsViewModel` (CommunityToolkit.Mvvm `ObservableObject`): an async `LoadCommand` fetches via `IWeatherGateway` for `Location.LondonGb`, sets `TemperatureDisplay` (e.g. `"23.3 °C"`, `InvariantCulture`, canonical °C — conversion is Feature 5 per ADR-0001) with a null `ErrorMessage` on success; catches `WeatherUnavailableException` and sets exactly the friendly copy "Couldn't reach the weather service — check your connection and try again." with an empty `TemperatureDisplay` on failure — caught, never swallowed (fail-visible); `IsLoading` wraps the fetch. Weather lives only in VM memory — nothing persisted (Req 52), recovery in F1 is app relaunch by design (no retry button; manual refresh is Feature 9).
- `AddWeatherPoc2Core` DI extension: named `HttpClient` for the Gateway (`AddHttpClient` — never a per-call `new HttpClient`), singleton `IWeatherGateway` → `OpenMeteoGateway`, transient `CurrentConditionsViewModel`. Proven by `ServiceRegistrationTests` resolving the ViewModel and its whole gateway graph from a real container with `BuildServiceProvider(validateScopes: true)` — **Seam 3 clause (c-1)**, the OS-agnostic clause; clause (c-2), the real MAUI host, is owned by the platform-verification story.

Covers Plan Tasks 6 and 7.

**Known-issue carried from the gauntlet (informational):** A6 — "Current Conditions" naming for a Temperature-only view is deliberate tracer-bullet forward-shaping; Feature 2 extends the same view into the full panel.

**Security pass (/check-security-design):** `AddWeatherPoc2Core` configures the named OpenMeteo client rather than registering it bare (the Plan is silent on client configuration; this enriches, not re-decides):

- `Timeout` = **15 seconds** (value decided at the interactive security design review) — a slow-dripping response fails visible in 15 s instead of holding the spinner for the 100 s framework default; expiry surfaces as `TaskCanceledException`, which the Gateway's transport catch (proven in the failure-paths story) converts to the friendly error.
- `MaxResponseContentBufferSize` = **1,048,576 bytes (1 MB)** — bounds a hostile or misbehaving oversized response (~3,000× the real ~300-byte payload); exceeding it surfaces as `HttpRequestException` through the same transport catch.

Shape: `services.AddHttpClient(OpenMeteoGateway.HttpClientName, c => { c.Timeout = TimeSpan.FromSeconds(15); c.MaxResponseContentBufferSize = 1_048_576; });`
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

- [ ] ViewModel success test green: `TemperatureDisplay == "23.3 °C"`, `ErrorMessage` null, `IsLoading` false after the load completes.
- [ ] ViewModel failure test green: `ErrorMessage` equals exactly "Couldn't reach the weather service — check your connection and try again.", `TemperatureDisplay` empty, `IsLoading` false — the exception is caught and surfaced, never swallowed (fail-visible).
- [ ] No stack traces, HTTP status codes, or exception type names in user-facing copy (Req 53; Technical-Context User Feedback rule).
- [ ] `AddWeatherPoc2Core` registers the named `HttpClient`, `IWeatherGateway` → `OpenMeteoGateway` (singleton), and `CurrentConditionsViewModel` (transient); `ServiceRegistrationTests` resolves the ViewModel and an `OpenMeteoGateway` from `BuildServiceProvider(validateScopes: true)` (Seam 3 clause c-1).
- [ ] Weather data is held only in memory on the ViewModel — no persistence code (Req 52).
- [ ] The whole Core suite is green: Location, Gateway ×9 (incl. the two security-pass tests from the failure-paths story), ViewModel ×2, registration ×1 (extended with the named-client configuration assertions below).

### Security acceptance criteria
<!-- Added by /check-security-design (threat: a slow-dripping or arbitrarily large Open-Meteo response holds the connection or exhausts memory — resource exhaustion at the app's only trust boundary). Each is independently
     testable; the post-PR /review-implementation-security gate checks the merged diff
     against these. -->
- [ ] The named OpenMeteo `HttpClient` obtained via `IHttpClientFactory.CreateClient(OpenMeteoGateway.HttpClientName)` from the `AddWeatherPoc2Core` container has `Timeout == TimeSpan.FromSeconds(15)`; asserted by a `ServiceRegistrationTests` (or sibling) test.
- [ ] The same named client has `MaxResponseContentBufferSize == 1_048_576` (1 MB); asserted by the same (or a sibling) test — an oversized response then fails as a transport error and surfaces as the friendly message, never an unbounded buffer.

## Plan and Spec (orchestrator-projected — authoritative for this Run)

- **Plan**: `docs/superpowers/plans/2026-07-21-feature1-current-temperature-fixed-location.md`
- **Spec**: `docs/superpowers/specs/2026-07-21-feature1-current-temperature-fixed-location-design.md`
