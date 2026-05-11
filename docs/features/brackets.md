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
- Each panel renders the **BracketView** component for the bracket id.

### BracketView

- Loading: indeterminate progress bar.
- Layout: horizontal scroll container with `gap: 48px` between branches (winners / losers / etc.). Each branch column shows:
  - Branch name (`subtitle2`).
  - Sub-columns per round (`gap: 16px`), labeled "R1", "R2", … (`caption`).
  - One outlined `MudPaper` per node, displaying two lines per side:
    - **Resolved:** `#seed teamName` (e.g. `#1 Aces 14U Black`).
    - **Placeholder (unresolved):** the slot's source label — `Gold-1 ← Pool A · 1st` or `← TBD (pool play in progress)` — when the bracket stage is in `configured` or `seeded` and `resolved_tournament_team_id` is still null.
    - **Manual override (📌):** a pin icon next to the seed when `manual_tournament_team_id` is set, with the actor and reason in a tooltip. Sourced from `bracket_seed_slot_overrides`.
    - **BYE:** explicit `BYE` label (no team).

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
