# Handoff — Feature #47 (Search History) status + orchestrator findings

**Date:** 2026-07-27 21:00 UTC
**Session role:** HITL operator monitoring Feature #47's stories through the AFK orchestrator (Enate SDLC Factory dogfood).
**Tracker:** Azure DevOps — org `EnateInternal`, project `DCWeatherApp2` (per `.factory.yml`). Code: GitHub `DeekAaron/WeatherPOC2`.

## Repos in scope (and one rule)

- `DeekAaron/WeatherPOC2` — the product. Ours to change.
- `kitcox-dev/enate-claude-skills` — the Factory skills. Ours to change.
- `kitcox-dev/enate-sdlc-orchestrator` — the Python orchestrator engine. **READ-ONLY. The user explicitly said never push/commit/PR here — it is not ours to change.** Added to the session only for diagnosis; cloned at `/workspace/enate-sdlc-orchestrator`.

## Feature #47 — current state (source of truth is ADO, not this doc)

Feature #47 = **Search History**, 6 child user stories. Live states as of this handoff:

| # | Story | State |
|---|---|---|
| 83 | SearchHistory — pure recency state machine | Done (Closed) |
| 84 | Prove the search-history persistence document seam | Done (Closed) |
| 85 | ILocationLoader / LocationLoader — load coordinator | Done (Closed) — **closed manually**, see below |
| 86 | Rewire LocationSearchViewModel + Recent list | Done (Closed) |
| 87 | Register SearchHistory + ILocationLoader in DI | Done (Closed) — **closed manually**, see below |
| 88 | App head: Recent list + startup hydration (platform-verification) | **HITL, open** — PR #54 raised, awaiting human on-device verification |

Read the ADO work items + their gate comments for full detail; don't trust this table over the tracker.

## Manual state changes made this session (all human-authorised)

- **#83** re-triggered to `Agent Ready` once (original fail was a transient API stall, `synthesised`). Recovered to Done on its own.
- **#85** re-triggered once, then **manually set `Custom.FactoryState=Done` + `System.State=Closed`** because its code was already merged (PR #52) and passed every merit gate; only the orchestrator's post-merge finalization failed (see Finding 1). Re-running could never reach Done (nothing to commit).
- **#87** re-triggered once (cleared a malformed-verdict readiness fail), then **manually set to Done/Closed** because #86 had already delivered its entire scope (see Finding 2).
- `Custom.FactoryFailureReason` cleared on each re-trigger.

## PR #54 — the deliverable awaiting verification

**https://github.com/DeekAaron/WeatherPOC2/pull/54** — `story/88-app-head-recent-list-hydration` → `feature/47-search-history`, commit `99f1dfa`. Authored this session; **NOT compiled** (the cloud/AFK runner is Linux and cannot build the MAUI head). Five files:

- `src/WeatherPoc2.App/MauiAppDataPathProvider.cs` (new) — host `IAppDataPathProvider` returning `FileSystem.Current.AppDataDirectory`. **This is the real prerequisite**: without it the persistence graph (`JsonPersistenceStore` → `LocationLoader`/`IUnitsService` → `LocationSearchViewModel`) cannot resolve at runtime — the app would crash at startup. It had never been wired (Units platform-verification was deferred too; there is no `SettingsPage`).
- `src/WeatherPoc2.App/MauiProgram.cs` — registers the path provider before `AddWeatherPoc2Core`.
- `src/WeatherPoc2.App/App.xaml.cs` — injects `ILocationLoader`, dispatches `HydrateAsync()` via `IDispatcher.DispatchAsync` (UI-thread affinity; fire-and-forget; store fails soft per ADR-0003).
- `src/WeatherPoc2.App/CountToBoolConverter.cs` (new) — hides the "Recent" header when history is empty.
- `src/WeatherPoc2.App/Views/LocationSearchPage.xaml` — the Recent list, matching the existing candidate-row idiom (CollectionView + Label + TapGestureRecognizer → `SelectRecentCommand`).

### Next actions for #88 (needs a human on a Windows box)

