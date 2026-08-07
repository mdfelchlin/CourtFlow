# Dialog Catalog

All modal dialogs in CourtFlow. Each per-feature PRD in [`../features/`](../features/) links here for the dialogs it invokes.

All dialogs are MudBlazor `MudDialog`s with a `DialogContent` body and a `DialogActions` row of buttons. Most cancel/save pairs use a left-aligned text "Cancel" and a right-aligned primary filled "Save" button.

---

## 1. Catalog

| Dialog | Title | Used from |
| --- | --- | --- |
| [`VenueDialog`](#venuedialog) | Add venue / Edit venue | Tournament editor |
| [`CloneTournamentDialog`](#clonetournamentdialog) | Clone tournament | Tournaments list (target) |
| [`TeamDialog`](#teamdialog) | New team / Edit team | Teams page |
| [`DivisionDialog`](#divisiondialog) | New division / Edit division | Divisions table |
| [`PoolDialog`](#pooldialog) | New pool / Edit pool | Division detail (Step 1) |
| [`MatchMoveDialog`](#matchmovedialog) | Move match | Schedule cells |
| [`ScoreDialog`](#scoredialog) | Score match | Schedule, Matches |
| [`GenerateBracketDialog`](#generatebracketdialog) | Generate bracket | Brackets toolbar |
| [`DelayCourtDialog`](#delaycourtdialog) | (no fixed title) | Court Status (planned) |
| [`WithdrawTeamDialog`](#withdrawteamdialog) | Withdraw team | Teams page (target) |
| [`CourtOutageDialog`](#courtoutagedialog) | Court unavailable | Court Status (target) |
| [`RefereeReassignmentDialog`](#refereereassignmentdialog) | Change referee | Schedule, Matches, Division detail Work rotation (target — extended) |
| [`PoolCourtSwapDialog`](#poolcourtswapdialog) | Swap pool courts | Division detail Step 1 Pool card (target) |
| [`ImportFormatHelpDialog`](#importformathelpdialog) | CSV import formats | Imports help affordance |
| [Confirmation message boxes](#confirmation-message-boxes) | Delete / Generate / Import / Start / Mark complete / Reset work rotation confirms | Throughout |

---

## VenueDialog

- **Fields:** **Name** (required), **Address** (multi-line, 2 rows, with Google Places Autocomplete when `GoogleMaps:ApiKey` is configured; otherwise a plain text field), **Number of courts** numeric (0–99).
- **Save behavior:** Save button is disabled while saving. On save, the dialog upserts the venue and atomically syncs courts to the requested count. New courts are auto-named `1`, `2`, … `N`. Reducing the count removes the highest-numbered courts first; the save is rejected if any court to be removed has scheduled or in-progress matches.

## Waves (tournament editor — not a dialog)

V1 does **not** use a separate wave dialog or Waves panel. When `session_mode = MultiWave`, the tournament editor **Match start times** section auto-maintains `AM` and `PM` `waves` rows with **start times only** (no end times, no notes). Wave assignment for teams and matches happens via imports and scheduling, not on the division editor.

See [`features/tournaments.md`](../features/tournaments.md) · Match start times.

## DivisionDialog

Opens from the [Divisions table](../features/divisions.md) **Add** toolbar button and per-row **Edit** action. **Create** mode collects **Name** only. **Edit** mode adds max teams and shows the tournament play-format caption. Authoring of pools / brackets / advancement rules happens on the [Division detail](../features/division-detail.md) workspace.

- **Create fields:**
  - **Name** text field (required; unique within the parent tournament, case-insensitive).
- **Edit fields (in addition to Name):**
  - Read-only caption: play format (set at tournament level).
  - **Max teams** numeric (2–128).
- **Save behavior:** `POST /divisions` with `{ tournamentId, name }` (new) or `PUT /divisions/{id}` (edit). On success the dialog closes, the table reloads, and a snackbar surfaces "Saved." Errors surface via snackbar.

## TeamDialog

- **Fields:**
  - **Division** select (required) — populated from the parent page's divisions list. Defaults to `InitialDivisionId` or the first division.
  - **Team name** text field (required, single-line).
  - **Club** autocomplete (clearable, lenient/non-strict, max 30 items, placeholder "Existing club or new name"). Search prepends the typed value as a "create new" option if it isn't already present. Caption beneath: "Pick from the list or type to add a new club for this tournament."
- **Validation:** division and team name are required; club is capped at 200 characters.
- **Save behavior:** in edit mode, if the division changed, calls `MoveDivisionAsync` first, then updates the team. Returns the saved DTO so the caller can patch its local list without a full reload.

## PoolDialog

Used by the Division detail surface (out of scope for V1 feature docs).

- **Fields:** **Pool name** text field (required).
- **Save behavior:** creates a pool under the supplied division id.

## MatchMoveDialog

- **Fields:**
  - **Date** picker.
  - **Time** picker.
  - **Duration (min)** numeric field, range 15–240.
  - **Court** select (Courts list passed in as a parameter).
- **Save button label:** **Move**.
- **Save behavior:** combines date+time, converts to UTC, then **`POST /api/v1/tournaments/{tournamentId}/schedule-versions/preview-move`** with `{ matchId, courtId?, scheduledStartUtc, durationMinutes }`; on success when `CanApply`, **`POST .../apply-move`** with the returned `applyToken`. The API updates **`match_assignments`** and bumps **`ScheduleVersion.MutationSequence`**; RFC 7807 responses cover validation failures, stale apply (`409`), bad token (`400`), and conflicts after preview (`422`). Match identity/participants/stage are unchanged. The **full** target still includes `POST /schedule-versions/{id}/assignments` and fork-on-published — see [`../features/schedule.md`](../features/schedule.md#gap-from-target-implementation).

## ScoreDialog

- **Body:**
  - **Mode tabs** (top of body): **Score** (default) / **Forfeit**. The dialog title updates to "Score match" or "Forfeit match" accordingly.
  - **Score tab:** vertical stack with one row per existing set ("Set N", home numeric 0–50, "-", away numeric 0–50). **Add-set** button (`CfToolbarAddButton`) below the rows. Final row: **Finalize match** checkbox.
  - **Forfeit tab:** **Forfeiting team** radio (the two participants of the match, rendered with seed and club to disambiguate; a third option "Double forfeit (no contest)" when both teams no-show). **Reason** select (`no_show`, `withdrew_mid_match`, `disciplinary`, `injury`, `other`) plus a free-form **Notes** field (multi-line, 2 rows, optional). A read-only caption: "A forfeit counts as a win for the non-forfeiting team and contributes nothing to set wins, point differential, or ratios — see [`../data-model.md` §10](../data-model.md#10-score-reporting-and-winner-resolution)."
- **Save behavior:**
  - **Score tab:** scoring updates the **`Match`** itself (not its assignment). Computes the winner from set wins (only when "Finalize" is checked) and calls `POST /matches/{id}:score`.
  - **Forfeit tab:** calls `POST /matches/{id}:forfeit` with the forfeiting team id (or `double_forfeit = true`), reason enum, and optional notes. Server sets `matches.winning_tournament_team_id` / `losing_tournament_team_id`, writes `matches.forfeit_reason`, sets `matches.completed_at` to `now()`, marks the `match_status` `final`, and skips set/point contributions in the standings recompute (per the V1 forfeit convention).
  - Both endpoints accept the same idempotency / optimistic-concurrency headers; both close the dialog on success and refresh the parent page.

## GenerateBracketDialog

- **Fields:**
  - **Bracket type** select: Single Elimination, Double Elimination.
  - **Team capacity (power of 2)** numeric field (2–64).
  - **If-necessary final (DE only)** checkbox.
- **Save button label:** **Generate**.
- **Save behavior:** calls `POST /brackets:generate` with tournament id, division id, type, capacity, and if-necessary-final flag.

## DelayCourtDialog

- **Body:** "Delay all upcoming matches on **{court name}**." Then a **Slots to delay** numeric field (1–20).
- **Actions:** Cancel / **Delay** (warning filled).
- **Save behavior:** calls `POST /courts/{id}:delay` with the slot count.

## WithdrawTeamDialog

Target. Opens from the Teams table **Withdraw** action. See [`../features/repairs.md`](../features/repairs.md#trigger-1-team-withdrawal).

- **Fields:** confirmation summary (team + division); **Effective after** optional match selector or "Immediately"; **Reason** multi-line (optional).
- **Actions:** Cancel / **Preview** (when preview API exists) / **Withdraw** (warning filled).
- **Save behavior:** creates a **RepairJob** with trigger team withdrawal and applies withdrawal fields on `tournament_teams` plus dependent **RepairActions** per server rules. Target endpoint shape TBD; not implemented in V1.

## CourtOutageDialog

Target. Opens from Court Status **Mark court unavailable**. See [`../features/repairs.md`](../features/repairs.md#trigger-2-court-outage).

- **Fields:** **Court** (read-only when opened from a card); **Starts at** / **Ends at**; **Reason** (optional).
- **Actions:** Cancel / **Preview** / **Apply** (warning filled).
- **Save behavior:** persists `court_outages`, creates a **RepairJob**, shifts or reassigns **MatchAssignments** in scope.

## RefereeReassignmentDialog

Target. Opens from Schedule, Matches, or Division detail **Work rotation** panel **Change referee**. See [`../features/repairs.md`](../features/repairs.md#trigger-3-referee-reassignment) and [`../features/work-teams.md`](../features/work-teams.md#surface-2--schedule-grid--matches-list-extended-change-referee-dialog).

- **Fields:**
  - **Match summary** (read-only) — division, pool / bracket, time, court, home vs away.
  - **WorkTeam** select — populated with every **TournamentTeam** in the match's pool (for pool matches) or the match's division (for bracket matches), excluding the two playing teams. Default value is the current `match_officials.work_team_tournament_team_id`.
  - **R1 override** select (optional) — division-scoped, excluding playing teams.
  - **R2 override** select (optional) — division-scoped, excluding playing teams.
  - **Reason** text field (multi-line, 2 rows, optional).
- **Preview** primary outlined button — opens the [Reflow preview drawer](../features/court-status.md#reflow-preview-drawer) showing whether the change introduces conflicts on adjacent matches. Mandatory when the dialog opens against a `published` schedule version.
- **Save behavior:** creates a **RepairJob** with `trigger_type = 'work_team_reassignment'`; writes one or more `match_official_overrides` audit rows; updates `match_officials` and flips `assignment_source` to `manual_override`. When the loaded schedule version is `published`, the save first forks a new `draft` version per the same fork-on-mutate rule used by Move.

## PoolCourtSwapDialog

Target. Opens from the Division detail Step 1 **Pool card → Courts strip** edit when the parent Stage status is `seeded`, `in_progress`, or `completed` *and* `match_assignments` already exist for that pool. See [`../features/repairs.md`](../features/repairs.md#trigger-5-pool-court-swap) and [`../features/division-detail.md`](../features/division-detail.md).

- **Fields:**
  - **Pool summary** (read-only) — pool name, division, current court chips.
  - **Target courts** multi-select (required, ≥ 1) — every court in the parent tournament. Pre-fills with the current court set so the dialog opens as a no-op until edited.
  - **Effective from match #** numeric (optional) — defaults to the `match_order` of the first `not_started` match in the pool. Earlier matches stay on their current courts; later matches move.
  - **Reason** text field (multi-line, 2 rows, optional).
- **Preview** primary outlined button — opens the [Reflow preview drawer](../features/court-status.md#reflow-preview-drawer) seeded with the pool-court-swap proposal. Mandatory when the loaded `ScheduleVersion` is `published`; recommended otherwise.
- **Apply** primary filled button (warning color when the preview surfaced conflicts; disabled when the target court set equals the current set).
- **Save behavior:** calls `POST /pools/{id}/court-swap:apply` with the `apply_token` from the preview. The server creates a **RepairJob** with `trigger_type = 'pool_court_swap'`, writes `repair_actions` rows for each moved `match_assignment` and for the `pool_court_allocations` diff, and forks a new `draft` schedule version when the loaded version is `published` (same fork-on-mutate rule as Move). On success the dialog closes and the parent page reloads; snackbar "Pool {name} moved to {target courts}. See Repair Jobs for the audit trail."

## CloneTournamentDialog

Target. Opens from the Tournaments table **Clone** action. See [`../features/tournaments.md`](../features/tournaments.md#interactions) and [`../data-model.md` §12 Tournament templates](../data-model.md#12-tournament-templates).

- **Fields:**
  - **Source summary** (read-only): source tournament name + dates.
  - **New tournament name** text field (required). Pre-fills to `{source name} (copy)` and selects all on focus.
  - **Start date** / **End date** date pickers (required).
  - **Time zone** select bound to `TimeZoneOptions.All`. Defaults to the source tournament's time zone.
  - **Description** text field (multi-line, 2 rows, optional). Pre-fills from source.
- **What gets copied panel** — outlined `MudPaper`, padding 2, with two columns:
  - **Copied:** divisions (with format flags, tiebreaker preset), stages (with seeding source), pools (with target size, court allocations cleared), brackets (with type, team count, advancement-rule wiring), waves (codes only), default match duration.
  - **Not copied:** teams, rankings, schedule, match assignments, scores, venues + courts (organizer relinks at the new tournament), publish status (new tournament always starts `draft`).
- **Save button label:** **Clone**.
- **Save behavior:** calls `POST /tournaments:clone` with `{ sourceTournamentId, name, startDate, endDate, timeZone, description }`. The server captures the source to a `tournament_templates` row, instantiates a new `tournaments` row in `status = 'draft'`, walks the template's children to write the structural subset described in [`../data-model.md` §12](../data-model.md#12-tournament-templates), increments the template's `usage_count`, and returns the new tournament id. The dialog closes and the page navigates to `/tournaments/{newId}`.

## ImportFormatHelpDialog

- **Tabs:** **Predefined pools** and **Ranked teams**.
- Each tab lists required and optional columns, accepted **Wave** values (`AM` / `PM` only), and a syntax-highlighted example block (`<pre class="cf-import-example">`).
- **Action:** single **Close** button.

---

## Confirmation message boxes

Use `IDialogService.ShowMessageBox(title, body, yesText, cancelText)`:

- **Destructive deletes** use `yesText: "Delete"`, `cancelText: "Cancel"`.
- **Generative actions** use `yesText: "Generate"` or `"Import"` plus `"Cancel"`.

These are not custom `MudDialog` components — they are MudBlazor's built-in message-box surface and are invoked inline from the calling page.
