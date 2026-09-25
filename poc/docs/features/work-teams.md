# Work Teams

> **Status:** **Target — not yet implemented.** This document is the contract for the V1.5 work that adds **WorkTeam** rotation to the schedule.

Vocabulary is pinned in [`../../CONTEXT.md`](../../CONTEXT.md) — **WorkTeam**, **Match**, **TournamentTeam**, **Pool**, **Bracket**, **RepairAction**.

## Purpose

Make CourtFlow's schedule complete by attaching a **WorkTeam** to every pool and bracket match — the volleyball-specific "the team that just played refs the next match" assignment that JVA, USAV, and most regional manuals require. Without this, the schedule is half-built: organizers know who's playing on each court but not who is reffing, and CourtFlow forces them to maintain a parallel spreadsheet.

The feature ships in three layers:

1. **Auto-rotation at match-generation time** — when **Snake Rankings into Pools** or **Generate brackets** runs, `match_officials.work_team_tournament_team_id` is written per the division's `ref_assignment_mode` ([`../data-model.md` §8 Work team auto-rotation](../data-model.md#work-team-auto-rotation)).
2. **Per-match override editor** on the Schedule grid and Matches list — the **Change referee** action (already in [`schedule.md`](schedule.md) and [`matches.md`](matches.md)) is extended to cover WorkTeam reassignment as well as R1 / R2 changes.
3. **Pool work-rotation panel** on Division detail — a single editor that lets the organizer see and tweak the whole rotation for a pool without going match-by-match. Useful for first-tournament setup and for "swap two teams' work assignments across the pool" corrections.

