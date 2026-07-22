## What to build

The thin MAUI app head that completes the end-to-end path in code: on launch the app shows the Current Conditions page, which fires the ViewModel's `LoadCommand` on appearing — fetch-on-load, the **only** refresh trigger in Feature 1 (focus-regain and manual refresh are Feature 9) — rendering London's Temperature or the friendly error.

- `WeatherPoc2.App.csproj` (from Plan Task 1 Step 4, moved into this story): `net10.0-maccatalyst` always; the Windows TFM (`net10.0-windows10.0.19041.0`) guarded behind `IsOSPlatform('windows')` so a non-Windows build leg only builds the Mac Catalyst head. Added to `WeatherPoc2.sln`.
- `MauiProgram.CreateMauiApp` — the DI host: calls `AddWeatherPoc2Core()` and registers `CurrentConditionsPage` + `AppShell` (this is Seam 3 clause (c-2)'s owning symbol).
- `App.xaml(.cs)`, `AppShell.xaml(.cs)` (single `ShellContent` route to the page), and `Views/CurrentConditionsPage.xaml(.cs)`: labels bound to `TemperatureDisplay` / `ErrorMessage`, an `ActivityIndicator` bound to `IsLoading`; code-behind only fires `LoadCommand` in `OnAppearing` — no business logic (MVVM-only).

Covers Plan Task 8 Steps 1–5. **Deliberately excluded:** Task 8 Step 6's platform build/launch proof (Seam 3 clause c-2) — the Linux AFK runner cannot build either desktop head, so that proof is owned by the follow-on HITL platform-verification story. This story's falsifiable checks are the file/code contract and the Core suite staying green.
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

- [ ] `WeatherPoc2.App.csproj` matches Plan Task 1 Step 4: `net10.0-maccatalyst` always, Windows TFM only when `IsOSPlatform('windows')`, `UseMaui`, single project; referenced from `WeatherPoc2.sln`.
- [ ] `MauiProgram.CreateMauiApp` calls `AddWeatherPoc2Core()` and registers `CurrentConditionsPage` and `AppShell` in DI (proof of resolution on a real host is deferred to the platform-verification story).
- [ ] `AppShell.xaml` routes to `CurrentConditionsPage`; the page binds `IsLoading` (spinner), `TemperatureDisplay`, and `ErrorMessage` with `x:DataType` on the ViewModel.
- [ ] Code-behind contains no business logic: `CurrentConditionsPage.xaml.cs` only sets `BindingContext` and fires `LoadCommand` on `OnAppearing` (MVVM-only).
- [ ] No persistence code (Req 52) and no secrets in source (Open-Meteo is keyless).
- [ ] The Core test suite still passes on the AFK runner (`dotnet test`, Tier-1 filter) — the app head introduces no Core change.

## Plan and Spec (orchestrator-projected — authoritative for this Run)

- **Plan**: `docs/superpowers/plans/2026-07-21-feature1-current-temperature-fixed-location.md`
- **Spec**: `docs/superpowers/specs/2026-07-21-feature1-current-temperature-fixed-location-design.md`
