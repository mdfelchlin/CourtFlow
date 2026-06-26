# Court Status & Delay Dashboard

> **Status:** **Target — partial ship today.** [`DelayCourtDialog`](../ui/dialogs.md#delaycourtdialog) is the only piece wired to the API; the dashboard page, match clock affordances, both layouts, TV mode, and reflow preview are the V1.5 work this doc specifies.

## Purpose

Give admins a real-time, at-a-glance view of every court in the selected tournament: what's playing, when it was supposed to start, when it actually started, how far behind or ahead each court is running, and **what would happen** if they slide that court by N minutes or swap a match.

The dashboard is CourtFlow's headline differentiator from AES / SportWrench. It composes four pieces specced here:

1. **Match clock** — the explicit Start / Mark-complete affordance that writes `matches.started_at` and `matches.completed_at` so deltas are computed honestly.
2. **Two layouts** — **Cards view** (one card per court, action-dense, for the organizer walking the floor) and **Grid view** (court × time matrix, color-driven, for at-a-glance scanning across the whole gym). Both read the same data and share the same actions; the toggle is purely visual. See [Layouts](#layouts).
3. **TV mode** — full-screen Grid view designed to be projected on a wall monitor at the gym; no chrome, larger type, auto-refresh forced on. See [TV mode](#tv-mode).
4. **Reflow preview** — a dry-run of any proposed change (slide court by N, mark a court unavailable, swap a match) that returns the cascade diff and lets the organizer **Apply** when satisfied.

**Court outage repairs:** the dashboard remains the primary entry point for a **court outage** **RepairJob**. See [`repairs.md`](repairs.md) Trigger 2. `DelayCourtDialog` is the narrower already-shipped operation; the full outage flow adds [`CourtOutageDialog`](../ui/dialogs.md#courtoutagedialog) and the reflow preview below.

## Routes

| Route | Page | Notes |
| --- | --- | --- |
| `/court-status` | Court Status & Delay Dashboard | Target. Appears in the side `NavMenu` (`Dashboard` icon) once shipped. |

## Page header / toolbar

- Page heading "Court status" (`Typo.h4`).
- **Tournament clock chip** under the heading — a soft `MudChip` showing the current wall-clock time in the tournament's time zone (refreshes every 15 s). The dashboard's delta computations anchor to this clock.
- Right-aligned filter strip (in order):
  - **Layout toggle** — `MudToggleGroup` with two options: **Cards** (`ViewModule` icon) and **Grid** (`GridView` icon). Persisted per-user in `localStorage` under `courtflow.courtStatusLayout`. Default is **Grid** when any **Tournament** in the selected event has at least one **Stage** with `status = 'in_progress'`; otherwise default is **Cards**. The toggle is keyboard-accessible (left/right arrows) and announces the active layout via `aria-pressed`.
  - **TV mode** icon button (`Tv` icon) — visible only when **Grid** is the active layout. Click enters full-screen Grid view; see [TV mode](#tv-mode). Hidden in TV mode itself (exit via `Esc`).
  - **Venue** select (clearable, dense, outlined).
  - **Division** select (clearable, dense, outlined).
  - **Wave** select (clearable, dense, outlined).
  - **Auto-refresh** toggle icon button (`Loop` icon). When on, the dashboard re-polls every 30 s. Off by default in Cards view, on by default in Grid view, **forced on** in TV mode. The chip on the heading shows whether auto-refresh is active.
  - **Refresh now** icon button.

## Layouts

The dashboard ships with two layouts of the same data. The Layout toggle in the toolbar switches between them with no refetch.

| Concern | Cards view | Grid view |
| --- | --- | --- |
| Primary purpose | Take action on one court | Scan whole gym at once |
| Density | Medium — one card per court, full action row | High — single-line cells, color-driven |
| Color encoding | Status chip on card header | Cell background — read in one glance |
| Best for | Organizer walking the floor | Operations table / gym wall monitor / TV mode |
| Interactions | Buttons on each card | Click a cell → same action popover |
| Default in | New / pre-tournament state (no live stages) | Tournament-day (any stage `in_progress`) |

## Page body — Cards view

Responsive grid of court cards (`MudGrid`). Suggested breakpoints: `xs=12`, `sm=6`, `md=4`, `lg=3`.

One `MudCard` (outlined, full height) per court in the selected tournament, sorted by venue then court name. Each card contains:

### Card header row

- Court name (`h6`), venue caption.
- Status `MudChip`:
  - **Idle / Empty** — Default
  - **On Time** — Success
  - **Slightly Behind** (1–5 min behind on the *currently-playing or next* match) — Info
  - **Behind** (6–15 min behind) — Warning
  - **Significantly Behind** (> 15 min behind) — Error
  - **Out** — Error (court has an active `court_outages` window covering "now")
  - **Ahead** — Success-outline (started early, > 2 min)

The chip color is derived from the per-match delta computed in [Delay computation](#delay-computation) below — specifically the largest absolute delta across the court's `not_started` and `in_progress` matches.

### Card "Now playing" block

- Division, "Home vs Away", and an optional pool / bracket chip.
- **Match clock row:**
  - Scheduled start (caption).
  - Actual start (caption) — `matches.started_at` formatted as local time, or "—" when null and the match is `not_started`.
  - Prominent **delta chip** — `+12 min behind` / `−3 min ahead` / `On time`. Tooltip on hover explains the math.
- Falls back to "No match in progress" caption + the **next scheduled match** preview (smaller text, with its own scheduled-start chip) when the court is idle.

### Card actions row

- **Start match** primary outlined button (`PlayArrow` icon) — visible only when there is a "next" match on the court and either no match is currently in progress, or the in-progress match has `completed_at` already written. Writes `matches.started_at = now()` and transitions `match_status` to `in_progress` per [`../data-model.md` §8 Match clock writes](../data-model.md#match-clock-writes). Snackbar "Match started — Court 3 · Pool A · Team X vs Team Y".
- **Mark complete** outlined button (`Done` icon, dense) — visible only when a match is `in_progress` and the organizer wants to set `completed_at` without going through the Score dialog (e.g. forfeit, post-hoc entry). Opens a confirmation message box: "Mark this match complete without scoring? You can still score it later." On confirm writes `completed_at = now()` and transitions `match_status` to `final`.
- **Reflow preview** outlined button (`Schedule` icon) — opens the [Reflow preview drawer](#reflow-preview-drawer) seeded with "slide this court by N minutes". Replaces the V1 `DelayCourtDialog` long-term; until the drawer ships the dashboard keeps `DelayCourtDialog` as a fallback for single-court slides.
- **Mark court unavailable** outlined button (warning, `EventBusy` icon) — opens [`CourtOutageDialog`](../ui/dialogs.md#courtoutagedialog); creates a **court outage** **RepairJob** with reflow preview embedded inline in the dialog. See [`repairs.md`](repairs.md#trigger-2-court-outage).
- **View court detail** text button — navigates to a per-court detail surface (out of scope for this document) that lists the court's full upcoming and historical match schedule with timestamps.

## Page body — Grid view

A **court × time** matrix designed to be read in one glance. Same data as Cards view; different layout and color encoding.

### Matrix axes

- **Columns:** one column per court in the selected tournament, in venue → court-name order. Header row is sticky on vertical scroll and shows: court name (`subtitle2`), venue caption, and the same status chip used on the Cards view header (Idle / On Time / Slightly Behind / Behind / Significantly Behind / Out / Ahead). The chip on the header reflects the court's worst current delta — the eye anchor for "which courts are hot right now".
- **Rows:** one row per distinct scheduled start time on the loaded `ScheduleVersion`, in ascending order. The leftmost **Time** column is sticky on horizontal scroll. Time format: `h:mm tt` in the tournament's time zone; for multi-day tournaments rows are grouped under a sticky day header (`ddd MMM d`).
- **Wave bands:** row backgrounds are tokenized — AM waves use `--cf-wave-band-am` (info-derived cool tint), PM waves use `--cf-wave-band-pm` (secondary-derived warm tint). Both resolve to ~3–5% opacity so they don't fight the per-cell color encoding. See [`../ui/design-system.md` §2 → Semantic surface tints](../ui/design-system.md#semantic-surface-tints).

### Cell content

Each cell renders either an **em-dash** (no match assigned to this court × time) or a **match cell**:

- Top row: division chip + small pool/bracket chip when relevant. Chip color is a **derived UI concern** — a deterministic hash of `tournament_divisions.id` selects a swatch from the design system's division palette (8-color rotation). No `color_token` column exists on the data model; persisting per-division colors would be premature config and the deterministic hash gives every division a stable color across sessions and devices without round-tripping. See [`../ui/design-system.md`](../ui/design-system.md#division-color-derivation).
- Middle row: `Home vs Away` team names, single line, ellipsis on overflow. Both team names rendered in 13pt; the home team is left-aligned and the away team right-aligned around a centered `vs`.
- Bottom row: **clock delta chip** (`+12 min behind` / `Live` / `Final` / `−3 min ahead`) on the left + **WorkTeam** caption on the right (`Ref: TeamName`, truncated to first 18 chars).

The full team names + WorkTeam + R1 / R2 + scheduled-vs-actual times appear on hover via a tooltip; the cell content is purposefully compact.

### Cell background color (the primary encoding)

The cell's background is the **single highest-leverage signal** in this layout. Picked from the per-match delta (see [Delay computation](#delay-computation)) and the `match_status`. Every background references a token from [`../ui/design-system.md` §2 → Semantic surface tints](../ui/design-system.md#semantic-surface-tints) — components never compute color literals inline, so TV mode and print stylesheets can override the rendering by redefining the same tokens.

| Status / delta | Background token | Foreground |
| --- | --- | --- |
| No match (em-dash) | `--cf-surface-0` | — |
| Future, on schedule (`not_started`, scheduled in the future) | `--cf-surface-1` | Default text |
| Future, court is behind & next match likely to slip (`not_started`, scheduled in past, delta 1–10 min) | `--cf-tint-warning-pale` | Default text |
| Live, on time (`in_progress`, delta ≤ 1 min) | `--cf-tint-success-pale` | Bold text |
| Live, 2–10 min behind (`in_progress`, delta 2–10) | `--cf-tint-warning-pale` | Bold text |
| Live, 11–20 min behind | `--cf-tint-warning-solid` | Bold light text |
| Live, > 20 min behind | `--cf-tint-error-solid` | Bold light text |
| Live, ≥ 2 min ahead | `--cf-tint-info-pale` | Bold text |
| Final (`final`) | `--cf-tint-success-solid` at `opacity: 0.9` | Slightly muted text |
| Canceled / forfeited | `--cf-stripe-canceled` (diagonal repeating gradient) | Muted text |
| Court outage covers this row (active `court_outages`) | `--cf-stripe-outage` (diagonal gray gradient) on every cell in that court column for the outage window | "Out" caption inside the striped band |

Wave-band row backgrounds use `--cf-wave-band-am` and `--cf-wave-band-pm` from the same token catalog. Division and pool/bracket chips on the cell read their color from the `divisionColor(divisionId)` helper, which selects from `--cf-division-1` … `--cf-division-8` per [Division color derivation](../ui/design-system.md#division-color-derivation).

The status chip on the court column header reflects the **worst** non-final delta in the column. The chip on the row header (when shown) reflects the worst delta across all courts at that time slot.

Cells **never** rely on color alone — every cell has a text status (`+12 min behind`, `Live`, `Final`, etc.) and a chip on the team-name row, so the layout meets the accessibility floor documented in [`../ui/design-system.md`](../ui/design-system.md).

### Now line

A horizontal accent rule spans the matrix at the row matching `now()` (placed between rows when no slot is exactly on the hour). Labeled "Now · h:mm tt" in a sticky chip anchored to the left of the rule. Repositioned every 30 s by the auto-refresh tick.

### Interactions

- **Click a cell:** opens a popover anchored to the cell with the same five actions as the Card actions row — **Start match**, **Mark complete**, **Score**, **Move**, **Change referee**, plus **Reflow preview** seeded with "slide this court by N minutes". The popover closes on outside click or `Esc`.
- **Click a court column header:** opens the same Reflow-preview drawer (seeded with the court's current delta) without going through a cell — the per-court entry point for "Court 3 is 20 min behind, let me slide it".
- **Hover a cell:** tooltip shows division · pool/bracket · home + away (full) · WorkTeam · R1 · R2 · scheduled start · actual start · delta. Tooltip dismisses when the popover is open.
- **Keyboard navigation:** arrow keys move the focused cell; **Enter** opens the popover; **Esc** closes it. Court column headers and row time labels are focusable via Tab.
- **Scroll behavior:** the body scrolls in both axes. The court header row is `position: sticky; top: 0` and the time column is `position: sticky; left: 0` so the eye always knows what court / time the focused cell belongs to.

## TV mode

A full-screen variant of the Grid view designed to project on a wall monitor or large TV at the gym lobby / scorer's table. Activated from the **TV mode** toolbar icon button.

### What TV mode changes vs normal Grid view

- **Chrome hidden:** app bar, drawer, breadcrumbs, page heading, the tournament selector, the toolbar itself — all hidden. The page becomes the matrix plus a slim top strip.
- **Top strip (replaces the page header):** tournament name + organization name (left), current local clock (center, `h:mm:ss tt`, refreshes every 1 s), exit-TV-mode icon button (right, `FullscreenExit` icon). The strip is 56 px tall, dark surface, light text.
- **Type sizes:** all font sizes bump by ~30% so the matrix is readable from across the gym. Team names go from 13pt to 17pt; chips from 11pt to 13pt; the Now-line label from 11pt to 14pt.
- **Auto-refresh:** **forced on at 30 s**, regardless of the user's toolbar preference. The auto-refresh chip is hidden — the cells just update.
- **Now line:** rendered with a thicker accent and a brighter background so it reads from 20 ft away.
- **Color saturation:** TV mode sets `data-cf-tv-mode="true"` on the document root, which redefines `--cf-tint-warning-solid` and `--cf-tint-error-solid` to higher saturation (`55% → 65%` and `65% → 75%` respectively) so the "Live · 11–20 min behind" amber and "Live · > 20 min behind" red read from across the gym. No component-level overrides — the token swap is the entire change. See [`../ui/design-system.md` §2 → Semantic surface tints](../ui/design-system.md#semantic-surface-tints).
- **Interactions:** click and hover work but are not the design target — TV mode is read-mostly. Cells stay clickable so an organizer at the table can take action without exiting TV mode; the popover renders at a larger size with bigger touch targets.
- **No scroll persistence:** TV mode resets scroll to "anchored on the Now line" on every refresh, so the projected display never drifts away from the present.

### Entering and exiting

- Enter: click the **TV mode** icon button in the toolbar. The browser is asked to enter the Fullscreen API (`document.documentElement.requestFullscreen()`); if the user dismisses the browser permission, TV mode still renders but inside the tab (without the OS-level fullscreen).
- Exit: press `Esc` (also leaves the Fullscreen API), or click the **Exit TV mode** icon in the top strip. The dashboard returns to the previous layout and the user's toolbar prefs.
- Reload-safe: TV mode is sticky for the current browser session via `sessionStorage` under `courtflow.courtStatusTvMode = 'true'`, so an accidental refresh on a kiosk doesn't drop back to the chrome'd view. Cleared on explicit exit and on tab close.

### Recommended kiosk setup (operations note, not a CourtFlow feature)

For organizations running TV mode on a dedicated kiosk:

- Sign in once as the org admin and select the tournament.
- Open `/court-status`, switch to Grid, enter TV mode.
- Set the OS / browser to inhibit sleep + auto-launch the URL on boot.

CourtFlow does not yet ship a token-based "kiosk session" auth flow — that's a future enhancement. Until then a dedicated browser profile signed in as an admin is the supported path.

## Delay computation

The chip color and per-match delta come from `matches.started_at` (or `now()` for `not_started` matches that are already past their `scheduled_start`):

```
For a match m on court C with assignment a:
  if m.started_at IS NOT NULL:
    delta_minutes(m) = (m.started_at − a.scheduled_start) / 60
  else if a.scheduled_start < now() AND m.status = 'not_started':
    delta_minutes(m) = (now() − a.scheduled_start) / 60         // running behind, not started
  else:
    delta_minutes(m) = NULL                                        // future on schedule — no delay signal yet
```

**NULL vs zero:** `NULL` means the **Match** has no meaningful delay contribution yet. Implementations **must not** coerce NULL to `0` for aggregation. Matches with NULL delta are **omitted** from the court-level "largest **positive** delta" chip; they still use on-schedule styling (`--cf-surface-1` in the Grid view). Canonical formula and **MatchClockDelaySignal** read model: [`../data-model.md` §8 · Match clock writes](../data-model.md#match-clock-writes), [`../adr/0006-read-models-tiebreaker-delay-public-division-readiness.md`](../adr/0006-read-models-tiebreaker-delay-public-division-readiness.md).

The card-level delta is the **largest positive value** across the court's currently-playing and next-up `not_started` matches (so a card stays "Behind" even when the playing match is on time but the next one is already running late). The Math is also documented in [`../data-model.md` §8 Match clock writes](../data-model.md#match-clock-writes).

When a court has an active `court_outages` row covering `now()`, the card chip is **Out** regardless of other deltas, and the action row hides everything except **Mark court unavailable** (relabeled **Edit outage**) and **View court detail**.

## Reflow preview drawer

A right-side `MudDrawer` (`Anchor.End`, width 480 px) that opens from the **Reflow preview** action on any court card, from the schedule grid's Move dialog when the loaded `ScheduleVersion` is `published`, and from the **Preview** step of [`CourtOutageDialog`](../ui/dialogs.md#courtoutagedialog) / [`WithdrawTeamDialog`](../ui/dialogs.md#withdrawteamdialog).

### Drawer header

- Drawer title — varies by trigger: "Slide Court 3 by N minutes", "Court 3 unavailable 2:00–4:00 PM", "Withdraw team Spike Crush 14U Black", etc.
- Read-only **Loaded schedule version** chip showing the version the preview ran against (e.g. "Published · v3"). Forms the apply token described in [`../data-model.md` §11 Reflow preview](../data-model.md#reflow-preview).

### Drawer body

1. **Proposal form** — inputs that parameterize the preview. For court slide: a single **Slide minutes** numeric field (range −60 to +180, default +N pre-filled from the originating card's delta). For court outage / withdrawal triggers the form is whatever the source dialog passed through.
2. **Run preview** primary outlined button (`PlayCircleOutline` icon). Calls `POST /schedule-versions/{loadedId}:preview` with the proposal payload. Shows an indeterminate progress bar while running.
3. **Diff panel** (rendered once the preview returns):
   - **Summary row:** the server-provided one-line description ("12 assignments shifted; 1 WorkTeam reassigned; no new conflicts"). Severity chip mirrors the diagnostics:
     - Green ("Safe") when the post-apply conflict count is 0.
     - Amber ("Warnings") when the post-apply count of *new* warnings is non-zero.
     - Red ("Conflicts") when post-apply hard conflicts exist.
   - **Affected matches** dense `MudTable` with columns: Court, Time (old → new), Match (Home vs Away), Status, **Change** chip (Move / Cancel / WorkTeam / Forfeit). Sortable by Court and Time.
   - **New conflicts** collapsible section listing any conflicts the apply would introduce (court reuse, ref overlap, wave violation, team rest-time). Same shape as the schedule grid's conflicts banner.
4. **Footer actions:**
   - **Cancel** text button — closes the drawer; nothing is written.
   - **Apply** primary filled button (`CloudUpload` icon, warning color when there are new warnings, error color when there are hard conflicts) — calls `POST /schedule-versions/{loadedId}:apply-preview` with the `apply_token`. On success closes the drawer, snackbars "Reflow applied — see Repair Jobs for the audit trail", and reloads the dashboard + the schedule grid. On stale-token rejection re-runs the preview and prompts the organizer to re-confirm. See [`../data-model.md` §11 Reflow preview · Apply semantics](../data-model.md#reflow-preview).

Applying always writes a `repair_jobs` row with `trigger_type` set per the source trigger (`schedule_reflow` for the plain court-slide path; `court_outage` / `team_withdrawal` / `work_team_reassignment` for the dialog-fed paths). The dashboard does not surface the resulting RepairJob inline — the snackbar's deep link to `/repairs` is the audit affordance.

## Interactions

- **Initial load:** ensures `SelectedTournamentStore` is initialized, then fetches `GET /tournaments/{id}/court-status` which returns per-court summaries + per-match clock deltas + outage windows. The endpoint payload feeds **both** layouts; switching layouts via the toolbar toggle is a client-side rerender only, no refetch.
- **Filter changes:** re-filter client-side; do not refetch unless venue scope changes substantively.
- **Layout toggle:** swaps between Cards and Grid client-side; persists choice to `localStorage` under `courtflow.courtStatusLayout`.
- **TV mode toggle:** enters / exits fullscreen Grid mode per [TV mode](#tv-mode).
- **Refresh now / auto-refresh:** re-fetch status for all courts.
- **Start match:** `POST /matches/{id}:start`. On success refreshes the affected card or cell. The match's row in the Matches list and the cell in the Schedule grid pick up the new `started_at` on their next load.
- **Mark complete:** confirmation message box → `POST /matches/{id}:mark-complete`. Same refresh behavior.
- **Reflow preview:** opens the drawer described above. Reachable from a card's action row (Cards view), a cell's popover (Grid view), and a court column header (Grid view).
- **Mark court unavailable:** opens [`CourtOutageDialog`](../ui/dialogs.md#courtoutagedialog) which embeds the reflow preview inline before its Apply action.
- **View court detail:** navigates to the per-court detail surface (out of scope for this document).

## Dialogs

- [`DelayCourtDialog`](../ui/dialogs.md#delaycourtdialog) — already implemented (the V1 single-court slide shim; superseded by the reflow drawer when shipped).
- [`CourtOutageDialog`](../ui/dialogs.md#courtoutagedialog) — target; court outage repair trigger.

## Data inputs

The dashboard derives its delta calculations from:

- `Match.started_at` / `Match.completed_at` (the **Match clock** anchors) plus the current schedule version's `match_assignments.scheduled_start` / `scheduled_end`.
- The match's coarse `match_status` for filtering "currently playing" vs "upcoming" vs "done".
- Active `court_outages` windows so an idle court can be labeled "Out" rather than "Behind".
- `match_officials` to render the WorkTeam (and any R1 / R2 overrides) on each card.

See [`../data-model.md`](../data-model.md#8-match-generation) (Match clock + Work team auto-rotation), [`../data-model.md`](../data-model.md#9-scheduling-and-assignment) (MatchAssignment), and [`../data-model.md`](../data-model.md#11-repair-and-live-adjustment) (Reflow preview + court outages) for the underlying records.

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md), [`../ui/dialogs.md`](../ui/dialogs.md).
- Data: [`../data-model.md`](../data-model.md#match-clock-writes) (Match clock), [`../data-model.md`](../data-model.md#work-team-auto-rotation) (WorkTeam), [`../data-model.md`](../data-model.md#reflow-preview) (Reflow preview), [`../data-model.md`](../data-model.md#11-repair-and-live-adjustment).
- Product: [`../product-overview.md`](../product-overview.md) §1.4 (court status as a key differentiator).
- Sibling features: [`schedule.md`](schedule.md), [`matches.md`](matches.md), [`repairs.md`](repairs.md), [`work-teams.md`](work-teams.md), [`print-outputs.md`](print-outputs.md).
- Glossary: [`../../CONTEXT.md`](../../CONTEXT.md) — **Match clock**, **Reflow preview**, **WorkTeam**, **TV mode**.
