# Persist app state as per-key JSON documents in the app-data directory

Feature 5 (Units, ADO #46) is the first Feature that must **persist state across restarts** — the
user's chosen display **Units** survive a relaunch. This stands up the **Persistence Store**, the
durable-state seam the PRD's module decomposition names, and two later Features (Search History, #47;
Favourites, #48) extend the same seam with their own persisted state. Because this introduces a new
data-flow contract that later Features build directly on top of, **Technical-Context Principle 5 ("No
breaking changes without an ADR")** requires the governing decision be recorded here before it is built.

We persist app state as **one `System.Text.Json` document per key** in the **MAUI app-data directory**
(`FileSystem.AppDataDirectory`), behind a small `IPersistenceStore` seam
(`LoadAsync<T>(key)` / `SaveAsync<T>(key, value)`). Units are stored under the key `units` as
`units.json`; Search History and Favourites later add their own keys (`search-history`, `favourites`)
as sibling documents — no shared document to schema-merge, each concern owning its own file. Enum
values are serialized **by name** (`JsonStringEnumConverter`), so reordering an enum never re-maps a
persisted value.

We choose the JSON-file mechanism over MAUI **`Preferences`** (the built-in key–value store)
deliberately: `Preferences` is a clean fit for the two scalar Units values today, but Search History
and Favourites persist **lists of structured `Location`s**, which `Preferences` can only hold as an
opaque serialized-blob-under-one-key. Picking the structured-document mechanism now means those
Features *extend* an existing pattern rather than forcing a storage migration mid-roadmap — at the
cost of a little more upfront machinery (async file I/O and a store abstraction) than a two-line
`Preferences` call would need.

**The app-data directory is resolved behind an injected `IAppDataPathProvider`**, not called directly
from `WeatherPoc2.Core`. The App head's implementation returns `FileSystem.AppDataDirectory`; Core
depends only on the abstraction. This keeps the JSON read/write logic host-agnostic and unit-testable
with **real file I/O against a temp directory** — no MAUI SDK — consistent with the project's existing
split (Core stays MAUI-free; the App head carries platform wiring, whose on-device verification is
deferred to the HITL platform-verification story).

Accepted 2026-07-26 (David Carron). Governs decision **D2** of the Units Spec/Plan (ADO #46).

## Consequences

- A new `IPersistenceStore` / `JsonPersistenceStore` seam in `WeatherPoc2.Core`, backed by
  `System.Text.Json` with `JsonStringEnumConverter`; one file per key under the injected base directory.
  All access is `async` (Technical-Context Principle 4).
- **Read is fail-soft, per ADR-0001 (a unit change cannot fail the user) and fail-visible (Principle 1):**
  an absent file returns the caller's defaults with no log (normal first run); a malformed / unreadable
  / unknown-enum document returns defaults and logs a **Warning** (never crashes, never blocks the UI).
  A write failure is Warning-logged and not thrown (the preference simply won't survive restart).
- **The app-data directory is not assumed to pre-exist.** `FileSystem.AppDataDirectory` is not
  guaranteed to already exist on Windows *unpackaged* (dotnet/maui #22231, #7657), so the store creates
  the directory (`Directory.CreateDirectory`) before writing.
- `FileSystem.AppDataDirectory` is consumed **only** through `IAppDataPathProvider`, whose concrete
  implementation lives in `WeatherPoc2.App`; Core is testable against a temp directory and never
  references the MAUI storage API.
- Later Features (Search History #47, Favourites #48) extend this seam with additional keys/documents
  and do **not** introduce a second persistence mechanism. Any change to this decision (a different
  store, a shared document, a keyed database) requires a new ADR.
- **Weather data is still never persisted** — Current Conditions and the Hourly Forecast remain
  fetch-fresh-only (PRD; ADR-0001 holds them in canonical units in memory). This ADR governs durable
  *user-preference/curation* state only.