1. Install the Windows .NET 10 + MAUI build toolchain (Visual Studio with the "**.NET Multi-platform App UI development**" workload; confirm a `10.0.1xx` SDK via `dotnet --list-sdks`; `global.json` pins `10.0.100`). Windows build target is `net10.0-windows10.0.19041.0` (unpackaged, `WindowsPackageType=None`).
2. Build + run PR #54's branch and walk the verification checklist in the PR body (empty-history shows just search box; select → Recent appears; cap 4 / move-to-front; **persists across full relaunch**, PRD-32; opens on search with nothing auto-loaded; no regression).
3. If the first build throws a compile error, feed it back — fix on the branch (code authoring is the machine's job; the human does build + visual verification only).
4. On success: merge PR #54, then set #88 to Done/Closed (HITL story — the human closes it, the orchestrator won't).
5. Run `/sync-project-docs` **after** verification — `CLAUDE.md`/`README.md`/`CHANGELOG.md` still say the app-head Recent binding + startup hydration "remain"; deliberately not updated pre-verification so docs don't claim an unconfirmed render.
6. #88 is the last child — once Done, the orchestrator merges Feature #47 to `main` (F12 feature-branch integration). **Watch that merge for Finding 1's failure mode.**

## Finding 1 (parked) — orchestrator branch-lifecycle bug

**Not ours to fix in place** (lives in read-only `enate-sdlc-orchestrator`). Deliverable is an **issue/writeup for that repo's owners**, not a PR from us.

- Symptom: a story whose PR is merged (and story branch auto-deleted via `delete_branch_on_merge`) **before** the orchestrator's `Finalizing` step runs → `Finalizing` does `git clone --depth 1 --branch story/<id>` → `exit 128` → story stranded, mislabelled `reason: adr` ("Architecture Compliance") because that is the `Finalizing` state's default fail bucket. On a re-run it instead fails `implementation` with "/tdd returned pass but no commits found" (work already merged).
- Root cause pinned in `src/orchestrator/worker.py`: the `git clone` branch-deleted recovery path (`except subprocess.CalledProcessError`, ~lines 273–348) is gated to `current_state == "Approved"` ONLY (`_FALLBACK_REASON_FOR_STATE`, ~line 100). The `Finalizing` (and `In Review`) arms have no equivalent recovery, so a deleted branch hard-fails there.
- Documented intended order (skills repo `docs/using-the-sdlc-factory.md` §Part 2): merge is the LAST step (`Approved`), AFTER `Finalizing`. So the real trigger is an EARLY/EXTERNAL merge (GitHub auto-merge on green CI) deleting the branch before `Finalizing`. Evidenced: #85 PR #52 merged 14:33:45, finalization clone failed 14:38:35. Same class already bit Story #81 (see WeatherPOC2 PRs #48/#49 — `returncode=128` post-merge, needed manual cherry-pick).
- Fix directions for the owning team: (a) make `Finalizing` tolerant of a deleted story branch (run the ADR diff against the merged base / recognise already-merged — mirror the `Approved` arm's idempotent probe); and/or (b) prevent the early external merge (no auto-merge on story PRs; let the orchestrator own the merge at `Approved`).
- #86 got through fine (branch not yet deleted when its Finalizing ran) → the race is **intermittent**, not deterministic.

## Finding 2 (parked) — #86/#87 story-decomposition flaw

**Ours to address** in `kitcox-dev/enate-claude-skills` (the `enate-to-stories` Architect skill).

- #87 ("register SearchHistory + ILocationLoader in DI") was drawn as a separate slice AFTER #86 ("rewire LocationSearchViewModel"). But #86's rewire changed the VM's constructor deps, and a pre-existing test (`AddWeatherPoc2Core_resolves_the_location_search_view_model`) plus #86's own "suite green" AC FORCED #86 to add the DI registration to stay green. Verified in PR #53's diff: #86 added both `services.AddSingleton<SearchHistory>()` and `services.AddSingleton<ILocationLoader, LocationLoader>()` AND the two singleton-registration tests that are #87's ACs verbatim.
- So #87's scope was structurally a prerequisite of #86, not a follow-on — they are not independently deliverable in that order. Not agent overreach; a slice boundary cut across a real dependency.
- Lesson for `enate-to-stories`: a "register in DI" slice must never trail the slice that rewires the DI-resolved consumer — fold registration into (or before) the rewire.

## Suggested skills for the next session

- After #88 is verified + merged: `/sync-project-docs` (WeatherPOC2).
- For Finding 2: `/enate-to-stories` (read its guidance; the fix is a wording/heuristic change in the skill in `enate-claude-skills`).
- Finding 1 is a writeup for the orchestrator team — no skill; do NOT push to `enate-sdlc-orchestrator`.

## Working branches

Designated dev branch this session (both our repos): `claude/feature-47-orchestrator-progress-m4f2e1`. This handoff is committed there in WeatherPOC2. #88's code is on `story/88-app-head-recent-list-hydration` (PR #54).
