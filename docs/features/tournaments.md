# Tournaments

## Purpose

Manage the top-level events in CourtFlow. Admins create tournaments, edit metadata (dates, time zone, default match duration), and add the venues and courts the tournament will use.

## Routes

| Route | Page | Notes |
| --- | --- | --- |
| `/tournaments` | Tournaments table | Default landing after sign-in |
| `/tournaments/new` | Tournament editor (create) | Same page as edit, with `Id` null |
| `/tournaments/{Id:guid}` | Tournament editor (edit) | Includes Venues & Courts panel |

## Page header / toolbar

### Tournaments list

- Page heading "Tournaments" (`Typo.h4`).
- Right-aligned **Add** button (`CfToolbarAddButton`, aria-label "Add tournament"). Click navigates to `/tournaments/new`.

### Tournament editor

- Back icon button (`ArrowBack`) — navigates to `/tournaments`.
- Title (`Typo.h4`) — "New tournament" when `Id` is null, otherwise the tournament's name.
- **Publish status strip** (visible only after the tournament exists) — a dense outlined `MudPaper` directly under the title row containing:
  - **Status chip** showing current `tournaments.status` (`Draft` / `Published` / `Archived`).
  - **Public link preview** — when `Published`: the absolute URL `/events/{id}` with a copy-to-clipboard icon. When `Draft`: caption "Not visible publicly. Publish to make this tournament discoverable on the public landing." When `Archived`: caption "Visible only as a past event."
  - **Last published / archived caption** — `published_at` and `archived_at` from `tournaments` (relative date + actor when wired).
  - **Primary action button** (rightmost):
    - `Draft` → **Publish to public** (primary filled, `Public` icon). Opens a confirm modal "Make this tournament visible to the public? It will appear on the public landing page immediately. You can move it back to draft later." On confirm calls `POST /tournaments/{id}:publish`; status flips to `published`.
    - `Published` → **Archive tournament** (warning, outlined). Confirms "Archive this tournament? It will move from the active landing to a Past Events section. Shared links remain reachable." Calls `POST /tournaments/{id}:archive`.
    - `Archived` → **Un-archive** (primary outlined). Restores to `published`. Calls `POST /tournaments/{id}:unarchive`.
  - **Overflow menu** on the strip:
    - When `Published`: **Move to draft** (destructive). Opens a strong confirm modal "This will remove the tournament from the public landing and break shared `/events/{id}` links. The schedule, brackets, and standings will become inaccessible to the public. Continue?". On confirm calls `POST /tournaments/{id}:unpublish`.
    - When `Archived`: **Move to draft** with the same warning copy (rare path; used to take a past event off the public surface entirely while continuing to edit it).

## Page body

### Tournaments list

`MudDataGrid<TournamentDto>` (dense, hover, not striped) with columns:

