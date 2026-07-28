# Writeup for the `enate-sdlc-orchestrator` owners — `Finalizing`/`In Review` hard-fail on a deleted story branch

**Date:** 2026-07-28
**Author:** HITL operator (Enate SDLC Factory dogfood on `DeekAaron/WeatherPOC2`, ADO org `EnateInternal` / project `DCWeatherApp2`).
**Status:** parked finding — **not fixed here.** `kitcox-dev/enate-sdlc-orchestrator` is read-only for us; this is a writeup for that repo's owners, not a PR from us. Line numbers below are approximate (from a diagnosis-time clone) — treat them as a starting point, not exact anchors.

## Summary

When a story's PR is merged **and its story branch is auto-deleted** (`delete_branch_on_merge`) **before** the orchestrator reaches its `Finalizing` step, `Finalizing`'s `git clone --depth 1 --branch story/<id>` fails with **exit 128** and the story is stranded. It is mislabelled **`reason: adr` ("Architecture Compliance")** because that is the `Finalizing` state's default fail bucket. On a re-run it instead fails **`implementation`** with **"/tdd returned pass but no commits found"** (the work is already merged, so there is nothing to commit) — so the story can never reach `Done` on its own.

## Root cause

In `src/orchestrator/worker.py`, the `git clone` branch-deleted **recovery path** (the `except subprocess.CalledProcessError` block, ~lines 273–348) is gated to `current_state == "Approved"` **only** (via `_FALLBACK_REASON_FOR_STATE`, ~line 100). The `Finalizing` and `In Review` arms have **no equivalent recovery**, so a deleted branch hard-fails there instead of being recognised as already-merged.

## Why it triggers (the real precondition)

The documented intended order (`kitcox-dev/enate-claude-skills` → `docs/using-the-sdlc-factory.md`, §Part 2) puts the **merge last** (`Approved`), *after* `Finalizing`. So under the intended flow the branch still exists at `Finalizing` time and the bug never fires. The trigger is an **early / external merge** — e.g. GitHub **auto-merge on green CI** — deleting the branch **before** `Finalizing` runs.

## Evidence

- **Story #85** (WeatherPOC2 PR #52): PR merged **14:33:45**; the finalization clone failed **14:38:35** (`exit 128`). The story was already code-complete and had passed every merit gate, so it was recovered by **manually** setting `Custom.FactoryState = Done` / `System.State = Closed` — re-running could never reach `Done` (nothing to commit).
- **Same class earlier — Story #81** (WeatherPOC2 PRs #48/#49): `returncode=128` post-merge; needed a manual cherry-pick of the stranded finalization docs-sync commit onto a branch off `main`.
- **Story #86** got through cleanly because its branch had **not yet been deleted** when *its* `Finalizing` ran → the race is **intermittent**, not deterministic.

## Fix directions (for the owning team)

1. **Make `Finalizing` tolerant of a deleted story branch.** On the `git clone` failure, fall back to running the ADR/finalization diff against the **merged base** (or otherwise recognise an already-merged story) — i.e. mirror the `Approved` arm's idempotent probe into the `Finalizing` (and `In Review`) arms, rather than treating a missing branch as a hard failure with the default `adr` reason.
2. **And/or prevent the early external merge.** Disable **auto-merge on story PRs** so the orchestrator owns the merge at `Approved`, as the documented order intends — which also keeps the branch alive through `Finalizing`.

Either fix removes the race; doing both is belt-and-braces (recovery *and* prevention).

## Scope note

This concerns the **automated orchestrator path only.** The Feature-47 close-out that surfaced this finding was completed via a **manual, human-driven** feature→main merge (PR #55), which does not go through the orchestrator's `Finalizing` step, so the bug did not bite that merge.
