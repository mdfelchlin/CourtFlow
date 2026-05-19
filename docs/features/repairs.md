# Repairs

Vocabulary is pinned in [`../../CONTEXT.md`](../../CONTEXT.md) (**RepairJob**, **RepairAction**, **RepairTrigger**). Admin-facing labels stay plain English ("Withdraw team"); this doc uses precise terms where they matter for schema and audit.

## Purpose

Give admins a single conceptual home for **mid-tournament corrections** that affect scheduling, standings, or bracket advancement — without silent edits. Every significant fix runs as a **RepairJob** with history: who triggered it, why (**RepairTrigger**), what changed (**RepairActions**), and whether it was previewed before apply.

The data model treats this area as the main differentiator from rigid platforms; see [`../data-model.md`](../data-model.md#11-repair-and-live-adjustment).

## Routes

| Route | Page |
| --- | --- |
| `/repairs` | Repair Job history for the selected tournament (read-mostly list + drill-down) |

Named triggers are **not** isolated on `/repairs` only — each opens from its natural surface (Teams, Court Status, Schedule / Matches) and completes by creating or updating a **RepairJob**. The history route answers "what changed when Court 3 went down at 2pm?"

## Page header / toolbar (`/repairs`)

- Page heading "Repairs" (`Typo.h4`).
- Right-aligned cluster:
  - **Refresh** icon button — re-fetch jobs for the selected tournament.
  - Optional **Filter** chips or menu: by **RepairTrigger**, by status (`RepairJob` **Status**), by date range.
- Disabled when no tournament is selected (same pattern as other tournament-scoped tabs).

## Page body — Repair Job history

- **Loading:** indeterminate progress bar.
- **Empty:** `MudAlert Info` — "No repair jobs yet." when the tournament has no recorded jobs.
- **Otherwise:** `MudDataGrid` or dense table, reverse chronological. Suggested columns:
  - **Started** — local timestamp.
  - **Trigger** — Withdrawal / Court outage / Referee reassignment (maps from `trigger_type`).
  - **Status** — RepairJob status (e.g. draft / preview ready / applied / canceled / failed — exact enum lives in domain).
  - **Actor** — display name from Clerk context.
  - **Summary** — one line from server (e.g. "12 assignments shifted; 1 team withdrawn").
  - **Actions** — **View detail** opens a side drawer or full-screen detail showing ordered **RepairActions** with before/after JSON or humanized diff.

**Undo / rollback:** In scope as a *future* enhancement unless the domain explicitly supports compensating actions; if not in V1, omit the button and document as backlog in the architectural PRD.

---

## Trigger 1: Team withdrawal

**RepairTrigger:** team withdrawal.

**Invocation**

- **Teams** table — row action **Withdraw** (warning-styled icon or menu item). See [`teams.md`](teams.md).
- Opens [`WithdrawTeamDialog`](../ui/dialogs.md#withdrawteamdialog).

**Dialog flow (`WithdrawTeamDialog`)**

- Confirms the **TournamentTeam** (team name + division).
- **Effective after** — optional: "after match M" vs "immediately" (`effective_after_match_id` in schema).
- **Reason** — short text (`withdrawal_reason`).
- **Preview** (preferred): shows impacted upcoming matches and bracket slots before commit.
- **Save** creates a **RepairJob** with trigger team withdrawal, applies `tournament_teams` withdrawal fields, then enqueues dependent **RepairActions** (forfeit or remove upcoming matches, advance alternates, reflow schedule per product rules).

**Post-apply:** snackbar success; optional deep link to this job in `/repairs`.

---

## Trigger 2: Court outage

**RepairTrigger:** court outage.

**Invocation**

- Primary: **Court Status** dashboard — **Mark court unavailable** (or equivalent) on a court card. See [`court-status.md`](court-status.md).
- Opens [`CourtOutageDialog`](../ui/dialogs.md#courtoutagedialog).

**Dialog flow (`CourtOutageDialog`)**

- **Court** — pre-filled from context.
- **Starts at** / **Ends at** — local time in tournament zone; maps to `court_outages`.
- **Reason** — optional text.
- **Preview:** list of affected **MatchAssignments** on that court in the window; proposed reassignment targets or time shifts.
- **Save** creates a **RepairJob**; **RepairActions** move or reschedule assignments (never destroys **Match** identity — see [`schedule.md`](schedule.md)).

**Relationship to `DelayCourtDialog`:** [`DelayCourtDialog`](../ui/dialogs.md#delaycourtdialog) is a narrow, already-shipped operation ("push slots forward on one court"). A full **court outage** repair may span venues, multiple courts, or bounded time windows and is modeled as a **RepairJob** with richer preview.

---

## Trigger 3: Referee reassignment (WorkTeam / R1 / R2)

**RepairTrigger:** `work_team_reassignment` (the schema trigger name, even though the UI verb is "Change referee" — the affordance covers all three roles).

**Invocation**

- **Schedule** grid or **Matches** list — per-match action **Change referee**.
- **Division detail** Work rotation panel ([`work-teams.md`](work-teams.md#surface-1--division-detail-work-rotation-panel)) — per-row WorkTeam change.
- Opens [`RefereeReassignmentDialog`](../ui/dialogs.md#refereereassignmentdialog).

**Dialog flow (`RefereeReassignmentDialog`)**

- **Match** — context from row.
- **New assignment** — choose **WorkTeam**, **R1**, and optional **R2** per [`../data-model.md`](../data-model.md#8-match-generation) (`match_officials`). See [`work-teams.md`](work-teams.md) for the editor surfaces.
- **Save** creates a **RepairJob** with `trigger_type = 'work_team_reassignment'` and writes one or more `match_official_overrides` audit rows.

---

## Trigger 4: Schedule reflow

**RepairTrigger:** `schedule_reflow`.

**Invocation**

- **Court Status & Delay Dashboard** — per-court **Reflow preview** action ([`court-status.md`](court-status.md#reflow-preview-drawer)).
- **Schedule** grid — Move dialog opened against a `published` `ScheduleVersion` (the fork-on-mutate path opens the reflow drawer automatically when the impact is multi-match).

**Drawer flow ([Reflow preview drawer](court-status.md#reflow-preview-drawer))**

- **Slide minutes** (or whatever proposal form is appropriate for the source trigger).
- **Run preview** — dry-runs the cascade against the loaded `ScheduleVersion`. See [`../data-model.md` §11 Reflow preview](../data-model.md#reflow-preview).
- **Apply** — creates a **RepairJob** with `trigger_type = 'schedule_reflow'`, writes one or more `repair_actions` rows (mostly `match_assignment_move` / `work_team_reassignment`), and forks a new `draft` schedule version when the loaded version is `published`.

The reflow drawer is also reused inside [`CourtOutageDialog`](../ui/dialogs.md#courtoutagedialog) and [`WithdrawTeamDialog`](../ui/dialogs.md#withdrawteamdialog) (their **Preview** step is the same drawer with a pre-set proposal payload), but the resulting RepairJob's `trigger_type` is `court_outage` or `team_withdrawal` respectively — `schedule_reflow` is reserved for organizer-initiated cascades that aren't tied to a specific outage or withdrawal.

---

## Trigger 5: Pool court swap

**RepairTrigger:** `pool_court_swap`.

Distinct from `court_outage`: an outage scatters a court's matches to whatever's free; a pool court swap **pins** the moved matches to specific replacement courts and updates the pool's structural `pool_court_allocations` so future regenerates use the new courts too. The trigger an organizer reaches for when they're saying "Pool A is moving to Court 5 starting now" rather than "Court 3 is gone".

**Invocation**

- **Division detail** Step 1 — editing the **Courts strip** on a Pool card when the parent Stage status is `seeded`, `in_progress`, or `completed` *and* `match_assignments` exist for that pool (i.e. the schedule has been generated). Editing in `draft` or `configured` (or `seeded` with no schedule yet generated) writes `pool_court_allocations` directly without a RepairJob — the structural-default path.
- Opens [`PoolCourtSwapDialog`](../ui/dialogs.md#poolcourtswapdialog).

**Dialog flow (`PoolCourtSwapDialog`)**

- **Pool summary** (read-only): pool name, division, current court set.
- **Target courts** multi-select — every court in the tournament. At least one required.
- **Effective from match #** numeric (optional, defaults to the first `not_started` match in the pool). Earlier matches in the pool are unaffected; later matches move.
- **Reason** text field (optional, multi-line).
- **Preview** primary outlined button → opens the [Reflow preview drawer](court-status.md#reflow-preview-drawer) seeded with the pool-court-swap proposal. Mandatory when the loaded `ScheduleVersion` is `published`.
- **Apply** primary filled button — calls `POST /pools/{id}/court-swap:apply` (with the `apply_token` returned by the preview). Server creates a **RepairJob** with `trigger_type = 'pool_court_swap'`, writes `repair_actions` rows for each moved `match_assignment` and for the `pool_court_allocations` diff, forks a new `draft` schedule version when needed, and snackbars the result.

**Edge cases:**
- Matches in the pool that are `in_progress` or `final` stay put; the preview lists them as "Not moved (already started / final)".
- If a target court has a conflict at the original scheduled time, the scheduler queues that match at the next available slot on a target court; the preview's Affected matches table shows `Time (old → new)`.
- If the target court set is identical to the current set, the **Apply** button is disabled with the caption "No change to apply."

---

## Interactions (cross-cutting)

- All triggers prefer **preview → apply** when more than N matches or assignments change (N is a product constant; start with 1 if unsure).
- Errors surface via snackbar; failed jobs leave **RepairJob** in `failed` with diagnostics stored on the job or last action.
- Domain events such as `TournamentTeamWithdrawn` and `RemainingMatchesReassigned` align with [`../data-model.md`](../data-model.md#domain-events-and-integration-hooks).

## Dialogs

- [`WithdrawTeamDialog`](../ui/dialogs.md#withdrawteamdialog)
- [`CourtOutageDialog`](../ui/dialogs.md#courtoutagedialog)
- [`RefereeReassignmentDialog`](../ui/dialogs.md#refereereassignmentdialog)
- [`DelayCourtDialog`](../ui/dialogs.md#delaycourtdialog) — operational shortcut; see Trigger 2 above.

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md), [`../ui/dialogs.md`](../ui/dialogs.md).
- Data: [`../data-model.md`](../data-model.md#11-repair-and-live-adjustment).
- Architecture: [`../architectural-prd.md`](../architectural-prd.md) (RepairJob lifecycle in domain).
- Sibling features: [`teams.md`](teams.md), [`court-status.md`](court-status.md), [`schedule.md`](schedule.md), [`matches.md`](matches.md).

## Gap from current V1 implementation

The **`/repairs`** route ships with a repair-job history table and `POST .../repairs/team-withdrawal` / `court-outage` APIs. **`WithdrawTeamDialog`** and **`CourtOutageDialog`** replace GUID prompts on that page. Preview/apply reflow, `RefereeReassignmentDialog`, and full assignment shifting on outage are not yet implemented. [`DelayCourtDialog`](../ui/dialogs.md#delaycourtdialog) exists and calls `POST /courts/{id}:delay` — a partial shortcut toward court-level disruption but not the full outage repair workflow above. Work advances toward the target model in [`data-model.md`](../data-model.md#11-repair-and-live-adjustment).
