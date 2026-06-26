# Imports

## Purpose

Let admins bulk-load teams (and the pools / divisions they belong to) from a CSV file, with format auto-detection, client-side preview, and a help dialog that documents the accepted shapes.

## Routes

| Route | Page |
| --- | --- |
| `/imports` | CSV upload + preview + import workflow |

## Page header / toolbar

- "Imports" heading (`Typo.h4`).
- `Info` icon button (aria-label "CSV format help") that opens [`ImportFormatHelpDialog`](../ui/dialogs.md#importformathelpdialog).

## Page body

### Upload card (`MudPaper` outlined, padding 3)

- **Status row (when a CSV is loaded):** small **Format** chip (primary, filled) — either "Predefined pools" or "Ranked teams". For Ranked teams, a dense **Strategy** select is shown next to the chip (currently only "Snake"). If the format can't be detected, a `MudAlert Warning` explains the expected headers and offers a "View expected formats" text button.
- Subtitle "CSV upload".
- Ranked-mode info banner: "Ranked import auto-builds pools: minimum 3 teams, prefers 4-team pools, and uses one 5-team pool when exactly 5 teams."
- **Drop zone** (`.upload-dropzone`):
  - 2 px dashed border, 8 px radius, min height 120 px, surface background.
  - Accepts CSV via drag-drop or click. The native `<input type="file">` is overlaid invisibly and stretched edge-to-edge.
  - Active drag adds primary border color and a subtle primary-tinted background.
  - Centered text: "Drag and drop CSV here" / "or click to browse" / and an outlined chip showing the selected file name once chosen.
- **Action row:**
  - **Preview** outlined button (small) — disabled until a format is detected. Runs client-side validation and surfaces errors or a success toast.
  - **Import** primary filled button (small) — disabled until a tournament is selected and a format is detected. Confirms via a message box, runs validation, then calls the appropriate import endpoint.

### Result panels (below the card)

- **Preview errors:** `MudAlert Warning` containing a dense list of error messages.
- **Ranked pool preview:** outlined paper with a dense list summarizing each division's computed pool sizes, e.g. "Open: 7 teams -> 4-team / 3-team", or a red error line if the team count is invalid (< 3, or other un-tileable counts).
- **Import result:** outlined paper showing **Created pools**, **Created teams**, **Updated teams**, and **Ranking rows written** counts, plus a per-row error list if any.
  - **Created teams** counts new **TournamentTeam** entries created in this tournament. New master `teams` rows are created lazily as a side effect when no existing **Team** matches by club + name.
  - **Updated teams** counts existing **TournamentTeam** entries whose pool/wave/club assignment changed. The master **Team** record is never renamed by an import.
  - **Ranking rows written** counts inserts/updates to `division_rankings` driven by `Rank` (ranked-teams CSV) or `SeedHint` (predefined-pools CSV). See [`../data-model.md`](../data-model.md#5-ranking-and-seeding) for why **Ranking** is the single source of truth for seeding input — no `seed` column on `tournament_teams`.
  - See [`../../CONTEXT.md`](../../CONTEXT.md) for the **Team** vs **TournamentTeam** distinction.

## Interactions

- **Drag-drop / file pick:** reads the file (max 2 MB), populates the in-memory CSV string, detects format from the header row (`PoolName` → predefined pools; `Rank` → ranked teams; otherwise undetected), and clears prior errors / preview / result.
- **Preview:** runs client validation. For ranked imports it also computes the pool size preview (`BuildRankedPoolPreview`).
- **Import:**
  - Confirms via message box "Apply this import to the selected tournament? This will create or update divisions, pools, and teams in the database."
  - On confirm, re-runs validation, then calls either `POST /imports/predefined-pools` or `POST /imports/ranked-teams` with the parsed rows.
  - Sets the **Import result** panel; snackbar "Import complete." (or warning if there were row errors).
- All errors surface via snackbar; the page never silently fails.

## Dialogs

- [`ImportFormatHelpDialog`](../ui/dialogs.md#importformathelpdialog) — tabbed reference for CSV column expectations.
- Confirmation message box for "Import" — see [`../ui/dialogs.md`](../ui/dialogs.md#confirmation-message-boxes).

## CSV format detection rules

- Header row must contain either `PoolName` (→ predefined pools) or `Rank` (→ ranked teams).
- Predefined-pools required columns: `DivisionName`, `PoolName`, `TeamName`, `Club`, `Wave`. Optional: `SeedHint` (integer; written to `division_rankings.rank` per division when present, deduplicated and renormalised within each division).
- Ranked-teams required columns: `DivisionName`, `Rank`, `TeamName`, `Club`, `Wave`. `Rank` is written directly to `division_rankings.rank`.
- Wave values: `AM` or `PM` only. The importer resolves each row's `Wave` value to a `waves` row on the parent tournament by matching `waves.code`. Missing waves are auto-created with `display_order` appended; the import-result panel notes any auto-created wave so the organizer can set start times in the tournament editor **Match start times** section ([`tournaments.md`](tournaments.md)).

## Ranking write rules

Both import formats are the **only** non-UI write path into `division_rankings` in V1. The import:

1. Resolves each row's `tournament_team_id` (creating new `tournament_teams` and `teams` / `clubs` as needed).
2. For ranked-teams: writes `division_rankings(division_id, tournament_team_id, rank, source='import')` directly from the `Rank` column.
3. For predefined-pools with `SeedHint`: collects the `(team, SeedHint)` pairs per division, sorts them, and writes `division_rankings` with `rank` = 1..N matching the SeedHint order, `source='import_seed_hint'`.
4. Conflicts with existing `division_rankings` rows (same `(division_id, tournament_team_id)`) overwrite, recording the old rank in the audit row (when audit is wired).

The deprecated `tournament_teams.seed` column is **not** written; see [`../data-model.md`](../data-model.md#4-team-import-and-event-entry) and the **EntrySeed (deprecated)** glossary entry in [`../../CONTEXT.md`](../../CONTEXT.md).

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md), [`../ui/dialogs.md`](../ui/dialogs.md).
- Data: [`../data-model.md`](../data-model.md#4-team-import-and-event-entry) (ImportJob, ImportRow aggregates).
- Samples: [`../sample-imports/predefined-pools-sample.csv`](../sample-imports/predefined-pools-sample.csv), [`../sample-imports/ranked-teams-snake-sample.csv`](../sample-imports/ranked-teams-snake-sample.csv).
- Sibling features: [`teams.md`](teams.md), [`divisions.md`](divisions.md).
