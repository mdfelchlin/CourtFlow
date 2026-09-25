# Division detail (structure-first workspace)

This is the **target** admin workspace where a tournament director designs an entire division — **Ranking**, **Stage** pipeline, **Pool** and **Bracket** structure, **AdvancementRules**, division-level settings — and where teams are placed into that structure. Vocabulary is pinned in [`../../CONTEXT.md`](../../CONTEXT.md); schema and lifecycles in [`../data-model.md`](../data-model.md#6-stages-the-round-container).

## Purpose

Give tournament directors one place per **Division** to:

1. **Build the tournament shape first**, with no teams required. Pools, brackets, and advancement rules are authored against **placeholder labels** so an organizer can lay out a division weeks before registrations close.
2. **Order TournamentTeams** for seeding (**Ranking**) in a persistent left panel that lights up as teams are imported or added — independent of structure design.
3. **Materialize** the structure by snaking ranked teams into pools and resolving bracket slots from advancement rules and manual overrides.
4. **See each Stage status** and exactly why a primary action is blocked (inline blocking reasons — no silent disabled buttons).

The page is the human surface of the engine described in [`../data-model.md`](../data-model.md): the same primitives that ship in the V1 schema (stages, `seeding_source`, advancement rules with `order_by`, `bracket_seed_slots` with manual overrides, `match_winner` / `match_loser` participant sources) will support Power Pool and Challenge Phase wizards in later releases without migrations. The V1 wizard exposes the constrained subset.

## Routes

| Route | Page | Layout |
| --- | --- | --- |
| `/divisions/{Id:guid}` | Division detail — structure-first workspace | `MainLayout` / admin shell |

Entry: **Manage** from [`divisions.md`](divisions.md) (`/divisions` table) or deep link from snackbars elsewhere.

## Page chrome

- **Back** text button → `/divisions`.
- **Title row:** division name (`Typo.h5`) + optional subtitle (wave, format hint derived from current stage shape).
- **Division settings** affordance (icon or "Settings") — opens a slide-over for name, max teams, format flags, **`tiebreaker_preset`**, best-of-sets, scheduling defaults. (Same data as add/edit division.)
- **Breadcrumb** optional: `Divisions` → `{name}`.

## Information architecture

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│ Division detail                                                       [Settings ⚙]  │
├─────────────────────────────────────┬───────────────────────────────────────────────┤
│ Rankings panel (persistent)         │  ┌──────────────────────────────────────────┐ │
│ ─────────────────────               │  │ Progress bar:  Pool ── Bracket ── Rules  │ │
│ 1. Aces 14U Black     [drag]        │  └──────────────────────────────────────────┘ │
│ 2. Crush 14U Black                  │                                               │
│ 3. Volley Co 14U Red                │   Step 1 · Pool Structure                     │
│ 4. Storm 14U Blue                   │   ──────────────────────                      │
│ ...                                 │   (pool cards with placeholder slots)         │
│ ─────────────────────               │                                               │
│ [Randomize]   [Save]                │   Step 2 · Bracket Structure                  │
│ ─────────────────────               │   Step 3 · Advancement Rules                  │
│ Materialize                         │                                               │
│ ┌─────────────────────────────┐     │                                               │
│ │ Snake Rankings into Pools   │     │                                               │
│ │ Resolve Bracket Slots       │     │                                               │
│ └─────────────────────────────┘     │                                               │
└─────────────────────────────────────┴───────────────────────────────────────────────┘
```

- **Left column (persistent):** Rankings panel + the two materialize actions. Always visible regardless of wizard step. Does not gate the wizard.
- **Right column (the wizard):** a progress bar at the top followed by the current step's content. The three steps are pure **structure** — none of them require teams to exist.
- **Below the wizard (optional Danger zone):** delete division, archive, or reset-all-stages (behind destructive confirm).

## Rankings panel (left, persistent)

- Lists every **TournamentTeam** in the division, ordered by `division_rankings.rank` ascending.
- **Drag handle** on each row reorders. Reorder is local until **Save** persists the new order to `division_rankings`.
- **Randomize** button shuffles draft order (does not save until Save). Uses a deterministic seed so the snake step is reproducible.
- **Save** persists the new rank order. Snackbar "Ranking saved."
- **Caption** above the list distinguishes **Ranking** from **Seed** (pool placement) and **Standing** (live result) — links to [`../../CONTEXT.md`](../../CONTEXT.md).
- Empty state: "No teams in this division yet. Add teams from [Teams]({/teams}) or [Imports]({/imports})." The wizard remains usable while empty — structure design does not require teams.
- Disabled state: read-only once any stage transitions to `in_progress`; the disabled banner names the stage and links to the Repairs flow.

#### Unplaced teams (added after `seeded`)

When a team is added to the division *after* the first pool Stage has reached `seeded`, it lands in the Rankings panel at the end of the rank list but is **not** auto-placed into a pool. The Rankings list visually segments these teams:

- A dashed divider labeled "**Unplaced — added after seeding (N)**" appears below the last placed team. Unplaced rows show their `division_rankings.rank` plus an amber **Unplaced** chip.
- Each unplaced row has a row-level action overflow with two options:
  - **Place in pool…** opens a small popover with the division's pools as a select; choosing a pool inserts the team into `pool_entries` at the next available `pool_slot_index` and triggers a single-team match-pair regeneration for that pool (the pool's existing matches keep their identity; new matches are appended in `match_order`). The schedule reflows via the standard Reflow preview flow when the loaded `ScheduleVersion` is `published`.
  - **Re-snake everything** runs the **Snake Rankings into Pools** materialize action with the standard "this rewrites seeded pools" warning — the right move when several unplaced teams are present and the organizer would rather restart pool placement clean.
- Until placed, unplaced teams are excluded from match generation, standings, and the bracket slot resolver. The Step 1 pool cards show a small caption "+ N unplaced" linking back to the Rankings panel.

This is the same path the [`teams.md`](teams.md) page documents under "Adding a team after the division's pool stage is seeded" — the Teams page just creates the `TournamentTeam` and `division_rankings` row; this panel is where the organizer decides what to do with the new entrant.

### Materialize actions (left column, below Rankings)

Shared idempotency, **Stage status** gating, and **RepairJob** vs in-wizard rules for these buttons are owned by the **StageMaterialization** / **BracketResolution** Application modules — see [`../adr/0005-stage-materialization-and-bracket-resolution.md`](../adr/0005-stage-materialization-and-bracket-resolution.md) and [`../architecture.md`](../architecture.md#deep-modules-named-application-seams).

Two idempotent buttons that bridge Rankings to the structure built on the right side.

| Button | Writes | Enabled when | Disabled reason copy |
| --- | --- | --- | --- |
| **Snake Rankings into Pools** | `pool_entries` for the first pool-type Stage according to `seeding_method` (snake by default), **then triggers pool-play match generation** — see [§8 When matches are created · Trigger 1](../data-model.md#when-matches-are-created). | Rankings has ≥ pool count × 1 teams, the first pool-type Stage exists with `pool_count` and `target_size` set, and the stage is `draft` or `configured`. | "Add at least N teams to Rankings." / "Define pool structure in Step 1 first." / "Stage 1 is `in_progress` — use Repairs to change placements." |
| **Resolve Bracket Slots** | `bracket_seed_slots.resolved_tournament_team_id` per [§7 Resolve Bracket Slots](../data-model.md#resolve-bracket-slots-the-materialization-action). Does **not** generate bracket matches — those are generated by the **Generate brackets** action in Step 2 (see [§8 When matches are created · Trigger 2](../data-model.md#when-matches-are-created)). | A bracket-type Stage exists with brackets generated, and either (a) the upstream pool stage is `completed` with all `finish_position`s written, or (b) every slot has a `manual_tournament_team_id` (override-only bracket). | "Generate brackets in Step 2 first." / "Stage 1 still has pool matches in progress — finish pool play to resolve advancement rules." |

Both buttons are re-runnable. **Snake Rankings into Pools** warns when running it would overwrite manual placements (drag-drop nudges in Step 1 between pools) or regenerate matches that are already `in_progress` / `final`; a confirm dialog explains the diff and offers "Re-snake everything" vs "Cancel". Matches already started block the regenerate — those require a **RepairJob**. **Resolve Bracket Slots** never overwrites `manual_tournament_team_id` — only rule-resolved slots are recomputed. The companion **Generate brackets** action in Step 2 also materializes the full bracket tree (round 1 via `bracket_seed_slot` participant sources; round 2+ via `match_winner` / `match_loser` participant sources).

## Wizard (right column)

### Progress bar

A horizontal `MudStepper`-style progress indicator at the top of the right column:

```
●─────●─────●
Pool   Bracket   Rules
```

- **Step is "complete"** (filled circle) when its validation passes — even with placeholder data only.
- **Step is "current"** when the user is editing it.
- Clicking a completed step navigates back; clicking a future step is allowed (no enforced order) but flags missing prerequisites in the destination's blocking-reasons strip.
- The progress bar is per-division and per-stage-pair: if a division ever grows to multi-stage formats in future, the bar grows accordingly. V1 caps at 1 pool stage + 1 bracket stage so the bar stays three steps.

### Step 1 · Pool Structure

**Purpose:** define how many pools exist, how big each is, and their order. **Does not require any team to exist.**

#### Header

- **Stage status chip** (`Draft` / `Configured` / `Seeded` / `In progress` / `Completed`) — maps `stages.status`. Tooltip explains current meaning.
- **Stage settings** affordance: opens a slide-over with `pool_settings` fields (`pool_count`, `teams_per_pool_target` default for new pools, `pool_size_strategy`, `club_separation_mode`, `play_style`, `ref_assignment_mode`). Changes here apply on save.
- **Add pool** button (`Add` icon, small) — appends a pool with `target_size = teams_per_pool_target` and an auto name (next letter). Opens [`PoolDialog`](../ui/dialogs.md#pooldialog) for non-default settings.
- **Apply suggested layout** — given a target team count and `pool_size_strategy`, suggests `pool_count` and per-pool `target_size`. Replaces the current pool list with confirm.

#### Blocking reasons strip

When any primary action on this step is disabled, an inline `MudAlert` directly under the header lists the reasons:

- "Pool count must be ≥ 1." / "Pool A's target size must be between 2 and 8." / "Two pools share the name 'Pool A'." / "Pool structure cannot be edited — this stage is `seeded`. Roll back to `configured` first."

No silent disabled buttons.

#### Body

A horizontal stack of **Pool cards** (one column per pool), ordered by `pools.sequence`. Each card:

```
┌──────────────────────────────┐
│ ⠿ Pool A    [⋯]              │   header row (drag handle, overflow menu)
├──────────────────────────────┤
│ Courts: [Court 1 ×][Court 2 ×]   pool_court_allocations editor
│         [+ Add court]        │
├──────────────────────────────┤
│ Slot 1: Aces 14U Black       │   filled slot
│ Slot 2: ─                    │   placeholder slot
│ Slot 3: ─                    │   placeholder slot
│ Slot 4: ─                    │   placeholder slot
├──────────────────────────────┤
│ [+ Resize]                   │   change target_size
└──────────────────────────────┘
```

- **Drag handle** on the pool header — drag to reorder pools (changes `pools.sequence`). Disabled in `seeded`, `in_progress`, `completed`.
- **Pool name** is editable inline.
- **Overflow menu:** rename, resize (changes `target_size`; warns if shrinking past current entries), delete (only when this stage is `draft` or `configured` and no advancement rule depends on this pool).
- **Courts strip** (`pool_court_allocations` editor):
  - A row of court chips representing the courts assigned to this pool. Each chip shows court name and a small `×` to remove.
  - `+ Add court` opens a court-picker popover scoped to the parent tournament's courts (`tournaments.md` → Venues & courts). Picking a court inserts a `pool_court_allocations` row.
  - Empty state caption: "No courts assigned — scheduler will auto-allocate from division `target_court_count`." Empty is valid; the scheduler falls back to division-level allocation rules.
  - Per-chip overflow: **Set allocation type** (`preferred` / `required`) and **Reorder** (drag chips to set `priority_order`). Allocation type semantics are defined in [`../data-model.md`](../data-model.md#61-pool-generation-inside-a-pool-stage).
  - **Edit routing by Stage status and schedule state:**
    - In `draft` or `configured` — chip edits write `pool_court_allocations` directly. No RepairJob.
    - In `seeded` *with no `match_assignments` yet generated for this pool* — same direct-write path; the next schedule generation picks up the new allocation.
    - In `seeded` *with `match_assignments` generated*, or in `in_progress`, or in `completed` — chip edits **do not silently mutate the allocation**. Instead the change is treated as a **pool court swap** proposal: the strip's save action opens [`PoolCourtSwapDialog`](../ui/dialogs.md#poolcourtswapdialog) pre-filled with the new court set, which routes through the [Reflow preview drawer](court-status.md#reflow-preview-drawer) and applies as a `pool_court_swap` **RepairJob**. See [`repairs.md`](repairs.md#trigger-5-pool-court-swap).
  - A small caption under the strip surfaces the active routing so the user knows which mode they're in: "Editing structural courts (free)" vs "Editing will create a court-swap RepairJob — N remaining matches will move".
- **Slot rows** (1..`target_size`):
  - **Empty (no team placed):** placeholder label `Pool A · 1`, `Pool A · 2`, …, rendered with a muted style. Drag-and-drop target.
  - **Filled (team placed):** `Pool A · 1 — Aces 14U Black`. Drag-and-drop source and target.
- **Resize** affordance below the slot list adjusts `target_size`. Adds or removes placeholder rows.

#### Drag-and-drop matrix

| Gesture | Effect when empty | Effect when filled |
| --- | --- | --- |
| Drag a pool card | Reorder pools (changes `pools.sequence`). | Same — moves both the pool meta and any `pool_entries` under it. |
| Drag a slot to another slot in the same pool | Swap `seed_in_pool` indices on both slots' rows (no-op for two empty placeholders, but the UI tracks intent). | Swap teams between slots. |
| Drag a slot to another pool's slot | Move slot intent across pools. | Swap teams between pools (updates `pool_id` on both `pool_entries`). |
| Drag a team chip from Rankings to a pool slot | n/a (no team). | Place that team in the slot. Creates or updates the `pool_entry`. |

#### Validation feeds blocking-reasons strip

- `pool_count` must be ≥ 1.
- Each pool's `target_size` must be in `[2, 8]` (configurable on division settings if needed; V1 default is `[3, 5]`).
- Pool names unique within the stage.
- When transitioning to `seeded`, every pool slot must reference a `tournament_team_id` — the "Snake Rankings into Pools" action is the canonical way to fill them.

### Step 2 · Bracket Structure

**Purpose:** define how many brackets exist (Gold, Silver, …), how many slots each holds, and the bracket type (single elim, double elim with grand-final reset / third-place match / if-necessary-final). **Does not require teams to exist or pool play to be complete.**

#### Header

- **Stage status chip** for the bracket Stage.
- **Stage settings** affordance — per-bracket defaults (`format`, `third_place_match`, `grand_final_reset`, `consolation_style`, `seed_style`).
- **Add bracket** button — appends a bracket (e.g. Silver next to Gold).
- **Generate brackets** primary button — materializes `brackets` rows with their `bracket_seed_slots`. Idempotent: re-runs preserve `manual_tournament_team_id` overrides.

#### Blocking reasons strip

Same pattern as Step 1. Specific blockers (full list in [§7 Validation before bracket generation](../data-model.md#validation-before-bracket-generation)):

- "Gold bracket has 8 slots but only 6 are covered by an advancement rule and there are no BYEs marked."
- "Two rules without priority target Gold-3."
- "Cannot edit bracket structure — this stage is `in_progress`. Use Repairs to modify."

#### Body

A vertical stack of **Bracket cards**. Each card:

```
┌────────────────────────────────────────────────────────────────────────┐
│ ⠿ Gold   ·   8 teams   ·   Double Elim                        [⋯]      │
├────────────────────────────────────────────────────────────────────────┤
│ Slot 1:  Gold-1 ← Pool A · 1st                          [📌] [drag]    │
│ Slot 2:  Gold-2 ← Pool B · 1st                          [drag]         │
│ Slot 3:  Gold-3 ← Pool C · 1st                          [📌 manual:    │
│                                       Storm 14U Blue]   [override]     │
│ Slot 4:  Gold-4 ← Pool D · 1st                                         │
│ Slot 5:  Gold-5 ← Pool A · 2nd                                         │
│ Slot 6:  Gold-6 ← Pool B · 2nd                                         │
│ Slot 7:  Gold-7 ← Pool C · 2nd                                         │
│ Slot 8:  Gold-8 ← Pool D · 2nd                                         │
└────────────────────────────────────────────────────────────────────────┘
```

- **Drag handle** on the bracket header — reorder brackets in the stage (changes `brackets.sequence`).
- **Slot rows** show the **placeholder label** (the rule that fills the slot) and, when resolved, the team name appended after a dash. A `📌` icon indicates a **BracketSlotOverride** (manual pin) and shows the actor on hover.
- **Slot drag-and-drop:** reorder seed positions within a bracket (swaps `seed_position` and the `advancement_rule_id` reference moves with the slot — the rule itself doesn't change, only which slot it resolves to).
- **Override affordance** per slot:
  - Drag a team chip from Rankings → sets `manual_tournament_team_id` (creates a row in `bracket_seed_slot_overrides`).
  - Right-click / overflow → "Pin team to this slot…" opens a picker dialog (also writes the override).
  - Right-click → "Clear override" removes the manual pin (slot reverts to the upstream rule).
- **Mark as BYE** affordance per slot — sets `expected_source_type = 'bye'` (used for non-power-of-2 brackets).

#### Preview bracket tree (after Generate brackets)

Once **Generate brackets** has materialized at least one bracket for the stage, each **Bracket card** in Step 2 gains an inline **preview panel** below the slot list:

- **Collapsed by default** (expand chevron / "Preview bracket tree") so dense divisions stay scannable until the director opens it.
- **Content:** read-only **BracketTree** for that bracket id — same visual vocabulary as [`brackets.md`](brackets.md) (flow layout, connectors, match cards, placeholders, pins, BYE). Horizontally scrollable inside a **compact max-height** region so Step 2 does not consume the full viewport on large draws.
- **Before generate:** no bracket id yet — show muted placeholder copy ("Generate brackets to preview the tree") or omit the panel until materialization succeeds.

This is an **inspection** aid inside the wizard; full-screen browsing remains on **`/brackets`**.

#### Drag-and-drop matrix (brackets)

| Gesture | Effect |
| --- | --- |
| Drag a bracket card | Reorder brackets in the stage (`brackets.sequence`). |
| Drag a bracket seed slot to another slot | Swap `seed_position` ordering — the slot's rule reference travels with it. |
| Drag a pool-finish chip from Step 3 onto a bracket slot | Creates or replaces the slot's `advancement_rule_id` reference (and, for wildcard rules, the slot's place in the wildcard's consumed range). |
| Drag a team chip from Rankings onto a bracket slot | Sets `manual_tournament_team_id` (audit-logged **BracketSlotOverride**). Empty or filled state — works either way. |

### Step 3 · Advancement Rules

**Purpose:** wire pool finish positions to bracket slots. **Works on placeholders** — slots are addressed by `seed_position` (`Gold-1`, `Gold-2`, …), pool finishes by `(pool, position)` chips.

#### Header

- **Validate** button — runs the rule validator and surfaces any blocking conditions on Step 2 *and* on this step.
- **Generate brackets** — same action as Step 2's button (mirrored here for ergonomic flow).

#### Quick presets

A row of buttons above the editor grid. Each preset inserts one or more rules; the user can then edit / delete:

| Preset | Inserts |
| --- | --- |
| **Top N from each pool → Gold** | For each `k` from 1 to N, a wildcard rule (`source_pool_id = NULL`, `source_finish_position = k`, `order_by = pool_sequence`) targeting consecutive Gold slots starting at slot 1. |
| **Remaining → Silver** | A wildcard rule with `source_finish_position > N` (modeled as one rule per remaining position) targeting consecutive Silver slots starting at slot 1, `order_by = pool_sequence`. |
| **All 1st-place finishers → Bracket X** | Single wildcard rule, `source_finish_position = 1`, target = chosen bracket. |
| **Re-rank top-N by tiebreaker** | Same shape as "Top N from each pool", but `order_by = tiebreaker_rank` instead of `pool_sequence`. Used by formats that want cross-pool comparison; the customer's default format does **not** use this. |

#### Rule editor grid

One row per rule, columns:

| Column | Editor | Notes |
| --- | --- | --- |
| **Source pool** | Select: each `Pool` in source stage, or **"Any pool (wildcard)"**. | Wildcard expands to one slot per pool at materialization. |
| **Finish position** | Numeric (1..N). | Maps to `source_finish_position`. |
| **Target bracket** | Select: each `Bracket` in the target stage. | Maps to `target_bracket_id`. |
| **Target slot (start)** | Numeric. | For wildcard rules this is the *starting* slot; subsequent slots are filled per `order_by`. |
| **Order by** | Toggle: **Pool sequence** *(default)* / **Tiebreaker rank**. | Maps to `advancement_rules.order_by`. The wizard caption explains each: "Pool sequence preserves incoming seeding (Pool A 1st → Gold-1). Tiebreaker rank compares the wildcard's produced teams across pools by your TiebreakerPreset chain." |
| **Priority** | Numeric (lower = higher priority). | Resolves conflicts when two rules target the same slot. |
| **Actions** | Edit / Delete row. | |

#### Validation (continuous)

As the user edits, the validator runs and feeds the blocking-reasons strip on Step 2:

- Source stage status is below `seeded` → warning (not blocker).
- Two rules without priority target the same slot → blocker.
- A bracket has unfilled slots that are not explicit BYEs or overrides → blocker for **Generate brackets**.
- A rule references a pool position that exceeds the pool's `target_size` → blocker.

See [`../data-model.md`](../data-model.md#wildcard-rule-expansion) for the wildcard expansion algorithm and [`../data-model.md`](../data-model.md#validation-before-bracket-generation) for the full validation list.

## Stage status and editability

| Status | Step 1 (Pool Structure) | Step 2 (Bracket Structure) | Step 3 (Advancement Rules) | Snake / Resolve buttons | Rankings panel |
| --- | --- | --- | --- | --- | --- |
| `draft` | Editable | Editable | Editable | Disabled until structure validates | Editable |
| `configured` | Editable | Editable | Editable | Enabled when inputs valid | Editable |
| `seeded` | Drag-swap within pool / between pools to nudge placements; structural changes (resize, add/delete pool) require rollback confirm to `configured` | Same for brackets; manual slot overrides allowed without rollback | Editable, but rule changes only re-resolve unpinned slots | "Snake Rankings into Pools" warns about overwriting nudges; "Resolve Bracket Slots" recomputes unpinned slots only | Editable |
| `in_progress` | Read-only | Read-only | Read-only | Disabled — link to Repairs | Read-only |
| `completed` | Read-only | Read-only | Read-only | Disabled | Read-only |

## Interactions (target API shape — illustrative)

Exact paths TBD during implementation; verbs map to aggregates per [`../architectural-prd.md`](../architectural-prd.md):

- `GET /divisions/{id}/detail` — division + stages + summary counts + placeholder labels.
- `PATCH /stages/{id}` — name, settings, `seeding_source`, status transitions where allowed.
- `POST /stages` / `DELETE /stages/{id}` — add/remove stage.
- `PATCH /pools/{id}` — name, `target_size`, sequence.
- `POST /stages/{id}/pools` / `DELETE /pools/{id}` — add/remove pool.
- `PATCH /brackets/{id}` — name, `team_count`, settings.
- `POST /stages/{id}/brackets:generate` — materialize bracket seed slots.
- `PUT /advancement-rules` (bulk under stage scope) — replace the rules list for a pool→bracket stage pair.
- `PUT /bracket-seed-slots/{id}/override` — set `manual_tournament_team_id` and append a `bracket_seed_slot_overrides` audit row.
- `DELETE /bracket-seed-slots/{id}/override` — clear manual override.
- `POST /divisions/{id}/rankings:reorder` — persist Ranking.
- `POST /stages/{id}/pools:snake-rankings` — Snake Rankings into Pools action.
- `POST /stages/{id}/brackets:resolve` — Resolve Bracket Slots action.

All errors → snackbar; structural validation → blocking-reasons strip.

## Dialogs

- [`PoolDialog`](../ui/dialogs.md#pooldialog) — create / edit pool inside the active pool-type Stage (name, `target_size`).
- Division settings (slide-over or reuse pattern from tournament editor).
- "Pin team to slot" picker (override) — to be specified alongside the bracket override flow.
- Confirmation: rollback `seeded` → `configured`, delete stage, destructive regenerate, "Snake will overwrite manual placements" diff confirm.

## References

- Glossary: [`../../CONTEXT.md`](../../CONTEXT.md) (**Stage**, **StageType**, **SeedingSource**, **Pool**, **Bracket**, **AdvancementRule**, **BracketSlotOverride**, **Stage status**).
- Data: [`../data-model.md`](../data-model.md#3-division-creation), [§5 Ranking](../data-model.md#5-ranking-and-seeding), [§6 Stages](../data-model.md#6-stages-the-round-container), [§6.1 Pools](../data-model.md#61-pool-generation-inside-a-pool-stage), [§7 Brackets](../data-model.md#7-bracket-configuration-and-advancement-inside-a-bracket-stage), [§8 Matches](../data-model.md#8-match-generation) (`match_winner` / `match_loser` source types), [§10 Score reporting](../data-model.md#10-score-reporting-and-winner-resolution) (standings / tiebreakers).
- Sibling features: [`divisions.md`](divisions.md), [`teams.md`](teams.md), [`imports.md`](imports.md), [`brackets.md`](brackets.md), [`standings.md`](standings.md), [`schedule.md`](schedule.md), [`repairs.md`](repairs.md).
- Shell: [`../ui/shell.md`](../ui/shell.md) (route table).

## Future-wizard sections (engine ready, wizard deferred)

These formats are supported by the V1 **schema** but not by the V1 **wizard**. Listed here so the doc carries the contract that future work will build on without schema migration.

### Power Pool (multi-pool-stage formats)

A second pool-type Stage seeded from the first stage's **FinishPosition** instead of from **Ranking**. Engine support:

- `stages.seeding_source = 'prior_stage_finish_positions'` on the second pool stage.
- A future wizard mode adds a Step 1.5 — "Power Pool" — between today's Steps 1 and 2, with its own pool-structure editor and a "Source: results of Stage 1" callout.
- No new tables; no migrations. The wizard is the only deliverable.

### Challenge Phase (inter-bracket play-in matches)

A short set of matches between the lower seeds of a higher bracket and the top seeds of a lower bracket, played "if we have time", whose winners move up and losers move down. Engine support:

- `match_participant_sources.source_type IN ('match_winner', 'match_loser')` ships in V1 even though no V1 wizard surface writes those values.
- A future wizard adds a "Challenge Phase" affordance between Step 2 (Bracket Structure) and the final lock, allowing the user to author a small list of challenge matches whose `match_winner` / `match_loser` outputs feed designated bracket slots.

### Multi-stage divisions beyond 1+1

The data model places **no cap** on the number of stages per division. Future wizards can extend the progress bar to N steps for formats with preliminary qualifying pools, multi-round bracket play, or experimental crossover stages.

## Gap from current V1 implementation

The shipped page `DivisionDetail.razor` in the Web.Shared project (`/divisions/{DivisionId}`) is a **two-column prototype**: Rankings on the left + tabbed **Pools** / **Brackets** on the right with **Gold/Silver** advancement and GUID override text fields. It does **not** yet model:

- the `stages` table or `stage_id` on matches,
- the **structure-first** wizard (Pool Structure → Bracket Structure → Advancement Rules) with progress bar and placeholder labels,
- explicit **Stage status** lifecycle (`draft` / `configured` / `seeded` / `in_progress` / `completed`) or blocking-reason strips,
- generic **AdvancementRules** with `order_by` and wildcard pool expansion (today's `topNPerPool` + `gold` / `silver` GUID overrides are hard-coded; the target migrates them to wildcard rules per pool finish position),
- the persistent left-side **materialize actions** ("Snake Rankings into Pools", "Resolve Bracket Slots") as idempotent buttons,
- **BracketSlotOverride** as a first-class concept (today: free-text GUID fields; target: drag-from-rankings + audit-logged `bracket_seed_slot_overrides`),
- single-source **Ranking** (today: `Team.SeedHint` on `tournament_teams.seed` collapses both ranking and bracket seeding hints),
- **`tiebreaker_preset`** on standings preview (today: W-L only).

Work moves from that prototype toward this document and [`../data-model.md`](../data-model.md) without deleting the route — the URL stays stable while the UI is rebuilt.
