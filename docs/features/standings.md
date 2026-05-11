# Standings

> **Term note:** This page shows **Standing** — the live, in-progress ranking inside each **Pool** (see [`../../CONTEXT.md`](../../CONTEXT.md)). Once a pool closes, the per-team Standing snapshots into **FinishPosition** (`pool_entries.finish_position`), which drives **BracketSeed** via `advancement_rules`. Standings are not the same as **Ranking** (organizer pre-pool input) or **Seed** (pool placement).

## Purpose

Show pool standings for a chosen division so admins can see where each team ranks during pool play, and so they can verify the **FinishPosition** that will feed bracket seeding once each pool closes.

## Routes

| Route | Page |
| --- | --- |
| `/standings` | Pool standings per division |

## Page header / toolbar

- Page heading "Pool Standings" (`Typo.h4`).
- Right-aligned cluster:
  - **Division** select (dense, outlined, sized `cf-select--md`).
  - **Tiebreaker** caption chip (read-only) showing the active **TiebreakerPreset** for the selected division — one of `WINS_H2H_SETS_POINTS`, `WINS_SETS_POINTS`, `SETS_POINTS`, `H2H_SETS_POINTS`. Tooltip expands to the full chain (e.g. "Wins → head-to-head → set ratio → point ratio"). Editing the preset lives on the [**Division detail**](division-detail.md) workspace.

## Page body

- Empty state: `MudAlert Info` "No standings yet. Enter scores to populate."
- Otherwise, one `MudPaper` (elevation 1, margin-bottom 4, padding 3) per pool, containing:
  - Pool name (`Typo.h6`).
  - Dense `MudTable` with columns: `#`, **Team**, **MP**, **W**, **L**, **SW**, **SL**, **Set %**, **PF**, **PA**, **Pt %**.
  - **Set %** = `SW / (SW + SL)` and **Pt %** = `PF / (PF + PA)` — both ratio, never differential. See [`../data-model.md`](../data-model.md#standings-tally-per-pool-per-tournamentteam).
  - Row order is the resolved order produced by the division's TiebreakerPreset; the underlying chain semantics live in [`../data-model.md`](../data-model.md#tiebreaker-presets).

## Interactions

- Initial load: ensures `SelectedTournamentStore` is initialized, then loads divisions and selects the first.
- Changing the **Division** select fetches `GET /divisions/{id}/standings` and re-renders the per-pool tables.
- All errors surface via snackbar.

## Dialogs

None.

## References

- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md).
- Data: [`../data-model.md`](../data-model.md#10-score-reporting-and-winner-resolution) (set-level scores, standings tally, tiebreaker presets), [`../data-model.md`](../data-model.md#3-division-creation) (`tiebreaker_preset` lives on `tournament_divisions`).
- Sibling features: [`matches.md`](matches.md), [`brackets.md`](brackets.md), [`division-detail.md`](division-detail.md).
