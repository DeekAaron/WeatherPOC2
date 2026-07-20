# Screenshots for `docs/new-project-setup.md`

These were extracted from the *"Call with Cristina Raicovici"* KT recording (WeatherPOC2 setup,
17 Jul 2026) and cropped to remove the webcam overlay. The guide embeds them by the filenames
below.

| File | Screen | Status |
|------|--------|--------|
| `00-create-repo.png` | The created `WeatherPOC2` repo (landing page) | ✅ captured |
| `01-branch-ruleset.png` | `ProtectMain` ruleset header — Active, target = default branch | ✅ captured |
| `01b-branch-rules.png` | The four branch rules ticked | ✅ captured |
| `02-app-repo-access.png` | Claude GitHub App — permissions + repository access | ✅ captured |
| `03a-github-pat-form.png` | New fine-grained PAT form (name / owner / repo access) | ✅ captured |
| `03-github-pat-permissions.png` | PAT permissions — Contents / Metadata / PRs / Workflows | ✅ captured |
| `04-ado-create-project.png` | ADO New-project on EnateInternal — Git + Enate Agentic Agile | ✅ captured |
| `05-ado-pat-scopes.png` | ADO New Token — Work Items Read & write | ✅ captured |
| `05b-ado-pat-regenerate.png` | ADO Manage tokens — Regenerate available | ✅ captured |
| `06a-connectors-list.png` | Claude connectors — the two Azure DevOps connectors | ✅ captured |
| `06-connector-permissions.png` | Per-tool approve/ask toggles for the ADO connector | ✅ captured |
| `07-init-repo.png` | `/init-repo` org/project prompt + generated `.factory.yml` | ⏳ **to add** — recording cuts off (~19:58) before this step; capture live |

## Note on the source video

The uploaded recording was a compressed re-encode (~20 min) whose timeline no longer matches the
`.vtt` transcript (~25 min) — so these frames were located by eye, not by transcript timestamp.
The recording also **ends mid-connector-setup**, which is why Phase 7 (`/init-repo`) has no shot.

## Secret redaction — done, but verify

Two frames in the recording exposed live credentials and were **deliberately excluded**:
- the GitHub PAT value on the "copy your token now" screen, and
- the *Steps and Tokens* spreadsheet (GitHub + ADO tokens in plaintext).

None of the committed images above show a token value (scopes/permissions were captured *before*
the value is revealed). Still, **eyeball each image** before it goes anywhere wider — browser
tabs, bookmarks, and adjacent panels can leak more than you expect.
