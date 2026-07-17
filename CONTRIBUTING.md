# Contributing — branching & merge rules

This product is built with the **Enate SDLC Factory**. Two branching rules apply, depending on
who is doing the work. Direct commits to `main` are blocked in both — `main` only ever moves
through a pull request (and review, where required).

> **Required checks.** No CI runs yet. When CI is wired it is an Azure DevOps pipeline that posts a
> status check back to the PR under the context name **`ci`**; that check is then added to the
> branch ruleset as required. Until then the ruleset enforces the structural rules — PR-only,
> linear history, no force-push, no branch deletion, squash-merge only — without a required check.

## 1. Human-in-the-loop (HITL) work

All human work — planning docs, fixes, skill changes, anything — happens on a **branch**, opened
as a **pull request** and merged into `main`. You cannot commit to `main` directly.

## 2. AFK — the orchestrator delivering Stories (current rule)

When the orchestrator delivers work autonomously it uses **one `story/<issue#>-<slug>` branch per
Story**. The agent works only inside that branch; the orchestrator owns the branch lifecycle (the
agent performs no branch operations). On `Approved`, the Story branch is **squash-merged into
`main`**. A single Story is in flight at a time.
