## What to build

The Tier-2 live drift guard: a trait-gated xUnit test (`[Trait("Tier", "2-Live")]`) that makes one real call to `api.open-meteo.com/v1/forecast` for `Location.LondonGb` through the real `OpenMeteoGateway` and asserts a `WeatherBundle` comes back within a sanity band (−60…60). The assertion is **unit-aware by construction**: the Gateway throws `WeatherUnavailableException` unless `current_units.temperature_2m == "°C"`, so a returned bundle proves the live response is in Celsius — a server-side unit-default change fails the test rather than passing a loose plausibility band. This is the ratchet's guard against fixture drift at the Open-Meteo seam.

The trait is what splits the runs: `dotnet test --filter "Tier!=2-Live"` is the per-commit command (no network dependency); `--filter "Tier=2-Live"` is the scheduled (daily) job. Cost ceiling: ≤ 5 live calls per scheduled run, once per day — Open-Meteo is free and keyless, so the ceiling is call-volume, not money.

Covers Plan Task 9. Wiring the actual schedule into an Azure DevOps pipeline is a pipeline concern **outside this story** (and outside this Feature's code, per the Plan) — the trait makes the split possible; the schedule lands with the Feature's CI setup.
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

- [ ] `LiveOpenMeteoTests` exists with `[Trait("Tier", "2-Live")]`; its comments record the cost ceiling (≤ 5 live calls per scheduled run, once per day).
- [ ] `dotnet test --filter "Tier!=2-Live"` runs every Tier-1 test and skips the live test — no network dependency on the per-commit run.
- [ ] `dotnet test --filter "Tier=2-Live"` passes against the real endpoint: the returned bundle proves the °C unit on the wire (the Gateway's unit assertion), with the −60…60 sanity band on top.
- [ ] No pipeline or schedule wiring is included — explicitly out of scope per the Plan.

## Plan and Spec (orchestrator-projected — authoritative for this Run)

- **Plan**: `docs/superpowers/plans/2026-07-21-feature1-current-temperature-fixed-location.md`
- **Spec**: `docs/superpowers/specs/2026-07-21-feature1-current-temperature-fixed-location-design.md`