Renaming and persistence rules are detailed in [`../data-model.md` §8](../data-model.md#8-match-generation). This doc covers the UI surface.

## Routes

Work-team affordances appear in three places; no dedicated `/work-teams` route. The Division detail workspace gets a new panel, and the existing Schedule and Matches surfaces gain a richer referee dialog.

| Route | Surface |
| --- | --- |
| `/divisions/{Id:guid}` | Division detail → **Work rotation** panel (new) on each pool Stage card. |
| `/schedule` | Cell-level **Change referee** action (existing — extended). |
| `/matches` | Row-level **Change referee** action (existing — extended). |
| `/court-status` | Card-level rendering of current WorkTeam (existing card layout, populated from new data). |

## Surface 1 — Division detail: Work rotation panel

Sits inside each pool-type **Stage card** in the Division detail wizard's Step 1 (Pool Structure) body, below the per-pool **Courts** multi-select. Renders when the stage status is `seeded` or later (i.e. `pool_entries` exist and pool-play matches have been generated); rendered as a disabled placeholder card ("Available after Snake Rankings into Pools") in `configured`.

### Panel header

- Subtitle "Work rotation" (`subtitle1`) with a small info icon — tooltip "Each pool match needs a WorkTeam — another team in the pool that fields the officiating crew. Auto-set on Snake Rankings; tweak here when needed."
- Right-aligned **Reset to auto-rotation** text button — visible only when any match in the pool has `assignment_source = 'manual_override'`. Confirms "Recompute work teams for every pool match using auto-rotation? Manual overrides will be lost." then `POST /pools/{id}/work-teams:reset`. Re-runs the auto-rotation algorithm and writes `match_officials` rows back to `assignment_source = 'auto_rotation'`.

### Panel body

Per pool, a dense `MudTable` of every pool-play match with columns:

- **#** — match order within the pool (e.g. 1, 2, 3, 4, 5, 6 for a 4-team round-robin).
- **Match** — `Home vs Away` (read-only).
- **WorkTeam** — `MudSelect<TournamentTeamDto>` populated with every **TournamentTeam** in the pool *other than* the two playing teams. Default value is whatever auto-rotation wrote. Changing the value writes a manual override (see Interactions).
- **R1 / R2 override** — small icon button (`Sports` icon) that opens a popover with two optional `MudSelect`s (R1, R2). Each option list is "every TournamentTeam in the division" (R1 and R2 can come from other pools when a region requires adult coverage). The popover's footer caption explains "Leave both empty to let the WorkTeam cover R1 and R2."
- **Source** chip — `Auto` (default chip), `Manual` (info chip), `Repair` (warning chip — set when the row was written by a `work_team_reassignment` **RepairJob**).
- **Conflicts** column — small chip when the configured WorkTeam overlaps with one of their own playing matches on a different court ("Conflicts with Court 2 · 10:15 AM"). Populated by the scheduler's conflict-check.

Loading state: indeterminate progress bar on the panel.

### Panel footer

Caption-line summary: "Auto-rotation: **{ref_assignment_mode}** (set on the division)." Clicking the mode name navigates to the divisions table edit dialog for this division so the organizer can change `ref_assignment_mode` and rerun.

## Surface 2 — Schedule grid + Matches list: extended Change referee dialog

The existing **Change referee** action in [`schedule.md`](schedule.md) and [`matches.md`](matches.md) opens [`RefereeReassignmentDialog`](../ui/dialogs.md#refereereassignmentdialog), which is extended to cover WorkTeam, R1, and R2 changes:

- **Match summary** (read-only) — division, pool / bracket, time, home vs away.
- **WorkTeam** select — pool-scoped or division-scoped options as in the Division detail panel.
- **R1 override** select — optional, division-scoped.
- **R2 override** select — optional, division-scoped.
- **Reason** text field (optional, multi-line, 2 rows).
- **Preview** primary outlined button — opens the reflow preview drawer ([`court-status.md` · Reflow preview drawer](court-status.md#reflow-preview-drawer)) showing whether the change introduces conflicts on adjacent matches. Mandatory when the dialog opens against a `published` schedule version.
- **Save** primary filled button (warning color when the preview surfaced conflicts).

On save the dialog:
1. Creates (or extends) a **RepairJob** with `trigger_type = 'work_team_reassignment'`.
2. Writes one or more `match_official_overrides` audit rows.
3. Updates `match_officials.work_team_tournament_team_id` (and `r1_*` / `r2_*` when set), flipping `assignment_source` to `manual_override`.
4. Forks the schedule version if the loaded one is `published` (same fork-on-mutate rule as Move — see [`schedule.md`](schedule.md#versions)).

## Surface 3 — Court Status & Matches rendering

The Court Status & Delay Dashboard's "Now playing" block (see [`court-status.md`](court-status.md#card-now-playing-block)) and the Matches list's row already display a "Ref: TeamName" line in their layouts; this feature wires that text to `match_officials.work_team_tournament_team_id` instead of the V1 placeholder. When R1 or R2 overrides are set, the line reads `Ref: TeamName · R1: OtherTeam · R2: OtherTeam`.

The Schedule grid cell already has a "Ref: TeamName" meta line (per [`schedule.md`](schedule.md#main-content-one-of) — "**status**, wave, and optional 'Ref: TeamName'"). Same source.

## Interactions

- **Division detail Work rotation panel:**
  - Changing a WorkTeam select dispatches `POST /matches/{id}/work-team` with the new team id. Snackbar "Work team updated — match #{n}". Re-renders the row to show `Source = Manual`.
  - Opening the R1 / R2 popover saves on apply via the same endpoint with optional `r1_tournament_team_id` / `r2_tournament_team_id` payloads.
  - Conflicts cell auto-updates when changes are saved (the response carries the recomputed conflict status).
  - **Reset to auto-rotation:** confirms, then `POST /pools/{id}/work-teams:reset` recomputes the entire pool's WorkTeams. Snackbar "Work teams reset — 6 matches recomputed". Audit log entry per match.
- **Change referee dialog (Schedule / Matches):**
  - Preview is the same `POST /schedule-versions/{loadedId}:preview` call documented in [`../data-model.md` §11 Reflow preview](../data-model.md#reflow-preview).
  - Apply is `POST /schedule-versions/{loadedId}:apply-preview` for multi-match impact, or a single `POST /matches/{id}/work-team` write when the impact is a single match with no conflicts.
- **All surfaces:** WorkTeam validation rules from [`../data-model.md` §8](../data-model.md#8-match-generation) apply — a WorkTeam can never equal either playing team; an R1 or R2 override can never equal either playing team.

## Dialogs

- [`RefereeReassignmentDialog`](../ui/dialogs.md#refereereassignmentdialog) — extended to cover WorkTeam, R1, R2.
- Confirmation message box for **Reset to auto-rotation**.

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md), [`../ui/dialogs.md`](../ui/dialogs.md).
- Data: [`../data-model.md`](../data-model.md#8-match-generation) (match_officials, work_team_auto_rotation), [`../data-model.md`](../data-model.md#11-repair-and-live-adjustment) (work_team_reassignment trigger).
- Sibling features: [`schedule.md`](schedule.md), [`matches.md`](matches.md), [`court-status.md`](court-status.md), [`division-detail.md`](division-detail.md), [`repairs.md`](repairs.md).
- Glossary: [`../../CONTEXT.md`](../../CONTEXT.md) — **WorkTeam**.
