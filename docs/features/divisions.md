# Divisions

> **Scope:** This document describes only the **divisions table view**. The **division detail** structure-first workspace (Rankings panel + Pool Structure → Bracket Structure → Advancement Rules wizard, Snake / Resolve materialize actions) is specified in [`division-detail.md`](division-detail.md). Add / edit dialog fields are specified in [`DivisionDialog`](../ui/dialogs.md#divisiondialog).

## Purpose

Give admins a single, fast list of every division in the currently selected tournament, with row-level actions to delete (and to invoke add/edit/manage flows). Each division row's **Format** column hints at its default stage composition (pool only, pool + bracket, bracket only); the canonical structure is **Stage**-based — see [`../../CONTEXT.md`](../../CONTEXT.md) and [`division-detail.md`](division-detail.md).

## Routes

| Route | Page |
| --- | --- |
| `/divisions` | Divisions table |

## Page header / toolbar

- Page heading "Divisions" (`Typo.h4`).
- Right-aligned `CfToolbarAddButton` (aria-label "Add division"). **Disabled when no tournament is selected.** Opens [`DivisionDialog`](../ui/dialogs.md#divisiondialog) in new mode.

## Page body

`MudDataGrid<DivisionDto>` (dense, hover, single-selection) bound to the divisions for the currently selected tournament. Columns:

- **Name** (`Property: Name`).
- **Format** (`Property: Format` — values: Pool only, Pool + bracket, Bracket only).
- **Max teams** (`Property: MaxTeams`).
- **Actions** column with three icon buttons per row:
  - **Manage** (`Dashboard` icon, tooltip "Manage stages, pools & brackets") — navigates to [`division-detail.md`](division-detail.md) route `/divisions/{Id}`.
  - **Edit** (`Edit` icon, aria-label "Edit division") — opens [`DivisionDialog`](../ui/dialogs.md#divisiondialog) in edit mode, seeded with the row.
  - **Delete** (`Delete` icon, red, aria-label "Delete division") — opens a confirm dialog ("Delete '{name}'?", Delete / Cancel) then calls `DELETE /divisions/{id}` and reloads. Success snackbar "Deleted". Disabled when any of the division's stages is past `configured` (i.e. has placed teams or generated matches); tooltip "Withdraw teams or roll stages back before deleting."

### State and edge cases

- **Initial load:** ensures `SelectedTournamentStore` is initialized, then fetches divisions for the selected tournament.
- **Loading:** the data grid shows its loading row.
- **No tournament selected:** the grid is empty and the **Add** button is disabled.
- **API error:** surfaced via snackbar.

## Interactions

- Manage icon → navigates to `/divisions/{Id}` — see [`division-detail.md`](division-detail.md).
- Add toolbar button → opens [`DivisionDialog`](../ui/dialogs.md#divisiondialog) in new mode. On save the new division is appended to the table.
- Edit icon → opens [`DivisionDialog`](../ui/dialogs.md#divisiondialog) in edit mode seeded with the row. On save the row updates in place.
- Delete icon → confirmation message box → `DELETE /divisions/{id}` → reload list.

## Dialogs

- [`DivisionDialog`](../ui/dialogs.md#divisiondialog) — new and edit, with identity, format, scheduling, and tiebreaker sections.
- Confirmation message box for delete — see [`../ui/dialogs.md`](../ui/dialogs.md#confirmation-message-boxes).

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md) (tournament context propagation).
- Data: [`../data-model.md`](../data-model.md#3-division-creation) (Division aggregate, format/scheduling/seeding settings, status lifecycle, tiebreakers).
- Sibling features: [`teams.md`](teams.md), [`schedule.md`](schedule.md), [`brackets.md`](brackets.md), [`standings.md`](standings.md), [`division-detail.md`](division-detail.md).
