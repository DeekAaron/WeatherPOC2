# CLAUDE.md — agent orientation

You are working in **WeatherPOC2**, a product built with the **Enate SDLC Factory**.
This file is the *agent* front door (auto-loaded every session); `README.md` is the human one.

## Read this first — and follow the flow

This product is built by walking the Factory's **HITL → AFK** flow. **Before you act, read
the field guide and follow the flow it describes:**

➡️ **[Using the Enate SDLC Factory](https://github.com/kitcox-dev/enate-claude-skills/blob/main/docs/using-the-sdlc-factory.md)**

The guide is the source of truth for *which skill to fire when*. The single rule it hinges on,
which you must never break: **only a human moves a Story to `Agent Ready`** — that is the HITL→AFK
handoff; the orchestrator owns every other transition.

## Where the Factory skills come from

The Factory skills install as the **`enate-sdlc-factory` plugin** from the `enate-skills`
marketplace declared in this repo's `.claude/settings.json` (source:
`kitcox-dev/enate-claude-skills`) — every session on this repo, desktop or cloud, loads them
automatically. Plugin-loaded skill names carry the `enate-sdlc-factory:` prefix (e.g.
`/enate-sdlc-factory:tdd`); guide references like `/tdd` mean that skill under whatever name
your available-skills list shows.

## The documentation fabric (load before you plan or build)

Authority order (lower wins): **ADR > Technical-Context > Context.MD > PRD > Roadmap > Spec > Plan.**

- **`Technical-Context.MD`** — the engineering contract every code-writing agent must respect
  (principles, secure-coding baseline, branching, and the **Testing & the ratchet** standard).
- **`Context.MD`** — the domain glossary (the project's language).
- **`docs/project-brief.md`** — the original inbound product brief (source material the PRD
  is built from; not an authority — Context.MD and PRD.md win where they diverge).
- **`PRD.md`** · **`Roadmap.md`** — product requirements; the ordered Feature list.
- **`docs/adr/`** — architectural decisions (highest authority).
- **`docs/superpowers/specs/`** · **`plans/`** — per-Feature Spec and Plan (the Plan carries
  the **Context references** an agent loads).

## Dev commands

Stack is **.NET 10 / C#** (SDK pinned via `global.json` at `10.0.100`). Solution: `WeatherPoc2.sln`.

- **Restore:** `dotnet restore`
- **Build:** `dotnet build`
- **Test (Tier 1, recorded-replay, every commit):** `dotnet test`

Built so far: `WeatherPoc2.Core` — the Open-Meteo weather seam (`OpenMeteoGateway`,
`IWeatherGateway`, `WeatherUnavailableException`, `Location`, `WeatherBundle`) — with its xUnit
test project `WeatherPoc2.Core.Tests`. The MAUI UI shell, ViewModels, and the remaining domain
modules from `PRD.md` are not built yet.
