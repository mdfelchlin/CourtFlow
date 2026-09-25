# Print Outputs

> **Status:** **Target — not yet implemented.** This document is the contract for the V1.5 work that adds print-friendly outputs across CourtFlow.

## Purpose

Volleyball tournament directors print physical artifacts on tournament day — pool sheets, bracket sheets, scoresheets, work-team assignments, daily court runs. Every region manual reviewed during research (USAV, JVA, KRVA) requires at least the official roster and a posted pool/bracket schedule at each event site. If CourtFlow can't produce these artifacts, organizers maintain a parallel spreadsheet and the platform loses trust.

This feature is **cross-cutting**: print views live as alternate stylesheets on existing pages, not as new routes. The principles:

1. **Browser print stylesheets first.** Each surface that has a printable output adds a `@media print` stylesheet plus a **Print** action button. Clicking the button calls `window.print()` after applying the layout. No server-side PDF rendering in V1.5; that can come later if organizations ask.
2. **One artifact per logical sheet.** Pool sheet = one pool. Score sheet = one match. Bracket sheet = one bracket. Daily court run = one court × one day. This keeps each sheet purposeful and printable on a single page.
3. **All printable data is already in the page's loaded dataset.** Print views never trigger fresh fetches; they re-style what's already on screen.
4. **The print stylesheet is the source of truth for the print version.** Screen-only chrome (nav drawer, app bar, action buttons, MudBlazor backdrops) is hidden via `display: none`. The print artifact's header is augmented with branding (tournament name, division, date) that the screen version of the same page may not show prominently.

## Catalog

Eight artifacts ship in V1.5; each lives on its existing feature page with a small **Print** action.

| # | Artifact | Source page | Action label | What it contains |
| --- | --- | --- | --- | --- |
| 1 | **Pool sheet** | [`division-detail.md`](division-detail.md) → Pool card | **Print pool sheet** | Pool name, division, all teams (rank + name + club), the full round-robin grid (rows = teams, columns = teams, cells = "Match #N" / "—"), tiebreaker preset, work-team rotation column. |
| 2 | **Bracket sheet** | [`brackets.md`](brackets.md) → Bracket card | **Print bracket sheet** | Bracket name, type, division, the full tree drawn left-to-right with team-name slots and score lines. Single-page where the tree fits; multi-page when team_count > 16. |
| 3 | **Score sheet** | [`matches.md`](matches.md) → row | **Print score sheet** | Single match: division, pool/bracket, time, court, home + away rosters (with jersey numbers when present), set-by-set score columns, libero, work-team, R1, R2, signatures line. USAV-compatible layout. |
| 4 | **Daily court run** | [`schedule.md`](schedule.md) → toolbar | **Print court run** | One sheet per court for the selected day: every match on that court in order with scheduled start, duration, home/away, work team, division. Posted at the court. |
| 5 | **Daily division run** | [`schedule.md`](schedule.md) → toolbar | **Print division run** | One sheet per division for the selected day: every match in the division across all courts, with time, court, home/away, work team. Posted at the team tent. |
| 6 | **Pool standings page** | [`standings.md`](standings.md) → pool card | **Print standings** | Pool name, division, every team with W/L, sets won/lost, points for/against, set %, point %, ordered per the TiebreakerPreset. For mid-tournament posting. |
| 7 | **Work-team assignments per pool** | [`division-detail.md`](division-detail.md) → Work rotation panel | **Print work rotation** | Pool name, division, every match with `Home vs Away · Work team · R1 · R2`. Posted at the scorer's table. |
| 8 | **Tournament summary** | [`tournaments.md`](tournaments.md) → editor | **Print tournament summary** | One-page overview: name, dates, time zone, venues, courts, waves, divisions (count of teams in each), tiebreaker presets, default match duration. Used for clipboard handoff to a site director. |

## UI placement

Each artifact's source page adds one button to its toolbar / row action group. The button:

- Uses `Print` icon from MudBlazor (`Icons.Material.Filled.Print`).
- Label is always "**Print {artifact}**" exactly as listed in the catalog above.
- On click runs:
  1. The page sets `data-cf-print="{artifact-id}"` on `<body>` so the print stylesheet knows which template to apply (the same page may host multiple printable artifacts; the data attribute scopes which `@media print` rules activate).
  2. The page calls `await JS.InvokeVoidAsync("window.print")`.
  3. After the print dialog closes (or via a `beforeprint` / `afterprint` JS event), the data attribute is cleared.
- Disabled when the underlying data hasn't loaded yet.

Toolbar buttons fit alongside existing primary actions; for surfaces with many printable variants (e.g. Schedule has both Court run and Division run), a `MudMenu` with a `Print` icon trigger groups them:

