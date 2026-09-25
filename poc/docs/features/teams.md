# Teams

> **Term note:** On this page, "Team" is shorthand for **TournamentTeam** — the team's entry in the currently selected tournament, not the master `teams` record. See [`../../CONTEXT.md`](../../CONTEXT.md) for the full distinction.
>
> **Surface gap (V1 implementation):** **Ranking**, **Seed**, **Standing**, **FinishPosition**, and **BracketSeed** are real domain concepts (see [`../../CONTEXT.md`](../../CONTEXT.md) and [`../data-model.md`](../data-model.md#5-ranking-and-seeding)) but are intentionally not surfaced on this page in V1. **Ranking** is the **single source of truth** for seeding input and is managed on the [**Division detail**](division-detail.md) workspace's persistent Rankings panel (`/divisions/{Id}`); pool / bracket structure also lives there. **Standing** / **FinishPosition** / tiebreakers surface on [`standings.md`](standings.md); **BracketSeed** is visible in the Brackets viewer ([`brackets.md`](brackets.md)). The deprecated `tournament_teams.seed` column (V0 "SeedHint") is removed from the target schema — see the **EntrySeed (deprecated)** entry in [`../../CONTEXT.md`](../../CONTEXT.md).

## Purpose

Let admins manage tournament-team entries: assign them to divisions, edit their names and clubs, and find a specific team quickly via filters.

## Routes

| Route | Page |
| --- | --- |
| `/teams` | Teams table with filter strip |

## Page header / toolbar

- Page heading "Teams" (`Typo.h4`).
- Right-aligned action cluster:
  - **Refresh** icon button (tooltip "Reload teams from server"). Disabled while loading or when no tournament is selected.
  - **Add** (`CfToolbarAddButton`, aria-label "Add team"). Disabled until at least one division exists.

## Page body

### Filter strip

`MudGrid` (2 spacing, three responsive items at xs/sm/md):

- **Division** select (clearable, "All divisions" placeholder, dense, outlined). Bound to the divisions for the selected tournament; selecting filters the table.
- **Team** text field (immediate, single-line, clearable). Performs a case-insensitive substring match on team name.
- **Club** autocomplete (clearable, lenient/non-strict, max 25 items, placeholder "Type to search clubs"). Searches across the distinct list of clubs found in the loaded teams.

### Table

`MudDataGrid<TeamDto>` (dense, hover) showing the filtered team list with columns:

- **Division** (template — name resolved from the divisions list, "-" if missing).
- **Team** (`Property: Name`).
- **Club** (`Property: Club`).
- **Actions:** edit, red delete, and **Withdraw** (warning-style icon or overflow menu — opens [`WithdrawTeamDialog`](../ui/dialogs.md#withdrawteamdialog); see [`repairs.md`](repairs.md)).

## Interactions

- On tournament change the page reloads divisions, then teams. Filters reset to defaults.
- Refresh icon button forces a re-fetch from the API.
- Add → opens [`TeamDialog`](../ui/dialogs.md#teamdialog) with the current division filter pre-selected (or the first division). Result is upserted into the local list and re-sorted by name (no full reload).
- Edit → opens [`TeamDialog`](../ui/dialogs.md#teamdialog) seeded with the row.
- Withdraw → opens [`WithdrawTeamDialog`](../ui/dialogs.md#withdrawteamdialog). Completes a **team withdrawal** repair (see [`repairs.md`](repairs.md)); target API creates a **RepairJob** rather than a silent delete.
- Delete → confirmation message box ("Delete '{name}'?"). On success, `DELETE /teams/{id}` then removes the row from the local list.
- All errors surface via snackbar.

### Adding a team after the division's pool stage is seeded

The `TeamDialog` Save behavior is conditional on the target Division's pool **Stage** status:

- **`draft` / `configured`** — no special handling. The new `TournamentTeam` is created and will be included the next time the wizard's **Snake Rankings into Pools** materialize action runs.
- **`seeded`** (structure committed, matches generated, schedule may or may not be published) — Save shows an inline warning and routes through a two-step confirmation:
  1. **Add to division only** *(default)* — creates the `TournamentTeam` row and appends it to `division_rankings` at the next unused rank, but **does not** auto-place it into any pool. The team appears in [`division-detail.md`](division-detail.md)'s Rankings panel as **Unplaced** with an inline call-to-action; the organizer either re-runs Snake (which the wizard explicitly warns will rewrite seeded pools and open a fresh schedule generation cycle) or manually drags the team into a pool slot.
  2. **Cancel** — closes the dialog.
- **`in_progress` / `completed`** — Save is disabled with an explanation tooltip; the supported path is to either roll the stage back to `seeded` from the Division detail header (see [`division-detail.md`](division-detail.md) Stage status chip) or, if the tournament is actively running, *not* add the team and treat them as a no-show for the current event.

The `TeamDialog` reads the relevant Stage status from the selected division's first pool stage and surfaces the routing as a small caption beneath the Division field so the user knows what they're about to do before they hit Save. See [`../data-model.md`](../data-model.md#5-ranking-and-seeding) for the underlying `division_rankings` write and [`division-detail.md`](division-detail.md) for the Rankings panel behavior.

## Dialogs

- [`TeamDialog`](../ui/dialogs.md#teamdialog) — new and edit, with division select and club autocomplete.
- [`WithdrawTeamDialog`](../ui/dialogs.md#withdrawteamdialog) — tournament-team withdrawal (repair trigger).
- Confirmation message box for delete — see [`../ui/dialogs.md`](../ui/dialogs.md#confirmation-message-boxes).

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md).
- Data: [`../data-model.md`](../data-model.md#4-team-import-and-event-entry) (`tournament_teams`, `clubs`, `teams`).
- Sibling features: [`divisions.md`](divisions.md), [`imports.md`](imports.md), [`repairs.md`](repairs.md).