- **Name** (`Property: Name`).
- **Visibility** chip — `Draft` / `Published` / `Archived` per `tournaments.status` (see [`../data-model.md`](../data-model.md#tournament-status-lifecycle)). Default chip colors: `Draft` = Default (neutral), `Published` = Success, `Archived` = Info. Sortable.
- **Start** (`Property: StartDate`, default formatting).
- **End** (`Property: EndDate`).
- **Time zone** (custom template that resolves the IANA id to a human label via `TimeZoneOptions.Label`).
- **Match (min)** (`Property: DefaultMatchDurationMinutes`).
- **Actions:** edit icon button (navigates to `/tournaments/{Id}`); **Clone** icon button (`ContentCopy` icon, aria-label "Clone tournament") — opens the [`CloneTournamentDialog`](../ui/dialogs.md#clonetournamentdialog); red delete icon button. Delete is disabled when `status = 'published'` to prevent accidental destruction of a live public surface; archive first.

Loading state: data grid's built-in indeterminate row.

### Tournament editor

Body is a vertical stack (4-spacing) of two sections.

#### Section A — Details (always visible)

`MudPaper` elevation 1, padding 5, with stacked form (3-spacing):

- Section heading "Details" (`h6`).
- **Name** text field (required).
- **Description** text field (multi-line, 2 rows).
- Row of two date pickers: **Start date** and **End date**.
- Row of two fields:
  - **Time zone** select bound to `TimeZoneOptions.All` (PST, MST, MST AZ, CST, EST, AKST, HST, UTC). Outlined, dense, dense margin, sized class `cf-select--lg`.
  - **Default match duration (min)** numeric field, range 15–240.
- Right-aligned **Save details** button (primary filled, `Save` icon) — disabled while saving.

#### Section B — Venues & courts (visible only after the tournament exists)

`MudPaper` elevation 1, padding 5, with stacked layout:

- Header row: section title "Venues & courts" (`h6`) and `CfToolbarAddButton` (aria "Add venue"). Click opens the **Add venue** dialog.
- Loading state: indeterminate progress bar.
- Empty state: `MudAlert Info` "No venues yet. Use Add above."
- Otherwise, `MudExpansionPanels` (multi-expansion). One panel per venue:
  - **Title content:**
    - Venue name (`subtitle1`), optional address (`caption`), and "(N) court(s)" caption.
    - Right-aligned action cluster (with `@onclick:stopPropagation`) holding **Edit venue** and **Delete venue** icon buttons.
  - **Child content:**
    - Either "No courts yet." caption (secondary text) or a dense `MudList` of courts. Each court row shows `SportsVolleyball` icon, court name, and a small red delete icon button.
    - Inline "New court name" text field with an `Add` button (`CfToolbarAddButton`, aria "Add court"). Pressing **Enter** in the field also adds the court. Names are de-duplicated case-insensitively per venue.

If the user is on `/tournaments/new` and hasn't saved yet, replaces section B with `MudAlert Info`: "Save tournament details to manage venues and courts."

#### Section C — Waves (visible only after the tournament exists)

`MudPaper` elevation 1, padding 5, with stacked layout:

- Header row: section title "Waves" (`h6`) and `CfToolbarAddButton` (aria "Add wave"). Click opens the [`WaveDialog`](../ui/dialogs.md#wavedialog).
- Caption beneath the header: "Sessions inside a tournament day (AM / PM / 1 / 2). Divisions are pinned to a wave on the division editor; CSV imports resolve the `Wave` column by code."
- Loading state: indeterminate progress bar.
- Empty state: `MudAlert Info` "No waves yet. Use Add above. Most tournaments use AM and PM."
- Otherwise, a dense `MudTable` of waves with columns:
  - **Code** (`AM` / `PM` / `1` / `2`).
  - **Default start** / **Default end** (formatted local-time; em-dash when unset).
  - **Divisions using this wave** (count, derived from `tournament_divisions.wave_id = wave.id`).
  - **Actions:** Edit icon → reopens [`WaveDialog`](../ui/dialogs.md#wavedialog) seeded; Delete icon → confirmation message box. Delete is disabled when any division references the wave.
- Reorder: each row has a drag handle that updates `waves.display_order`.

If the user is on `/tournaments/new` and hasn't saved yet, replaces section C with `MudAlert Info`: "Save tournament details to manage waves."

## Interactions

### Tournaments list

- Edit icon → `/tournaments/{Id}`.
- **Clone icon** → opens [`CloneTournamentDialog`](../ui/dialogs.md#clonetournamentdialog). On save, the server captures the source tournament to a `tournament_templates` row, instantiates a new `draft` **Tournament** with the structural subset (divisions, stages, pools, brackets, advancement rules, waves, tiebreaker presets — see [`../data-model.md` §12 Tournament templates](../data-model.md#12-tournament-templates) for the precise capture/instantiation scope), and navigates to `/tournaments/{newId}`. Snackbar "Tournament cloned from {source name}. Teams, rankings, schedule, and scores were not copied."
- Delete icon → opens "Delete tournament" message box. On confirm, calls `DELETE /tournaments/{id}`, snackbar "Deleted", then reload.
- API errors are surfaced via `Snackbar.Add(message, Severity.Error)`.

### Tournament editor

- **Save details:** validates name; on create calls `POST /tournaments` then navigates to `/tournaments/{newId}`. On edit calls `PUT /tournaments/{id}`.
- **Add venue / Edit venue:** opens [`VenueDialog`](../ui/dialogs.md#venuedialog). On close (not canceled) reloads venues and courts.
- **Delete venue:** confirmation message box, then `DELETE /venues/{id}`.
- **Add court:** validates non-empty name and case-insensitive uniqueness; calls `POST /courts`. Pressing Enter in the inline field also submits.
- **Delete court:** confirmation message box, then `DELETE /courts/{id}`.
- **Add wave / Edit wave:** opens [`WaveDialog`](../ui/dialogs.md#wavedialog). On close reloads waves.
- **Delete wave:** confirmation message box, then `DELETE /waves/{id}`. Disabled when any division references it.
- **Reorder waves:** drag handle → `PATCH /waves/{id}` with new `display_order`.
- **Publish to public:** confirmation modal → `POST /tournaments/{id}:publish` → snackbar "Tournament published". Status chip flips to `Published` and the public link preview appears.
- **Archive tournament:** confirmation modal → `POST /tournaments/{id}:archive` → snackbar "Tournament archived". Status chip flips to `Archived`.
- **Un-archive:** `POST /tournaments/{id}:unarchive` → snackbar "Tournament restored to published".
- **Move to draft:** strong confirmation modal → `POST /tournaments/{id}:unpublish` → snackbar "Moved to draft. The tournament is no longer publicly visible."
- After every venue / court / wave / status mutation the page reloads the relevant list to stay consistent.
- If `GET /tournaments/{id}` fails, shows inline `MudAlert Error` "Tournament not found."

## Dialogs

- [`VenueDialog`](../ui/dialogs.md#venuedialog) — add/edit venue (with inline staged courts).
- [`WaveDialog`](../ui/dialogs.md#wavedialog) — add/edit wave (code + optional default time window).
- [`CloneTournamentDialog`](../ui/dialogs.md#clonetournamentdialog) — name + date pickers for the new tournament; previews what is and isn't copied.
- Confirmation message boxes for delete tournament, delete venue, delete court, delete wave, **Publish to public**, **Archive tournament**, **Un-archive**, **Move to draft** — see [`../ui/dialogs.md`](../ui/dialogs.md#confirmation-message-boxes). The Move-to-draft modal uses strong destructive copy because shared `/events/{id}` links break when the tournament leaves `published`.

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md), [`../ui/dialogs.md`](../ui/dialogs.md).
- Data: [`../data-model.md`](../data-model.md#2-tournament-creation) (Tournament aggregate, Venues, Courts, CourtAvailabilityWindow, Waves), [`../data-model.md`](../data-model.md#tournament-status-lifecycle) (Tournament status & public-visibility gate), [`../data-model.md`](../data-model.md#12-tournament-templates) (clone via template).
- Public surface: [`public-landing.md`](public-landing.md), [`public-event.md`](public-event.md) (what `published` and `archived` tournaments look like to the public).
- Architecture: [`../architectural-prd.md`](../architectural-prd.md) §4.1 (aggregate roots).
- Glossary: [`../../CONTEXT.md`](../../CONTEXT.md) — **Tournament template**.