```
[Print ▼]
  ├─ Print court run for today
  ├─ Print division run for today
  └─ Print all daily runs (one PDF, one court per page)  (V1.6+, deferred)
```

## Print stylesheet conventions

A single stylesheet at `wwwroot/css/print.css` is loaded on every layout but kicks in only under `@media print`. Rules in this file:

- Hide `MudAppBar`, `MudDrawer`, all `MudIconButton` action clusters, `MudSnackbarProvider`, `MudDialogProvider` backdrops, and any element with the `cf-print-hide` class.
- Show elements with `cf-print-only` class (used to surface tournament branding headers that are not part of the screen layout).
- Reset background colors to `white` for `MudPaper`, `MudCard`, `MudTable`, `MudList` so they print without ink-wasting fills.
- **Collapse semantic tint tokens to `transparent`** under `@media print` — every `--cf-tint-*`, `--cf-wave-band-*`, and `--cf-stripe-*` defined by [`../ui/design-system.md` §2 → Semantic surface tints](../ui/design-system.md#semantic-surface-tints) is redefined to `transparent` (gradients to `none`) inside the print stylesheet's `:root` block. Consumers of those tokens (Court Status Grid cells, wave-banded rows, outage stripes) then print as plain white regardless of their on-screen color encoding, while their **text content** (clock chips, WorkTeam captions, team names) remains untouched. This single overrides block replaces what would otherwise be a per-component `background: white !important` cascade.
- Use page break helpers: `cf-print-page-break-after` forces a page break, `cf-print-keep-together` applies `break-inside: avoid` for content blocks (e.g. one pool block per page).
- Apply 10pt body / 14pt heading sizes; serif font (Georgia) for body, sans (Inter) for headings — matches the legibility expectations of printed-and-posted material at a gym.
- Each artifact template gets its own scope inside `@media print` keyed off `body[data-cf-print="{artifact-id}"]`. Adding a new artifact = adding one new keyed block in the stylesheet.

The artifact-specific HTML lives as a sibling block in the source page's Razor component, hidden on screen via `cf-print-only`, shown in print via the stylesheet. Pages do not have to ship two complete templates — they re-style their existing DOM where possible and add a minimal `cf-print-only` header containing the branding row.

## Branding header (shared)

Every printed artifact starts with a single header row containing:

- Tournament name (left, 16pt bold).
- Tournament date range (left, 11pt under the name).
- Organization name (right, 11pt italic).
- Optional **organization logo** if `organizations.print_logo_url` is set (right of the org name; max 60×60 px).
- Below the row, a horizontal rule.

Underneath the header, the artifact's title (e.g. "Pool A — Girls 14U Open") in 14pt bold.

This standardized header makes any printed CourtFlow artifact recognizable without needing to look at the URL bar.

## Page-size assumptions

- Default page size: **US Letter (8.5×11 in), portrait**, with **0.5 in** margins.
- Bracket sheets > 16 teams switch to **landscape** automatically via `@page :first { size: landscape; }`.
- Daily court run sheets prefer one page per court; if a court has more than ~12 matches in a day the sheet flows to a second page with the branding header repeated.

## Interactions

- Clicking **Print {artifact}** opens the browser's native print dialog. There is no in-app preview surface in V1.5 — the browser preview is the preview.
- **Right-click → Save as PDF** in the browser's print dialog is the supported "I want a PDF" path. CourtFlow does not host PDF generation in V1.5.
- All print buttons share the same disabled / enabled logic — they require the underlying data to have loaded but **do not** require a published schedule version (organizers print previews of draft work routinely). The branding header includes a small "Draft" watermark when the source data is from a non-`published` ScheduleVersion or `draft` Tournament.

## Accessibility

- All print buttons carry `aria-label="Print {artifact}"`.
- The print stylesheet does **not** rely on color alone for meaning — chips that carry status meaning on screen render their status as text in print (e.g. `Status: Final` instead of just a green chip).
- High-contrast: print stylesheet uses black text on white background only, including for status chips (which become `[Status]` bracketed text).

## Gap from current V1 implementation

Today CourtFlow has no print stylesheet and no Print actions. Adding them is non-blocking — every artifact's data already exists on the source page; the work is layout + stylesheet only. There is no schema change required for this feature.

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md).
- Source pages: [`division-detail.md`](division-detail.md), [`brackets.md`](brackets.md), [`matches.md`](matches.md), [`schedule.md`](schedule.md), [`standings.md`](standings.md), [`tournaments.md`](tournaments.md).
- Sibling: [`work-teams.md`](work-teams.md) (#7 work rotation printout source).
