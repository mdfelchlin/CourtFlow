# Brackets

> **Term note:** A **Bracket** is one elimination tree inside a **bracket-type Stage** of a **Division**; a **Pool** lives inside a **pool-type Stage**. See [`../../CONTEXT.md`](../../CONTEXT.md) for the full Stage / Pool / Bracket vocabulary. Bracket **authoring** (structure, advancement rules, manual slot overrides) lives in the [**Division detail**](division-detail.md) structure-first wizard; this page is the **viewer**.

## Purpose

Let admins **view** the bracket tree for a division — including placeholder labels before teams resolve, manual-override pins (📌), and winners/losers branches for double elimination. Generating and editing the bracket structure happens in the Division detail workspace; this page is a read-mostly inspection surface plus a shortcut to generate when none exists.

## Routes

| Route | Page |
| --- | --- |
| `/brackets` | Tabbed bracket viewer per division |

## Page header / toolbar

- Page heading "Brackets" (`Typo.h4`).
- Right-aligned action cluster:
  - **Division** select (dense, outlined, sized `cf-select--md`) bound to the divisions for the active tournament.
  - **Manage in Division detail** text button — deep-links to `/divisions/{Id}` for full structure-first editing (pool structure, bracket structure, advancement rules, manual overrides). See [`division-detail.md`](division-detail.md).
  - **Generate** primary filled button (`Add` icon, small) — shortcut for the common case where no bracket exists yet for the division. Disabled until a division is selected. Opens [`GenerateBracketDialog`](../ui/dialogs.md#generatebracketdialog). For everything beyond "make a default bracket", use the Division detail workspace.

## Page body

- Empty state: `MudAlert Info` "No brackets in this division. Generate a default bracket above, or open Division detail to design the full structure."
- Otherwise `MudTabs` (no elevation, outlined). One `MudTabPanel` per bracket; tab text is `"{display name or type} · {capacity}"`.
- Each panel renders **BracketFlowView** for that bracket id (vertical stacked mini-brackets). Data loads from the enriched bracket tree API (`GET /api/v1/brackets/{id}/tree`), including **`Groups`** for display composition.

### BracketFlowView

- Loading: indeterminate progress bar.
- **Theme:** CourtFlow MudBlazor palette (`--mud-*`, `--cf-*` tokens) — compact SportWrench-style **vertical flow**, not a single wide branch canvas.
- **Layout:** The page scrolls **vertically** through **display groups** — **Main bracket**, then a single **Lower bracket** mini-tree, then **Grand final** / **Third place** as needed. Each group is a compact **horizontal mini-tree** (`MiniBracketTree`) with its own round headers and internal connectors. Consolation or one-off lower-path matches appear **inside** the lower mini-tree (round columns remapped to stay compact), not as separate page sections.
- **Grouping:** Server-side **`BracketDisplayGrouper`** puts all losers-branch nodes in one lower group; the API returns `Groups` alongside flat `Nodes`. Cross-branch loser drops are **not drawn** as connectors — slots show **`Loser of M{n}`** until teams populate.
- **Positioning:** Each mini-tree uses **`BracketLayoutEngine`** in `MiniBracket` mode (no branch bands). Vertical spacing within a mini-tree is content-aware (~12px leaf gap).
- **Connectors:** Solid SVG elbows **within** each mini-bracket only.

### BracketMatchCard (match unit)

Slim stacked rows — not a giant bordered card:

- **Match rail:** vertical `M{n}` label left of the participant stack (continuous numbering across the whole bracket) — from server `MatchLabel` or client numbering.
- **Schedule caption (above rows):** when the match is on a published schedule — `ddd, h:mm tt · Venue / Court`; unscheduled matches reserve the same caption band (invisible spacer). All bracket nodes get a `Match` row at generate time so the scheduler can assign court/time before both teams are known.
- **Participant rows:** two slim bordered rows (name + trailing score); winner row uses `--cf-tint-success-pale`; path highlight uses `--cf-tint-info-pale` on the unit wrapper.
- **Resolved:** team name on each row.
- **Placeholder (round 1):** seed slot label — e.g. `Gold-1 ← Pool A · 1st`, or `Seed N` when the slot is not yet resolved.
- **Placeholder (round 2+):** always a **known source** — `Winner of M{n}` or `Loser of M{n}` from the bracket graph. Generated brackets never show `TBD`; unwired sides fall back to `Open`.
- **BYE:** explicit **BYE** on the affected side.
- **Manual override (📌):** inline pin with tooltip when `manual_tournament_team_id` is set.
- **Scores:** set summary on the trailing edge when scored; em dash only for in-progress/final unscored matches.

### Responsive behavior

- Desktop and tablet: **horizontal scroll** when the canvas exceeds the viewport (hint shown for 16+ teams).
- Mobile: same pattern — users **swipe** the tree horizontally rather than collapsing to a table.

## Interactions

- Changing the **Division** select reloads brackets for that division (`GET /divisions/{id}/brackets`).
- **Generate** opens [`GenerateBracketDialog`](../ui/dialogs.md#generatebracketdialog). On save (not canceled) reloads brackets.
- **Manage in Division detail** navigates to `/divisions/{Id}` (the structure-first wizard).
- All errors surface via snackbar.

## Dialogs

- [`GenerateBracketDialog`](../ui/dialogs.md#generatebracketdialog) — bracket type, capacity, if-necessary final. V1 quick path; full structure editing lives in [`division-detail.md`](division-detail.md).

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md), [`../ui/dialogs.md`](../ui/dialogs.md).
- Data: [`../data-model.md`](../data-model.md#7-bracket-configuration-and-advancement-inside-a-bracket-stage) (`brackets`, `bracket_seed_slots` with `advancement_rule_id` / `manual_tournament_team_id` / `resolved_tournament_team_id`, `advancement_rules` with `order_by`, `bracket_seed_slot_overrides` audit).
- Sibling features: [`schedule.md`](schedule.md), [`standings.md`](standings.md), [`division-detail.md`](division-detail.md) (full structure-first authoring surface).
