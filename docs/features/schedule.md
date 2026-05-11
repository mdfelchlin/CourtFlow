# Schedule

## Purpose

Give admins one screen for everything about when and where matches are played: generate a schedule, see every court × time slot at a glance, spot conflicts, and re-route or score individual matches inline.

## Routes

| Route | Page |
| --- | --- |
| `/schedule` | Court × time-slot grid for the selected tournament |

## Page header / toolbar

- Page heading "Schedule" (`Typo.h4`).
- **Version chip** (read-only) immediately under the heading showing the loaded `ScheduleVersion`: `Published · v3` / `Preview · draft from 2:14 PM` / etc. Click expands the **Versions panel** (see [Versions](#versions)). When viewing anything other than the current `published` version, an inline `MudAlert Warning` banner reads "You are viewing a {status} schedule. Publish to make it the current schedule."
- Right-aligned action cluster:
  - **Generate** primary filled button (`AutoAwesome` icon, small) — opens a confirm dialog "Generate schedule". On confirm, creates a new `ScheduleVersion` in `status = 'draft'`, runs full-DB conflict checks against the draft assignment set, and on success transitions it to `status = 'preview'` and loads it into the grid. **Publishing is a separate explicit step** — see [Versions](#versions). Reloads on success.
  - **Publish** primary filled button (`CloudUpload` icon, small) — visible only when the loaded version is in `preview`. Confirms with "Publish this schedule? It will replace the current published version." On confirm, atomically transitions the loaded version to `published` and marks the previous `published` version as `superseded`.
  - **Check conflicts** outlined button (`VerifiedUser` icon, small) — runs the conflict report against the loaded `ScheduleVersion` and shows the result count via snackbar.
  - **Print ▼** outlined `MudMenu` (`Print` icon, small) — opens a menu with two options: **Print court run for today** and **Print division run for today**. Each option triggers the corresponding artifact per [`print-outputs.md`](print-outputs.md).
- All buttons disabled when no tournament is selected.

## Page body

### Conflicts banner (when present)

- `MudAlert Warning` with subtitle "N conflict(s) detected" and a dense list of `kind — message` items.

### Main content (one of)

- **Loading:** indeterminate progress bar.
- **Empty:** `MudAlert Info` with copy "Select a tournament to view its schedule." or "No scheduled matches yet. Use Generate to create pool-play matches." depending on context.
- **Schedule grid:** horizontally scrollable table (`.schedule-scroller`) with sticky first column and sticky header row.
  - Columns: leftmost **Court** column followed by one column per distinct scheduled start time. Slot headers formatted as `h:mm tt` in the tournament's time zone, or `ddd h:mm tt` if the schedule spans multiple days.
  - Rows: one per court (sorted by name).
  - Cells: either an em-dash for empty slots or a **match cell** containing:
    - "Home vs Away" team names (medium weight).
    - Meta line: **status**, wave, and **WorkTeam** label ("Ref: TeamName" — sourced from `match_officials.work_team_tournament_team_id` per [`work-teams.md`](work-teams.md)). When R1 or R2 overrides are set, the meta line extends to `Ref: TeamName · R1: OtherTeam · R2: OtherTeam`.
    - **Match clock chip** when `matches.started_at` is set or when the match is past its scheduled start without a Start press: `+12 min behind` (warning) / `−3 min ahead` (success) / `Live` (info). Mirrors the chip on the Court Status & Delay Dashboard. See [`court-status.md` · Delay computation](court-status.md#delay-computation).
    - Optional secondary chip **"Bracket"** when the match belongs to a **bracket-type Stage** (i.e. `matches.bracket_id` is set). Pool-play matches show no chip; the pool name is implied by row context. (See [`../../CONTEXT.md`](../../CONTEXT.md) for **Stage** / **Pool** / **Bracket**.)
    - Action row: **Start match** icon (`PlayArrow` — visible when the match is `not_started` and `started_at` is null), **Move** icon (`SwapHoriz`), **Score** icon (`Scoreboard`), and **Change referee** icon (`Sports`) *(target — opens [`RefereeReassignmentDialog`](../ui/dialogs.md#refereereassignmentdialog) with WorkTeam / R1 / R2 fields)*.

### Unscheduled matches panel (when any matches lack a court)

- `MudPaper` outlined, padding 3, top margin.
- Subtitle "Unscheduled matches (N)".
- Vertical stack of small outlined match cards, each rendering the same match-cell template.

### Versions

A collapsible panel anchored to the **Version chip** under the page heading. Lists every `ScheduleVersion` for the tournament, newest first. Columns:

- **#** (`schedule_versions.id` short form).
- **Status** chip (`draft` / `preview` / `published` / `superseded`) — colors per [`../ui/design-system.md`](../ui/design-system.md). One row at any time has `published`.
- **Scope** (Tournament-wide, or "Division: X" / "Wave: AM" for scoped versions).
- **Created** (`created_at` with actor).
- **Published** (`published_at`, em-dash when not yet published).
- **Diagnostics** (conflict count, unassigned match count, soft-penalty score — pulled from `schedule_versions` diagnostics json).
- **Actions:**
  - **Load** — sets this version as the one rendered on the grid (loads its `match_assignments`). Read-only for `published` / `superseded`; editable for `draft` / `preview`.
  - **Publish** — only on `preview` rows. Same as the toolbar Publish button.
  - **Discard** — only on `draft` / `preview` rows with no further references. Confirms and `DELETE`s.
  - **Compare to current** — opens a diff drawer that lists every match whose assignment differs between this version and the currently-`published` version. (Phase 2 affordance; the row exists in V1 docs as a placeholder.)
- Each `Move` action on the grid mutates the **loaded** version. If the loaded version is `published`, the Move opens a "Create draft from current?" confirm and forks a new draft before applying the change — so live edits never silently rewrite the published schedule. The forked draft can then be reviewed, optionally published.

The lifecycle reference lives in [`../data-model.md`](../data-model.md#schedule-version-status-lifecycle).

## Interactions

- **Generate:** confirmation message box → create a `ScheduleVersion` in `draft`, run conflict diagnostics → transition to `preview` → load into the grid. Snackbar "Schedule generated — review and publish".
- **Publish:** confirmation message box → atomically transition the loaded `preview` version to `published` and mark the previously-published version `superseded`. Snackbar "Schedule published".
- **Check conflicts:** runs conflict diagnostics for the loaded `ScheduleVersion` → populates the conflicts banner; snackbar "Found N conflict(s)".
- **Start match (cell action):** `POST /matches/{id}:start`. Writes `matches.started_at = now()` and transitions `match_status` to `in_progress`. The clock chip on the cell becomes `Live` and the Court Status dashboard's chip recomputes. See [`../data-model.md` §8 · Match clock writes](../data-model.md#match-clock-writes).
- **Move (cell action):** opens [`MatchMoveDialog`](../ui/dialogs.md#matchmovedialog). On save, **reassigns the match** by creating or amending a `MatchAssignment` against the loaded `ScheduleVersion`. The match's identity, participants, and stage are not modified. The user-facing label stays "Move". If the loaded version is `published` and the move's cascade impact is multi-match (the schedule engine's dry-run detects downstream shifts), the dialog auto-opens the [Reflow preview drawer](court-status.md#reflow-preview-drawer) before applying. Otherwise the action first forks a new `draft` version (see [Versions](#versions)). Reloads on success.
- **Score (cell action):** opens [`ScoreDialog`](../ui/dialogs.md#scoredialog). Scoring is a `Match` concern (not an assignment concern) and writes through directly regardless of which `ScheduleVersion` is loaded; on save the match itself is updated and reload follows. **Auto-clock fallback:** if `matches.started_at` is null at score-save time, the same transaction writes `started_at = now()` per [`../data-model.md` §8 · Match clock writes](../data-model.md#match-clock-writes). The dialog's **Forfeit** tab is the entry point when a team no-shows or withdraws mid-match; see [`ScoreDialog`](../ui/dialogs.md#scoredialog).
- **Change referee (cell action, target):** opens [`RefereeReassignmentDialog`](../ui/dialogs.md#refereereassignmentdialog) with WorkTeam / R1 / R2 fields. See [`repairs.md`](repairs.md#trigger-3-referee-reassignment-workteam--r1--r2) and [`work-teams.md`](work-teams.md).
- **Print (toolbar menu):** the toolbar menu options trigger `window.print()` after applying the print stylesheet's court-run or division-run layout. See [`print-outputs.md`](print-outputs.md).
- **Versions panel:** Load / Publish / Discard / Compare actions per the [Versions](#versions) subsection.
- Initial load picks the `published` version by default (or the most recent `preview` when none is published), fetches its `match_assignments`, plus venues, courts, and division team names so the cells can resolve readable labels.
- All errors surface via snackbar; the grid renders whatever data it has.

## Gap from current V1 implementation

The target model treats `ScheduleVersion` and `MatchAssignment` as first-class resources separate from `Match`, with an explicit `draft → preview → published → superseded` lifecycle. The current V1 API does not yet expose this: today the move action is a single `POST /matches/{id}:move` call that mutates the match in place, schedule generation is `POST /tournaments/{id}/schedule:generate` that publishes implicitly, and there is no Versions panel. Per [`../data-model.md`](../data-model.md#9-scheduling-and-assignment) and the [Schedule version status lifecycle](../data-model.md#schedule-version-status-lifecycle), these will migrate to versioned endpoints (e.g. `POST /schedule-versions` for create-draft, `POST /schedule-versions/{id}:publish` for publish, `POST /schedule-versions/{id}/assignments` for moves). The user-facing labels and behaviors on this page evolve with that migration — most notably the explicit Publish step appears.

## Dialogs

- [`MatchMoveDialog`](../ui/dialogs.md#matchmovedialog).
- [`ScoreDialog`](../ui/dialogs.md#scoredialog).
- [`RefereeReassignmentDialog`](../ui/dialogs.md#refereereassignmentdialog) — target; referee reassignment repair trigger.
- Confirmation message box for "Generate schedule" — see [`../ui/dialogs.md`](../ui/dialogs.md#confirmation-message-boxes).

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md), [`../ui/dialogs.md`](../ui/dialogs.md).
- Data: [`../data-model.md`](../data-model.md#9-scheduling-and-assignment) (ScheduleVersion, MatchAssignment, conflicts, diagnostics), [`../data-model.md`](../data-model.md#match-clock-writes) (Match clock), [`../data-model.md`](../data-model.md#reflow-preview) (Reflow preview).
- Architecture: [`../architecture.md`](../architecture.md#conflict--scheduling-correctness) (full-DB conflict checks).
- Sibling features: [`matches.md`](matches.md), [`court-status.md`](court-status.md), [`repairs.md`](repairs.md), [`work-teams.md`](work-teams.md), [`print-outputs.md`](print-outputs.md).
