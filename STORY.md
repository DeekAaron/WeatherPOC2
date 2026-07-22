## What to build

The walking skeleton of the testable Core: pin the SDK (`global.json`: `10.0.100`, `rollForward: latestFeature`), create the solution with the `WeatherPoc2.Core` class library (`net10.0`) and the `WeatherPoc2.Core.Tests` xUnit project, add the domain records, and stand up the Open-Meteo Gateway seam's happy path end-to-end: a captured real Open-Meteo 200 body flows through real file I/O and real `System.Text.Json` into a `WeatherBundle` carrying London's current Temperature in canonical °C.

- `Location` is a resolved place per `Context.MD` (coordinates + label + `OpenMeteoId`, null until geocoding mints it in Feature 3); Feature 1 exposes the single hard-coded constant `Location.LondonGb` (51.5074, −0.1278, "London, GB").
- `OpenMeteoGateway.GetWeatherAsync` builds the `/v1/forecast` request with `&temperature_unit=celsius` **explicitly**, deserializes with `System.Text.Json` including the `current_units` DTO (the unit *assertion* branch lands with the failure-paths story, TDD ordering), maps `current.temperature_2m` → `WeatherBundle.CurrentTemperatureCelsius`, and logs endpoint (URL) + outcome via `ILogger<OpenMeteoGateway>` (Technical-Context Instrumentation contract).
- The recorded-replay test fakes only the HTTP transport (stub `HttpMessageHandler`); the fixture is read from the test output directory via `CopyToOutputDirectory` — the Seam 2 build/emit contract.

Covers Plan Tasks 1 (Core + Tests portions), 2, and 4. The MAUI app-head project of Task 1 is deliberately **not** in this story — it is added by the app-head story, because MAUI workloads do not restore on the Linux AFK runner.

**Known-issues carried from the feature-doc-gauntlet:** A2 — this is the first story to touch the scaffold, so its **first proof** is `dotnet restore` / `build` / `test` green on a .NET-10-SDK runner (the pinned package versions are pinned-but-unverified until that live restore). A7 (informational) — the committed happy-path fixture (`23.3` @ 14:15) and the Spec's Seam-1 (e) re-grounding quote (`23.8` @ 15:00) are two different valid same-day live captures, not a contradiction.
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

- [ ] First proof (gauntlet advisory A2): `dotnet restore`, `dotnet build`, and `dotnet test` run green on a .NET-10-SDK runner, confirming the pinned package versions against a live restore.
- [ ] `global.json` pins `sdk.version` `10.0.100` with `rollForward: latestFeature`; `WeatherPoc2.sln` contains `WeatherPoc2.Core` and `WeatherPoc2.Core.Tests` per Plan Task 1 (the App head project is added by the app-head story).
- [ ] `Location` record with `Location.LondonGb` — latitude 51.5074, longitude −0.1278, label "London, GB", `OpenMeteoId` null — proven by `LocationTests`.
- [ ] `WeatherBundle` record carries `CurrentTemperatureCelsius` (canonical °C per ADR-0001); no unit conversion anywhere (that is Feature 5).
- [ ] `IWeatherGateway` / `OpenMeteoGateway` exist with the full-shape signature `GetWeatherAsync(Location, CancellationToken)`; the request URL includes `&temperature_unit=celsius` explicitly; the `current_units` DTO is deserialized.
- [ ] Recorded-replay happy-path test green: the live-captured `current-temperature-london-200.json` fixture, read via real file I/O from the test output directory (`CopyToOutputDirectory` — Seam 2), through real `System.Text.Json` → `CurrentTemperatureCelsius == 23.3`.
- [ ] The Gateway logs endpoint (URL) + outcome via `ILogger<OpenMeteoGateway>` on the request line and every outcome line (Technical-Context Instrumentation contract).
- [ ] No persistence code (Req 52); all I/O is async — no `.Result`, no `.Wait()` (Technical-Context).

## Plan and Spec (orchestrator-projected — authoritative for this Run)

- **Plan**: `docs/superpowers/plans/2026-07-21-feature1-current-temperature-fixed-location.md`
- **Spec**: `docs/superpowers/specs/2026-07-21-feature1-current-temperature-fixed-location-design.md`
