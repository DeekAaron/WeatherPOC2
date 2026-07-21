# Technical Context

Engineering-practice contract for code-writing agents in this project. Every implementation must respect what's written here. If a constraint conflicts with the user's request, flag the conflict — don't silently bypass it.

## Overriding Principles
1. **Fail visible, not silent.** Errors surface to the user or the log — never swallowed. A caught exception that produces no user-visible message and no log entry is a rejectable defect.
2. **MVVM only.** All UI logic lives in ViewModels; code-behind holds no business logic. Views bind to ViewModels. This keeps logic testable without the UI.
3. **No secrets in source.** No API keys, connection strings, or credentials committed to the repo or baked into `appsettings`/`MauiProgram`. Open-Meteo needs no key today; any future keyed service goes through platform secure storage (`SecureStorage`), never source.
4. **Async I/O only.** All network and file access is `async`/`await`; nothing blocks the UI thread. No `.Result`, no `.Wait()`, no synchronous `HttpClient` calls.
5. **No breaking changes without an ADR.** A change to an architectural decision (framework, data flow, public contract) lands an ADR in `docs/adr/` first.

## Frameworks
- **.NET MAUI (on .NET 10)** — cross-platform desktop application framework; owns the UI shell, navigation, and dependency-injection host.
- **.NET 10 / C#** — runtime and language. `TargetFramework` is `net10.0` (plus the platform-specific TFMs MAUI generates).

## 3rd-party tech
- **Open-Meteo** — external weather data API. Source of all forecast/current-conditions data. No API key or account required (free, keyless HTTPS endpoints).

## Packages in use
Reach for these before pulling in a new dependency. New dependencies are a deliberate decision, not a reflex.

**HTTP:** `System.Net.Http.HttpClient` via `IHttpClientFactory` (registered in the MAUI DI host) — do not `new` up `HttpClient` per call.
**JSON:** `System.Text.Json` — the built-in serializer; no third-party JSON library.
**MVVM:** `CommunityToolkit.Mvvm` — source-generated `ObservableObject`/`RelayCommand` for ViewModels.
**Logging:** `Microsoft.Extensions.Logging` (`ILogger<T>`).
**Testing:** `xUnit` for the test framework; a lightweight substitute library (e.g. `NSubstitute`) for fakes at seams.

## Toolchain pinning
- Policy: **exact-pinned** — the conscious per-project decision. A build that works today must work on a later install day.
- Pinned tools: the **.NET SDK is pinned via `global.json`** at the repo root (`sdk.version`, e.g. `10.0.100`, with `rollForward: latestFeature`). The project `TargetFramework` is `net10.0`. MAUI workloads are restored from the pinned SDK.
- Bumping: a version bump edits `global.json` (and the TFM if the major changes) in its own PR, reviewed and passing CI before merge — the single escape hatch.

## Instrumentation
- Logging via `Microsoft.Extensions.Logging` — inject `ILogger<T>` from the MAUI DI host; no static/ambient loggers.
- Output goes to the platform debug/console sink (Debug output in development). A richer sink can be added later without changing call sites.
- **Always logged:** every outbound Open-Meteo call (endpoint + outcome), every failed HTTP call (status/exception), and every caught exception. Auth/secret material is never logged.
- Log levels: `Information` for normal external calls and lifecycle events; `Warning` for handled/recoverable failures; `Error` for anything the user sees as a failure.

## User Feedback Approach
- Channel: **in-app** — inline status text or a dialog within the MAUI UI. This is a desktop app; there is no email/Slack channel.
- Tone: **friendly, plain language.** No stack traces, HTTP status codes, or exception type names in user-facing copy. "Couldn't reach the weather service — check your connection and try again," not "HttpRequestException: 503."
- Default for agent-generated copy: short, human, actionable. Say what happened and what the user can do next.

## Branching and Environments
- Model: **trunk-based.** A single long-lived `main`; work happens on short-lived feature branches merged back via PR.
- Branches: `main` (trunk) + short-lived `claude/*` / feature branches.
- Environments: local developer machine (dev) and packaged desktop builds produced from `main`. No hosted staging/prod tiers for this POC.
- Protected: **`main`** — no direct pushes; merges require a PR passing CI.

## Git Guardrails
- Never, without explicit confirmation: **force-push, `git reset --hard`, `git clean`, or branch deletion** on any shared branch.
- Never push directly to `main` — all changes go through a PR.
- Never rewrite shared history (`rebase`, `filter-branch`, `branch -D`) without explicit confirmation.
- Hooks: none wired yet. `setup-pre-commit` / `git-guardrails-claude-code` can add enforced safety nets when the project warrants it.

## Testing & the ratchet
> Seeded from the Factory standard — **opt-out, not opt-in**. Trim a line only with a deliberate, recorded decision; don't start from nothing. Full standard: <https://github.com/kitcox-dev/enate-claude-skills/blob/main/docs/testing-and-the-ratchet-standard.md> · the *why*: <https://github.com/kitcox-dev/enate-claude-skills/blob/main/docs/agentic-e2e-testing-principles.md>.

- **Test the contract, not the LLM.** Assert on the deterministic envelope (output shape, state transition, side-effect ordering), never on generated text or chain-of-thought.
- **Every seam gets a real-IO test on at least one side.** Mock-on-both-sides only proves the mocks agree; defects live at integration seams, not inside modules. (The Open-Meteo boundary is the primary seam here.)
- **Three tiers, each catching a class the others can't.** Tier 1 — recorded-replay (real local I/O, fakes at the seams, replayed API fixtures; $0, every commit); Tier 2 — live, disposable (real subprocess against a per-run throwaway substrate; bounded cost, scheduled); Tier 3 — live (real production substrate and subprocess; continuous).
- **Platform matrix is non-negotiable for OS-touching code.** Filesystem/subprocess code runs Tier 1 + Tier 2 on every OS it supports, not just the dev box's.
- **Plan test coverage at the Feature level, not the Story level.** Each Feature declares at kickoff which tiers it needs, which fixtures/substrates must exist, and a Tier-2 cost ceiling.
- **The ratchet — every Tier-3 defect pins a cheap-tier regression.** A live defect's fix lands with a Tier-1 (or Tier-2) regression that would have caught it, encoding the **class** of defect **bounded by the seam** where it surfaced — not the exact reproducer, and not a project-wide sweep. A Story that surfaced from a live defect carries that ratcheting fixture as an acceptance criterion.
- **Disposable substrate, not shared.** Tier-2 substrate is created per run and torn down; reproducibility comes from a seed (tarball/manifest/script), never persistent state.
- **Assert on observable behaviour, not internal state.** For Tier 2/3 the target is what an operator could verify from external artefacts — tracker state, commit shape, ordered log events, posted comments.
