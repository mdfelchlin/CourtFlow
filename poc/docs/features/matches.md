# Matches

> **Origin note:** Rows in this list aren't created here. **Match identity is generated** at two upstream moments — pool play matches when the pool stage transitions `configured → seeded` (via **Snake Rankings into Pools**), and bracket-tree matches when **Generate brackets** runs in Step 2 of the Division detail wizard. This page is a read + score surface, not an authoring surface. See [`../data-model.md`](../data-model.md#when-matches-are-created) and [`division-detail.md`](division-detail.md).

## Purpose

Provide a flat, sortable list of every match in the selected tournament so admins can find a specific match quickly and enter scores without going through the schedule grid.

## Routes

| Route | Page |
| --- | --- |
| `/matches` | Flat matches table with scoring action |

## Page header / toolbar

- Page heading "Matches" (`Typo.h4`).
- Right-aligned **Print score sheet** action — disabled until a row is selected. Triggers the per-match score sheet print artifact described in [`print-outputs.md`](print-outputs.md) for the selected match.

## Page body

`MudDataGrid<MatchDto>` (dense, hover, not groupable) listing all matches for the selected tournament. Columns:

- **Start (UTC)** — `ScheduledStartUtc`, format `g`.
- **Status** — `MatchStatusDto` (Not Started / In Progress / Final / Canceled). See [`../../CONTEXT.md`](../../CONTEXT.md) for **Status** vs **State**.
- **Clock delta** — `+12 min behind` / `Live` / `−3 min ahead` / `Final` chip computed from `matches.started_at` and the match's `MatchAssignment.scheduled_start`. Same chip as the Schedule grid cell and the Court Status dashboard. See [`court-status.md` · Delay computation](court-status.md#delay-computation).
- **Wave** — AM or PM.
- **Ref (WorkTeam)** — `match_officials.work_team_tournament_team_id` rendered as team name; R1 / R2 overrides surface in a tooltip on hover. See [`work-teams.md`](work-teams.md).
- **Score** — joined set scores (`H-A, H-A, ...`).
- **Actions** — **Start match** icon button (`PlayArrow`) visible only when `not_started` and `started_at` is null; **Score** icon button (`Scoreboard`) opens [`ScoreDialog`](../ui/dialogs.md#scoredialog); **Change referee** icon button (`Sports`) opens [`RefereeReassignmentDialog`](../ui/dialogs.md#refereereassignmentdialog); **Print score sheet** icon button (`Print`) per [`print-outputs.md`](print-outputs.md).

## Interactions

- Initial load and reload: `GET /tournaments/{id}/matches`.
- **Start match** action: `POST /matches/{id}:start`. Writes `matches.started_at = now()` and transitions `match_status` to `in_progress`. See [`../data-model.md` §8 · Match clock writes](../data-model.md#match-clock-writes). Snackbar "Match started".
- **Score** action opens [`ScoreDialog`](../ui/dialogs.md#scoredialog). On save (not canceled) reloads the list. **Auto-clock fallback:** if `matches.started_at` is null at score-save time, the same transaction writes `started_at = now()` per the match-clock contract.
- **Forfeit** is reached via the **Forfeit** tab inside `ScoreDialog` (no separate row action). Use when one or both teams no-show or withdraw mid-match. The forfeit path records `matches.forfeit_reason`, marks the match `final`, sets `completed_at`, and the V1 standings tally credits a `W` to the non-forfeiting team without touching set / point totals (see [`../data-model.md` §10](../data-model.md#10-score-reporting-and-winner-resolution) "Forfeit handling").
- **Change referee** (target): opens [`RefereeReassignmentDialog`](../ui/dialogs.md#refereereassignmentdialog) with WorkTeam / R1 / R2 fields; completes a `work_team_reassignment` repair — [`repairs.md`](repairs.md#trigger-3-referee-reassignment-workteam--r1--r2).
- **Print score sheet** action: triggers `window.print()` after applying the score-sheet print stylesheet per [`print-outputs.md`](print-outputs.md).
- All errors surface via snackbar.

## Dialogs

- [`ScoreDialog`](../ui/dialogs.md#scoredialog).
- [`RefereeReassignmentDialog`](../ui/dialogs.md#refereereassignmentdialog) — target; referee reassignment repair trigger.

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md), [`../ui/dialogs.md`](../ui/dialogs.md).
- Data: [`../data-model.md`](../data-model.md#8-match-generation) (Match aggregate, Match clock, match_officials, WorkTeam auto-rotation), [`../data-model.md`](../data-model.md#10-score-reporting-and-winner-resolution) (MatchResultReport, MatchSet).
- Sibling features: [`schedule.md`](schedule.md), [`standings.md`](standings.md), [`repairs.md`](repairs.md), [`court-status.md`](court-status.md), [`work-teams.md`](work-teams.md), [`print-outputs.md`](print-outputs.md).
