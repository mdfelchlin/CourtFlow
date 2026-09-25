# Data Model

> **Status:** Implemented schema. This document is the canonical source of truth for CourtFlow's data layer. All aggregates, entities, and enums described here have corresponding domain classes and EF Core configurations. Companion docs: [`architecture.md`](architecture.md), [`architectural-prd.md`](architectural-prd.md), [`product-overview.md`](product-overview.md).

This document defines the data-layer scope for an admin-first volleyball tournament management application focused on organizer setup, division configuration, pool and bracket construction, scheduling, result reporting, and operational flexibility during tournament changes.

## Purpose

The data model separates four concerns: master data, event participation, competition structure, and schedule execution. This separation is what enables flexible repairs such as team withdrawals, court outages, referee reassignment, and schedule reflow without corrupting historical team, club, or tournament data.

The model aligns with **Domain-Driven Design**: aggregates are organized around business invariants and transaction boundaries rather than around UI pages or a flat relational diagram.

## Aggregates and bounded contexts

The model is grouped into the following aggregates and contexts. Each aggregate root owns a transactional consistency boundary; cross-aggregate references use ids only and are reconciled by domain services.

| Bounded context | Aggregate roots | Notes |
|-----------------|-----------------|-------|
| Identity & tenancy | `Organization`, `User` (with `OrganizationUser` membership), `League` | Master data partitioning all other aggregates by `OrganizationId`. |
| Facilities | `Venue` (owns `Court`) | Reused across tournaments. V1 assumes each court is available all day for every tournament day; per-window availability is deferred (see §2). |
| Tournament setup | `Tournament` (owns `TournamentVenue`, `Wave`, settings), `TournamentTeam`, `TournamentTemplate` (owns `TemplateDivision`, `TemplateStage`, `TemplateAdvancementRule`, `TemplateWave`) | Event shell; team participation is event-scoped. Templates capture the structural subset of a tournament for reuse. |
| Competition structure | `Division` (owns `DivisionRanking`, `PoolSettings`, `Pool`+`PoolEntry`, `PoolCourtAllocation`, `Bracket`+`BracketSeedSlot`+`AdvancementRule`) | Carries the competition rules per division. |
| Match execution | `Match` (owns `MatchParticipantSource`, `MatchOfficialAssignment`, `MatchResultReport`, `MatchSet`) | Match identity is stable across reschedules. `matches.started_at` / `matches.completed_at` are the **Match clock** anchors used by the Court Status & Delay Dashboard. |
| Scheduling | `ScheduleVersion` (owns `MatchAssignment`, diagnostics) | Versioned plans that resolve to a published current schedule. |
| Operations | `RepairJob` (owns `RepairAction`), `CourtOutage`, `ImportJob` (owns `ImportRow`) | First-class repair, outage, and import history. The `work_team_reassignment` action type writes to `match_officials` under a RepairJob. |
| Master entities | `Club`, `Team`, `Player` | Long-lived records reused across events. |

Aggregate roots carry a concurrency token; references between aggregates use foreign-key ids and are never navigated as object graphs across aggregate boundaries.

## Core design layers

| Layer | Purpose | Main records |
|---|---|---|
| Master data | Long-lived records reused across events. | `organizations`, `users`, `venues`, `courts`, `clubs`, `teams`, `players` |
| Event participation | One team's presence in one tournament/division. | `tournament_teams`, `organization_users`, `league_tournaments` |
| Competition structure | Event-specific pools, brackets, rankings, match definitions. | `tournament_divisions`, `division_rankings`, `pools`, `pool_entries`, `brackets`, `bracket_seed_slots`, `advancement_rules`, `matches` |
| Schedule execution | Assignments, score reporting, repairs, publish/version history. | `schedule_versions`, `match_assignments`, `match_sets`, `match_result_reports`, `match_officials`, `match_official_overrides`, `repair_jobs` |

## Workflow and required data

### 1. Account and organization setup

The first workflow step creates the organizer identity and organization boundary. These records partition data ownership and later control which users can manage tournaments, leagues, and venues.

Required records and fields:
- `users`: `id`, `email`, `display_name`, auth fields, status.
- `organizations`: `id`, `name`, `slug`, `timezone`, default settings.
- `organization_users`: `organization_id`, `user_id`, role, invited/accepted timestamps.
- `leagues`: `id`, `organization_id`, `name`, `season_label`, `starts_on`, `ends_on`, `ranking_method`, `is_active`.

Recommended additions:
- Organization defaults for match duration, break duration, ref assignment style, and preferred ranking/tiebreak settings.
- Audit columns on all top-level tables: `created_at`, `updated_at`, `created_by_user_id`, `updated_by_user_id`.

### 2. Tournament creation

Tournament creation defines the event shell and facility context before competitive structure exists.

**V1 scheduling assumptions:**
- Each **court** linked to the tournament is treated as **available all day** on every calendar day from `start_date` through `end_date`. Per-court availability windows are deferred (see below).
- **Waves** are limited to **`AM`** and **`PM`** session codes when `session_mode = MultiWave`. Custom numbered waves (`1`, `2`) and wave end times are out of scope for V1.

Required records and fields:
- `tournaments`: `id`, `organization_id`, `league_id nullable`, `name`, `slug`, `status`, `start_date`, `end_date`, `timezone`, `session_mode`, `default_start_time nullable`, `notes`, `published_at nullable`, `archived_at nullable`.
  - `status` controls **public visibility** (see [Tournament status lifecycle](#tournament-status-lifecycle) below). All other tournament fields are independent of the publish gate.
  - **`session_mode`**: `SingleSession` (one start time for the whole tournament) or `MultiWave` (separate AM/PM start times). New tournaments default to `SingleSession`.
  - **`default_start_time`**: local `HH:mm` used when `session_mode = SingleSession`. Ignored in multi-wave mode.
- `tournament_venues`: `tournament_id`, `venue_id`, `display_order`.
- `venues`: `id`, `organization_id`, `name`, address fields.
- `courts`: `id`, `venue_id`, `name`, `sort_order`, `is_active`.
- `waves`: `id`, `tournament_id`, `code`, `display_order`, `default_start_time nullable`.
  - Used when `tournaments.session_mode = MultiWave`. The tournament editor auto-maintains **`AM`** and **`PM`** rows with start times; organizers do not add, delete, or rename waves manually in V1 UI.
  - **`code`** is `AM` or `PM` — the short label on the schedule grid and in CSV import. Unique per tournament.
  - **`default_start_time`** is the local `HH:mm` start for that wave's matches. Combined with `tournaments.timezone` and the tournament's calendar dates when scheduling. V1 does not model wave end times; the scheduler assumes courts are available for the rest of the day after the wave start.
  - Teams and matches carry a `Wave` (`AM` / `PM`) for scheduling within a division; divisions themselves do not pin to a wave.
  - CSV import `Wave` values must be `AM` or `PM`; missing waves are auto-created during import and the import-result panel notes any auto-created wave so the organizer can set start times in the tournament editor **Match start times** section. See [`features/imports.md`](features/imports.md).

Deferred (not required for V1):
- `court_availability_windows`: `id`, `court_id`, `tournament_id nullable`, `starts_at`, `ends_at`, `is_available`, `reason` — future support for partial-day court closures outside live **CourtOutage** repairs.
- Wave **`default_end_time`** and **`notes`** columns on `waves` (schema may retain nullable columns; V1 UI and scheduler ignore them).
- Custom wave codes beyond `AM` / `PM` (e.g. numbered `1` / `2` sessions).

Recommended additions:
- `tournament_settings` or owned settings on `tournaments` for scheduling defaults, publish rules, and score-entry policy.
- An organization-level `auto_archive_days` integer (default `14`) controlling the auto-archive policy described below.

#### Tournament status lifecycle

`tournaments.status` is the **public-visibility gate**. It is distinct from any per-aggregate readiness (a `published` ScheduleVersion, a `seeded` bracket Stage, etc.) which gate inner content of an already-public tournament. The two layers compose:

1. **Tournament-level gate (this lifecycle):** does the tournament appear publicly at all?
2. **Per-tab readiness gates** (documented in [`features/public-event.md`](features/public-event.md)): inside a public tournament, which content surfaces are ready to show?

| Status | Public visibility | Editable on admin | Default |
| --- | --- | --- | --- |
| `draft` | **Invisible.** Hidden from `GET /public/events`. Deep-link `/events/{id}` returns 404 (generic — does not leak existence). | Fully editable. | Yes — the state every new tournament starts in. |
| `published` | **Visible.** Appears on `/`; `/events/{id}` works. Each public tab inside the event shows real content per its readiness gate, or the documented empty-state copy when its underlying data is not yet ready. | Editable subject to per-aggregate rules (matches in progress require **RepairJob**, etc.). | No |
| `archived` | **Visible only as a past event.** Appears in a separate "Past events" section on `/` (or, per organization settings, can be hidden entirely while leaving `/events/{id}` reachable for direct links). | Read-only except via **RepairJob** or explicit un-archive. | No |

**Transitions** (with side effects):

- `draft → published` — organizer clicks **Publish to public** in the tournament editor. Writes `published_at = now()`. Emits `TournamentPublished` domain event.
- `published → draft` — organizer clicks **Move to draft**, confirms the modal "This will remove the tournament from the public landing and break shared links. Continue?". Clears `published_at`. Emits `TournamentUnpublished`. Allowed but flagged because shared links break.
- `published → archived` — manual click (**Archive tournament**) or automatic via the **Auto-archive policy** below. Writes `archived_at = now()`. Emits `TournamentArchived`.
- `archived → published` — un-archive. Clears `archived_at`. The original `published_at` is preserved.
- `draft → archived` — not exposed in V1 UI (no meaningful use case; archive a `draft` tournament by deleting it).

**Auto-archive policy:**

- A background job (or on-read fallback for environments without one) transitions `published → archived` for any tournament whose `end_date + organization.auto_archive_days < today`.
- Default `auto_archive_days = 14`. Editable per organization.
- The policy is **soft**: it does not delete data; it only flips the status. Un-archiving immediately restores the tournament to `published` with its prior public surface intact.
- Tournaments that are still mid-play after `end_date` (matches in progress) are skipped by the auto-archive job; manual archive is the only path out for those.

**Why `status` is not on `tournaments.is_public` (a boolean):** the three-state enum keeps "currently live", "historical, still browsable", and "private build-in-progress" semantically distinct in the schema and the API. It also gives us a clean home for `published_at` / `archived_at` audit columns that a boolean would require side-tables to capture.

### 3. Division creation

A division is the organizer-defined competitive unit inside a tournament. **Create** requires only a **name**; format and max teams use server defaults (tournament play format when set, otherwise pool + bracket; max teams 32). Edit can adjust max teams. AM/PM scheduling is assigned per team or match later, not at division create.

Required records and fields:
- `tournament_divisions`: `id`, `tournament_id`, `name`, `format`, `max_teams`, `tiebreaker_preset`, `status`.
- Division format settings: `best_of_sets`, `guaranteed_matches`, `pool_play_enabled`, `bracket_play_enabled`, `match_duration_minutes`, `break_duration_minutes`.
- Division scheduling settings: `target_court_count`, `preferred_start_time`, `latest_end_time`, `priority`, `allow_cross_venue`.
- Division seeding settings: `ranking_mode`, `seeding_method`, `manual_override_allowed`.
- Division standings settings: `tiebreaker_preset` — one of `WINS_H2H_SETS_POINTS` (USAV default), `WINS_SETS_POINTS` (most common organizer choice), `SETS_POINTS` (round-robin / jamboree), or `H2H_SETS_POINTS` (specialized formats). The full chain semantics live in §10. Set and point comparisons are always **ratio**-based; differential is not exposed in V1.

Recommended additions:
- **Division checklist status (V1):** not a persisted write column. Coarse progress (`draft`, `teams_loaded`, `seeded`, …) is **derived** by the **DivisionReadinessProjection** read model from `stages.status` and team counts — see [`adr/0003-status-lifecycle-per-aggregate.md`](adr/0003-status-lifecycle-per-aggregate.md). Stage-level progress remains authoritative on `stages.status`.

### 4. Team import and event entry

CSV import creates or matches master team records and then creates event-specific entries tied to the tournament and division. The scheduler never relies only on global `team_id` because a persistent team may appear in multiple tournaments and divisions over time.

Required records and fields:
- `import_jobs`: `id`, `organization_id`, `tournament_id`, file metadata, status, created by, completed at.
- `import_rows`: row number, raw values, parse status, validation errors, matched club/team IDs.
- `clubs`: `id`, `organization_id`, `name`, normalized name.
- `teams`: `id`, `club_id`, `name`, normalized name, external ids.
- `tournament_teams`: `id`, `tournament_id`, `division_id`, `team_id`, `entry_status`, `checked_in_status`, `notes`.
  - **No `seed` column.** Seeding input is the **Ranking** in `division_rankings` — the single source of truth (see [§5](#5-ranking-and-seeding)). Imports that historically carried a `SeedHint` column (predefined-pools CSV) or a `Rank` column (ranked-teams CSV) now write directly into `division_rankings.rank` for that division.
  - Pool / bracket placement of a TournamentTeam lives in `pool_entries` and `bracket_seed_slots.resolved_tournament_team_id` respectively, **not** denormalized on `tournament_teams`.

Recommended additions:
- `tournament_team_status` values such as `active`, `withdrawn_pre_schedule`, `withdrawn_post_schedule`, `disqualified`, `completed`.
- Import reconciliation data so organizers can repair duplicates or mis-matched clubs without manual SQL.

### 5. Ranking and seeding

Ranking is the **single source of truth** for seeding input within a Division. There is no parallel "seed hint" column on `tournament_teams`. Rankings are event-scoped and tied to `tournament_teams`, not to permanent team records.

Required records and fields:
- `division_rankings`: `id`, `division_id`, `tournament_team_id`, `rank`, `source`, `is_manual_override`, `updated_by_user_id`, `updated_at`.
- Division seeding metadata: `seeding_method`, `randomization_seed nullable`, `snake_seed_enabled`, `club_separation_mode`.

Source of truth and write paths:
- **CSV import (ranked-teams)** writes `division_rankings.rank` per row (`source = 'import'`).
- **CSV import (predefined-pools)** that carries a `SeedHint` column writes `division_rankings.rank` derived from the per-division ordering of SeedHint values (`source = 'import_seed_hint'`).
- **Manual reorder** in the Division detail Rankings panel writes new `rank` values (`source = 'manual'`, `is_manual_override = true`).
- **Random fill** (a "Randomize" affordance) writes ranks from a deterministic shuffle with stored `randomization_seed` (`source = 'random'`).

Editability:
- `division_rankings` is editable while every stage in the division is at `draft`, `configured`, or `seeded` and no match has started. Once any stage transitions to `in_progress`, rankings are read-only unless touched by a **RepairJob**.
- The Ranking is decoupled from `tournament_teams` insertion order so that re-imports replace it cleanly without touching team entries.

Recommended additions:
- Ranking change audit table with old rank, new rank, reason, and actor.
- Constraints for unique `(division_id, rank)` and unique `(division_id, tournament_team_id)`.

### 6. Stages (the round container)

A **Stage** is the round of competition organizers describe in plain English: a "pool round" or a "bracket round". Each Stage has a **StageType** and contains one or more leaf entities of that type — Pools when `pool`, Brackets when `bracket`. A single Stage never mixes the two. "Gold + Silver" is one Stage of type `bracket` containing two Brackets, not two stages.

Required records and fields:
- `stages`: `id`, `division_id`, `stage_number`, `stage_type` (`pool` | `bracket`), `name`, `status`, `seeding_source`.
- Stage **Status** values (five-step lifecycle): `draft` → `configured` → `seeded` → `in_progress` → `completed`. See **Stage status lifecycle** below.
- **`seeding_source`** enum (the engine primitive that makes Power Pool support a future-wizard concern, not a schema concern):
  - `division_rankings` — placement reads from the division's **Ranking**. Default for the first pool-type Stage of a division and for any bracket-only division.
  - `prior_stage_finish_positions` — placement reads from an earlier stage's **FinishPosition**. Required for a second pool-type Stage (Power Pool) sourcing from the first; deferred from the V1 wizard but supported by the engine.
  - Bracket stages do not read this field directly — their slots are filled by `advancement_rules` (when a pool stage precedes) or by direct **Ranking** placement (when no pool stage precedes). `seeding_source` is informational for bracket stages.

Constraints:
- `stage_number` is unique per `division_id` and contiguous (1..N).
- No two stages in the same division share the same number.
- A division's `pool_play_enabled` / `bracket_play_enabled` flags are derived from whether stages of each type exist; they remain on `tournament_divisions` for display and import defaults but stop being the source of truth for "does this division have brackets?".
- `stages.seeding_source = 'prior_stage_finish_positions'` requires at least one earlier pool-type stage in the same division (validated on transition to `configured`).

V1 wizard scope:
- The Division detail wizard ships with **at most one pool-type Stage and at most one bracket-type Stage** per division (the most common 1+1 format). The schema places no such cap; Power Pool, multi-pool-round, and challenge-stage formats are unlocked by future wizard work without migration.

Recommended additions:
- A `parallelism_group` integer (default = `stage_number`) used by the scheduler to mark stages that run in parallel; reserved for a future feature where two stages of the same division run simultaneously (rare in volleyball).
- Stage-level diagnostics: generator version, settings hash, warnings — same shape as pool generation diagnostics.

#### Stage status lifecycle

Each **Stage** has its own **Status** and **Lifecycle** independent of `tournament_divisions.division_status`. Division-level status is coarse (`draft`, `teams_loaded`, `seeded`, `stages_in_progress`, `completed`); stage-level status drives what the Division detail workspace allows for that round.

| Status | Meaning | What is editable | Transitions out |
| --- | --- | --- | --- |
| `draft` | Stage exists; settings not finalized or leafs not yet valid. | Stage name, `stage_type` (only if no leafs yet), `seeding_source`, pool/bracket settings including `pool_count` and `target_size` per pool, advancement rules, delete stage (if allowed by division rules). All edits with **no team references** required. | → `configured` when settings validate (pool counts, bracket capacity, advancement prerequisites satisfied, every bracket slot has a rule / wildcard / explicit BYE). |
| `configured` | **Structure complete; teams not yet placed.** Pools exist with their `target_size`, brackets exist with their slots, advancement rules are validated. This is the explicit **pre-build** state used when an organizer designs the tournament shape before teams have registered or been confirmed. | Same as `draft` (drag-reorder pools, resize pools, edit advancement rules). Plus **"Snake Rankings into Pools"** and **"Resolve Bracket Slots"** actions become available when their inputs exist. Empty pool slots and bracket slots render as **placeholder labels** (`Pool A · 1`, `Gold-1 ← Pool A · 1st`). | → `seeded` when every **TournamentTeam** that should compete in this stage is assigned to a pool entry (pool-type) or every bracket slot has a `resolved_tournament_team_id` or BYE (bracket-type). |
| `seeded` | Structure complete **and** team placement complete; no match in this stage has started. | Team-to-pool moves (swap `pool_entries`), team-to-bracket-slot nudges via **BracketSlotOverride** (write `manual_tournament_team_id`), **AdvancementRule** edits that re-resolve unpinned slots, tiebreaker display preview. | → `in_progress` when the first **Match** in this stage leaves `not_started`. **Rollback:** → `configured` with explicit organizer confirm ("discard current team placements; keep pool / bracket structure") — allowed only while still `seeded` (no match started). |
| `in_progress` | At least one match started in this stage. | Structure read-only; scoring continues. Structural changes require a **RepairJob** ([§11](#11-repair-and-live-adjustment)). | → `completed` when every **Match** in the stage is `final` (or `canceled` with resolution) and pool-type stages have written **`finish_position`** on all `pool_entries` that require it for downstream advancement. |
| `completed` | Terminal for this stage. | Read-only. | (none) |

**Gating principle:** disabled primary actions on the Division detail UI must show an inline **blocking reasons** list (not only a disabled button) — see [`features/division-detail.md`](features/division-detail.md).

**Cross-stage ordering:** `stage_number` defines pipeline order. **AdvancementRules** always reference a `source_stage_id` strictly less than the `target_bracket_id`'s owning stage's `stage_number` (for pool→bracket flows) or same-stage bracket cross-links (double elimination) as modeled in [§7](#7-bracket-configuration-and-advancement-inside-a-bracket-stage).

### 6.1. Pool generation (inside a pool Stage)

Pool settings live on the Stage (not directly on the Division) so divisions with two pool stages can configure each independently. Pools themselves are owned by the Stage.

Required records and fields:
- `pool_settings`: `stage_id`, `pool_count`, `teams_per_pool_target`, `pool_size_strategy`, `play_style`, `court_allocation_mode`, `ref_assignment_mode`.
- Pool generation constraints: avoid same-club-in-pool, max same-club count, balance seed spread, preserve manual ranking order.
- `pools`: `id`, `stage_id`, `name`, `sequence`, `target_size`, `status`.
  - **`target_size`** is the declared capacity of the pool (how many teams it will hold). Set when the pool is created (default = `pool_settings.teams_per_pool_target`). Used to render placeholder slots when no teams are placed yet.
  - **Empty pools are valid in `draft` and `configured`.** A pool with zero `pool_entries` is rendered with `target_size` placeholder slots (`Pool A · 1`, `Pool A · 2`, …) so organizers can build the structure before teams exist.
- `pool_entries`: `id`, `pool_id`, `tournament_team_id`, `seed_in_pool`, `finish_position nullable`.
  - `seed_in_pool` is the slot index (1..`target_size`) the team occupies inside the pool. Frozen once the stage reaches `seeded`.
- `pool_court_allocations`: `id`, `pool_id`, `court_id`, `allocation_type` (`preferred` | `required`), `priority_order`.
  - **`preferred`** courts are tried first by the scheduler; matches fall through to other tournament courts if all preferred are busy.
  - **`required`** courts are hard constraints; the scheduler will leave a match unassigned rather than place it on a non-required court.
  - **Empty allocation** is valid and is the default — the scheduler then falls back to `tournament_divisions.target_court_count` and tournament-level court availability, which is also the behavior today (V1 implementation has no per-pool allocation UI; see [`features/division-detail.md`](features/division-detail.md#step-1--pool-structure) for the target courts editor).
  - The editor lives on the Division detail Step 1 pool card; changes are mutable in `draft`, `configured`, `seeded` Stage status and require a **RepairJob** (court outage path) in `in_progress`.

Two explicit, idempotent actions on the Division detail surface drive the materialization:

- **Snake Rankings into Pools** — reads `division_rankings` and the pool structure (`pool_count`, each pool's `target_size`) and writes `pool_entries` according to the configured `seeding_method` (snake by default). Re-runnable; warns when it would overwrite an existing layout that has been manually adjusted. See [`features/division-detail.md`](features/division-detail.md).
- **Resolve Bracket Slots** — described in [§7](#7-bracket-configuration-and-advancement-inside-a-bracket-stage).

Recommended additions:
- Pool generation diagnostics: algorithm version, settings hash, warnings, balancing notes.
- `pool_standings_snapshots` only as a cache or audit artifact, not the primary source of truth.

Migration note: the V1 code base owns pools under `division_id` and has no `target_size`. The target model owns pools under `stage_id` and adds `target_size`. Migration creates one implicit Stage (`stage_number = 1`, `stage_type = pool`) per division that has pools today, re-parents, and back-fills `target_size` from the current `pool_entries` count or `pool_settings.teams_per_pool_target`.

### 7. Bracket configuration and advancement (inside a bracket Stage)

Bracket settings live on the Stage. Brackets are owned by the Stage; one Stage can hold multiple Brackets (Gold + Silver, Championship + Consolation, etc.). Bracket structure (slot count, slot order, advancement wiring) can be authored **before teams exist** — `bracket_seed_slots` rows are created at bracket-generation time with `resolved_tournament_team_id = NULL`, and stay null until **Resolve Bracket Slots** runs.

Required records and fields:
- `brackets`: `id`, `stage_id`, `name`, `type`, `team_count`, `sequence`, `status`.
- `bracket_seed_slots`: `id`, `bracket_id`, `seed_position`, `label`, `expected_source_type`, `advancement_rule_id nullable`, `manual_tournament_team_id nullable`, `resolved_tournament_team_id nullable`.
  - **`seed_position`** is the bracket-internal position (1..`team_count`). Wins or losses route to subsequent rounds via the rules implicit in `brackets.type`.
  - **`expected_source_type`** enum: `advancement_rule` (filled by a rule that resolves into this slot), `manual` (an organizer chose the team directly), `bye` (no team — counts as a free pass to the next round).
  - **`advancement_rule_id`** is the rule that the resolver should consult for this slot. When a wildcard rule produces *P* teams, the resolver writes the same `advancement_rule_id` on *P* consecutive slots and the within-rule ordinal disambiguates which produced team goes where.
  - **`manual_tournament_team_id`** is a **BracketSlotOverride** — an organizer-pinned team for this slot. When set, it always wins over `advancement_rule_id` at materialization (see Resolve Bracket Slots below). Persists across rule re-runs.
  - **`resolved_tournament_team_id`** is the materialized result of the resolver (the team that actually plays in this slot). Recomputed by **Resolve Bracket Slots**; null until then.
- `bracket_seed_slot_overrides` (audit): `id`, `bracket_seed_slot_id`, `previous_manual_tournament_team_id nullable`, `new_manual_tournament_team_id nullable`, `actor_user_id`, `reason nullable`, `at`. Append-only; surfaces "who put team X into Gold-1 and when" without snapshotting the whole bracket.
- `advancement_rules`: `id`, `source_stage_id`, `source_pool_id nullable`, `source_finish_position`, `target_bracket_id`, `target_seed_slot`, `order_by`, `priority`.
  - **V1 scope is cross-stage flow only.** Sources are always a prior pool-type Stage. `source_pool_id` may be left null to mean "any pool in the source stage" (a wildcard rule that the generator expands by pool order or by tiebreaker rank, see below).
  - **`order_by`** enum (engine-level; wizard exposes via presets):
    - `pool_sequence` *(default)* — when expanding a wildcard rule across *P* pools, the resolver fills *P* consecutive bracket slots in `pools.sequence` order. Pool A's *k*-th finisher → first slot, Pool B's *k*-th finisher → second slot, … This is the **incoming-seed-preserved** behavior used by formats that distrust cross-pool point-differential ("if you win Pool A you're the 1-seed", per the customer's bid-tournament logic).
    - `tiebreaker_rank` — when expanding a wildcard rule, the resolver compares the *P* produced teams to each other via the division's **TiebreakerPreset** chain and fills slots in tiebreaker rank order (best first). Used by formats that want cross-pool comparison.
  - **Within-bracket flow is implicit in `brackets.type`.** Single elimination, double elimination (with optional grand-final reset, optional third-place match) all derive winner/loser pointers from the bracket type — they are not authored as advancement rules. Bracket-source columns (`source_bracket_id`, `source_bracket_result_kind`) are explicitly **deferred** beyond V1 and not added to the schema until a customer use case requires non-standard within-bracket wiring.
- Bracket settings: `format`, `third_place_match`, `grand_final_reset`, `consolation_style`, `seed_style`.

Recommended additions:
- A bracket template/version concept if organizers may try different bracket layouts before publishing.
- Validation records for incomplete advancement mappings before bracket generation is allowed.

#### Wildcard rule expansion

A rule with `source_pool_id = NULL` and `source_finish_position = k` means "the *k*-th place finisher of **every** pool in the source stage feeds into this target bracket". Expansion at materialization time (when **Resolve Bracket Slots** runs):

- Source stage has *P* pools.
- The wildcard rule's `target_seed_slot` is the **starting** slot; the resolver consumes *P* consecutive slots in the target bracket.
- The *P* produced teams are ordered by the rule's `order_by`:
  - `pool_sequence` — by `pools.sequence` ascending (Pool A's *k*-th finisher first, Pool B's *k*-th finisher second, …).
  - `tiebreaker_rank` — by the division's **TiebreakerPreset** chain applied to the *P* teams (best first; ties broken by the chain's stable fallback).
- Explicit rules (non-null `source_pool_id`) outrank wildcard rules at the same `target_seed_slot` via `priority` (lower number wins).

This is what makes "top 2 from each pool → Gold" expressible as **two rules** (one wildcard for 1st place, one for 2nd place) rather than 2×*P* rules.

#### Resolve Bracket Slots (the materialization action)

A single idempotent action on the Division detail surface that walks each bracket's `bracket_seed_slots` and writes `resolved_tournament_team_id` per the precedence:

1. If `manual_tournament_team_id` is set (**BracketSlotOverride**), use it directly.
2. Otherwise, if `advancement_rule_id` is set and the source stage has `finish_position`s for all participating teams, run the rule and write the resolved team.
3. Otherwise, leave `resolved_tournament_team_id = NULL` and report the slot in the resolver diagnostics ("Gold-3: source stage Stage 1 not yet completed").

Re-runnable. Re-running preserves all `manual_tournament_team_id` values; only rule-resolved slots are recomputed.

#### Validation before bracket generation

A bracket cannot be **generated** (rows in `bracket_seed_slots` materialized) until:
- The bracket's `team_count` is set.
- Each future slot is either covered by an advancement rule, intended for a **BracketSlotOverride**, or marked as a **BYE** (`expected_source_type = 'bye'`).
- No two non-priority-ordered rules resolve to the same `target_seed_slot`.
- The source stage exists in the same division and has `stage_number` < the bracket stage's `stage_number`.

A bracket can be **resolved** (write `resolved_tournament_team_id`s) only when:
- The source stage is at `completed` (every match `final`; `finish_position` set on every `pool_entry` referenced by a rule) — *or* all referenced slots have `manual_tournament_team_id` (override-only bracket).

Violations surface as **blocking reasons** on the downstream bracket Stage card per [`features/division-detail.md`](features/division-detail.md).

Migration note: today's `brackets.division_id` becomes `brackets.stage_id` pointing at a single implicit bracket Stage per division that has brackets. The current "Gold / Silver" generator output becomes two Brackets inside one Stage, with two wildcard `advancement_rules` (1st place → Gold `order_by = pool_sequence`, 2nd place → Silver `order_by = pool_sequence`) reproducing the today behavior of `topNPerPool = 2`. Today's manual GUID override fields on `DivisionEdit.razor` migrate to `bracket_seed_slots.manual_tournament_team_id` with an entry in `bracket_seed_slot_overrides`.

### 8. Match generation

Matches are the operational center of the system once competition structure exists. Match identity is stable even if court assignments change later.

Required records and fields:
- `matches`: `id`, `tournament_id`, `division_id`, `stage_id`, `pool_id nullable`, `bracket_id nullable`, `status`, `best_of_sets`, `match_order`, `winner_to_match_id nullable`, `loser_to_match_id nullable`, `started_at nullable`, `completed_at nullable`.
  - `stage_id` is the canonical reference for "which round does this match belong to" and replaces the previous `stage_type` enum on `matches`.
  - Exactly one of `pool_id` / `bracket_id` is non-null and must be a leaf of `stage_id`.
  - **`started_at`** and **`completed_at`** form the **Match clock** — the actual wall-clock anchors used by the Court Status & Delay Dashboard. Distinct from `MatchAssignment.scheduled_start` (the *plan*) and from the `match_status` lifecycle (which is coarse). Written by the explicit **Start match** / **Mark match complete** actions and by auto-fallback when scoring arrives without a prior Start press (see [Match clock writes](#match-clock-writes)).
- `match_participant_sources`: `id`, `match_id`, `side`, `source_type`, `source_reference_id`, `resolved_tournament_team_id nullable`.
  - **`source_type`** enum (engine-level; V1 wizard exposes a subset):
    - `pool_finish_position` — `source_reference_id` references a `(pool_id, finish_position)` pair. Used by pool-round matches that are scheduled before pool play completes (rare in V1) and by bracket-source materialization that goes through `bracket_seed_slots`.
    - `bracket_seed_slot` — `source_reference_id` references a `bracket_seed_slots.id`. Standard bracket round-1 match participants.
    - **`match_winner`** — `source_reference_id` references another `matches.id`; the winner of that match becomes this side. Standard bracket-tree wiring AND the path that enables future **Challenge Phase** support: a "challenge" match between specific teams whose winner feeds the next bracket round. Schema-level support is in V1 even though no V1 wizard surface authors challenge matches.
    - **`match_loser`** — `source_reference_id` references another `matches.id`; the loser of that match becomes this side. Used by double-elimination loser bracket wiring (derived from `brackets.type`) and by future "loser of challenge match drops to Silver" patterns.
    - `manual` — `resolved_tournament_team_id` is set directly with no upstream source.
  - `resolved_tournament_team_id` is the materialized result. Recomputed when its upstream source changes (bracket regenerate, pool close, etc.).
- Match metadata: expected duration, stage priority, scheduling flexibility score.
- `match_officials` (renamed from `match_official_assignments` in target schema): `id`, `match_id`, `work_team_tournament_team_id nullable`, `r1_tournament_team_id nullable`, `r2_tournament_team_id nullable`, `assignment_source`, `manual_assigned_at nullable`, `manual_assigned_by nullable`, `manual_reason nullable`.
  - **`work_team_tournament_team_id`** is the **WorkTeam** — the **TournamentTeam** responsible for fielding the officiating crew for this match (scorer, libero tracker, two line judges, and R2 when no separate `r2_tournament_team_id` is set).
  - **`r1_tournament_team_id`** is set when a separate **TournamentTeam** (e.g. an adult-staffed club team or an assignor pool) covers the first referee role. NULL when R1 is a paid official tracked outside the schema, or when the WorkTeam covers R1.
  - **`r2_tournament_team_id`** is set on the rare event that R2 is covered by a different team than the WorkTeam (e.g. 12U / 13U playoff matches that require an adult-Player-Referee R2 per region rules).
  - **`assignment_source`** enum: `auto_rotation` (computed from the division's `ref_assignment_mode` and the pool / bracket's match order — the default for fresh matches), `manual_override` (an organizer changed it on the Schedule or Matches surface), `repair` (changed by a **RepairAction** of kind `work_team_reassignment`).
  - **Constraints:** `work_team_tournament_team_id` can never equal either of the match's playing participants. `r1_tournament_team_id` and `r2_tournament_team_id` likewise can never equal either playing participant.
- `match_official_overrides` (audit, parallel to `bracket_seed_slot_overrides`): `id`, `match_id`, `previous_work_team_tournament_team_id nullable`, `new_work_team_tournament_team_id nullable`, `previous_r1_tournament_team_id nullable`, `new_r1_tournament_team_id nullable`, `previous_r2_tournament_team_id nullable`, `new_r2_tournament_team_id nullable`, `actor_user_id`, `repair_job_id nullable`, `reason nullable`, `at`. Append-only.

Recommended additions:
- Match numbers visible to admins and staff.
- A `bracket_round_number` field on bracket-stage matches for easier UI grouping (see **BracketRound** in `CONTEXT.md`).
- A `locked_from_reschedule` flag for matches already started or manually protected.

#### When matches are created

`matches` rows are **not** the operator's input — they are derived from competition structure plus team placement. There are two trigger points and one resolver:

**Trigger 1: pool play matches** are generated when a pool-type Stage transitions from `configured` → `seeded`. This happens at the end of the **Snake Rankings into Pools** action (or any other path that writes `pool_entries`):

- For each Pool, a full round-robin is generated: `N * (N − 1) / 2` matches where `N = COUNT(pool_entries)` for that pool.
- Each match has `stage_id = {pool stage}`, `pool_id = {pool}`, `bracket_id = NULL`, `status = 'not_started'`, `best_of_sets = division.best_of_sets`.
- Both `match_participant_sources` rows have `source_type = 'pool_finish_position'` is **not** used here (that source type is for downstream consumers); instead pool-play matches use `source_type = 'manual'` with `resolved_tournament_team_id` set directly from the `pool_entries.tournament_team_id` of each side. This is the only place V1 writes `manual` sources implicitly.
- A consistent intra-pool ordering policy (e.g. King-style or Standard round-robin pairings) writes `match_order` so the schedule has a stable default sequence inside each pool.
- Re-running **Snake Rankings into Pools** with the warning-confirm shown in [`features/division-detail.md`](features/division-detail.md) deletes the pool's existing `not_started` matches and regenerates them. Matches already `in_progress` or `final` block the regenerate; a **RepairJob** is required to touch them.

**Trigger 2: bracket-tree matches** are generated alongside `bracket_seed_slots` when the **Generate brackets** action runs on a bracket-type Stage:

- For each Bracket, the full tree implied by `brackets.type` and `team_count` is materialized:
  - **Round 1** match participants use `match_participant_sources.source_type = 'bracket_seed_slot'` with `source_reference_id` pointing at the slot. The pairing for round 1 follows the bracket type's standard seeding (1 vs N, 2 vs N−1, etc.).
  - **Round 2+** match participants use `source_type = 'match_winner'` (and `match_loser` for double-elimination loser-bracket wiring) with `source_reference_id` pointing at the upstream match. `winner_to_match_id` / `loser_to_match_id` are written on each round-1+ match to mirror the structure (denormalized for fast traversal).
  - Optional **grand-final reset**, **third-place match**, and **if-necessary final** matches are generated per bracket settings.
- `match_status = 'not_started'`. `resolved_tournament_team_id` on each participant source stays `NULL` until **Resolve Bracket Slots** runs (which writes `bracket_seed_slots.resolved_tournament_team_id`); the resolved values flow into the participant sources lazily at read time, not by separate writes.
- Re-running **Generate brackets** preserves `manual_tournament_team_id` overrides and regenerates only `not_started` rounds. Started rounds require a **RepairJob**.

**Resolver: participant materialization.** `match_participant_sources.resolved_tournament_team_id` is recomputed when its upstream source changes:

- `pool_finish_position` source: resolves when its `pool_entries.finish_position` is written (at pool close).
- `bracket_seed_slot` source: resolves when **Resolve Bracket Slots** runs.
- `match_winner` / `match_loser` source: resolves when the upstream match's `winning_tournament_team_id` / `losing_tournament_team_id` is written (at match completion).
- `manual` source: resolved at creation.

The scheduler ([§9](#9-scheduling-and-assignment)) does **not** generate matches — it consumes already-existing match rows and writes `match_assignments` to schedule them. This separation is what lets a tournament's match list be stable across schedule regenerations.

Engine note on Challenge Phase (future): a Challenge Phase is a small set of inter-bracket matches whose results route teams up or down between brackets in the same Stage. With `match_winner` / `match_loser` source types already in V1, the future wizard can author challenge matches without further schema changes — challenge matches are just additional `matches` rows whose downstream consumers (the next bracket round's `match_participant_sources` or the upper bracket's seed slots) point at them.

#### Match clock writes

`matches.started_at` and `matches.completed_at` are the system's wall-clock anchors. They are independent of `match_status`, which still walks `not_started → in_progress → final` for coarse-grained consumers; the clock columns are the precise values the Court Status & Delay Dashboard reads. Two write paths:

- **Explicit start.** The **Start match** action on the Schedule cell, Matches list row, or Court Status card writes `started_at = now()` and transitions `match_status = 'in_progress'` atomically. This is the recommended path for organizers who want accurate delta tracking. A symmetric **Mark match complete** action writes `completed_at = now()` and transitions `match_status = 'final'`, used only when the score is being entered after the fact.
- **Auto-fallback.** When a `MatchSet` row is created (i.e. the **ScoreDialog** **Save** is the first signal) and `matches.started_at` is still null, the score-write transaction also writes `started_at = now()` (or, optionally, `started_at = MatchAssignment.scheduled_start` if the organization sets `tournament_settings.match_clock_fallback = 'scheduled'`). Likewise, finalizing a match (`Finalize` checkbox on the ScoreDialog) writes `completed_at = now()` when null. This guarantees the clock columns are always set by the time downstream views need them, without requiring a separate Start press.

**Per-match delay delta** (used by the Court Status & Delay Dashboard chips):

```
delta_started_minutes = (started_at − match_assignments.scheduled_start) / 60s    when started_at IS NOT NULL
                      = (now() − match_assignments.scheduled_start) / 60s         when started_at IS NULL AND match_assignments.scheduled_start < now()
                      = NULL                                                       otherwise
```

Implementations of the Court Status & Delay Dashboard, Schedule cell clock chip, and public my-team strip **must** use a single **MatchClockDelaySignal** read model (see [`adr/0006-read-models-tiebreaker-delay-public-division-readiness.md`](adr/0006-read-models-tiebreaker-delay-public-division-readiness.md)): **NULL** means "exclude this match from positive-delta aggregation"; never coerce NULL to numeric zero for "ahead" semantics. A positive value means "started behind schedule"; a negative value means "started early". Aggregating across a court's `not_started` and `in_progress` matches produces the court-level "Court is N min behind / ahead" chip — see [`features/court-status.md`](features/court-status.md#delay-computation).

#### Work team auto-rotation

`match_officials.work_team_tournament_team_id` is written automatically by the **Snake Rankings into Pools** action (for pool matches) and **Generate brackets** action (for bracket matches), per the division's `pool_settings.ref_assignment_mode`. V1 modes:

- **`next_in_pool`** *(default for pool matches)* — for each pool, the WorkTeam for the *k*-th match in `match_order` is the **TournamentTeam** in that pool that is **not** playing match *k* and is sitting closest in the rotation (typically the team that just finished match *k − 1*). The last match's WorkTeam is the team that played match 1 (cyclic). For pools of 3 (only two playing teams per match, one sitting), the sitting team is the WorkTeam unambiguously. For pools of 4+, the algorithm prefers "team that just played" over "team that hasn't played yet" so the WorkTeam is warmed up.
- **`loser_of_prior_match_on_court`** *(default for bracket matches per JVA/USAV convention)* — for each bracket match in match-order, the WorkTeam is the loser of the prior bracket match on the same court. The first bracket match on each court inherits a `next_in_pool`-style WorkTeam from the source pool, or a manual choice if cross-pool / cross-division.
- **`manual_only`** — no auto-rotation; every match's WorkTeam is set explicitly by the organizer. Used by divisions that rely on paid officials or assignor pools.

`assignment_source = 'auto_rotation'` for matches written by these algorithms. The organizer can override any individual match's WorkTeam from the Schedule or Matches surface; the override writes `assignment_source = 'manual_override'`, audit-logs to `match_official_overrides`, and (when the change crosses bigger thresholds, e.g. "swap a whole pool's WorkTeams") wraps the writes in a **RepairJob** with `trigger_type = 'work_team_reassignment'` for the audit trail. See [`features/work-teams.md`](features/work-teams.md).

The auto-rotation runs at match-generation time so the assignments exist before the schedule is generated; the scheduler's conflict checker validates that a WorkTeam is never assigned to a match that overlaps with one of their own playing assignments on a different court.

### 9. Scheduling and assignment

Scheduling is versioned so the engine can generate, preview, repair, and publish plans without destroying history. Assignment data is separate from match identity so reassigning remaining games to another court is a controlled change rather than a destructive rewrite.

Required records and fields:
- `schedule_versions`: `id`, `tournament_id`, `scope_type`, `scope_id nullable`, `status`, `created_by_user_id`, `created_at`, `published_at nullable`, `superseded_at nullable`, `superseded_by_schedule_version_id nullable`.
- `match_assignments`: `id`, `schedule_version_id`, `match_id`, `court_id`, `scheduled_start`, `scheduled_end`, `assignment_status`.
- Scheduling constraints: min rest, no overlap, wave rules, venue restrictions, ref overlap rules, team conflict rules.
- Diagnostics: conflict count, unassigned match count, soft-constraint penalty score, explanation json.

Recommended additions:
- Manual override history with actor, old assignment, new assignment, and reason.
- A current effective assignment pointer for fast reads once a schedule version is published.
- Court outage records tied into the scheduling engine rather than only general availability windows.

#### Schedule version status lifecycle

`schedule_versions.status` walks four values; only one version per tournament is `published` at any moment.

| Status | Meaning | Editable | Transitions out |
| --- | --- | --- | --- |
| `draft` | A generated plan that has not been seen by the rest of the system. `match_assignments` exist but nothing else reads them. | `match_assignments` mutable (engine can refine, organizer can preview-edit). Generator may overwrite. | → `preview` when the organizer opens it in the schedule grid for review. → discarded (deleted) if abandoned. |
| `preview` | Organizer is reviewing — conflicts are surfaced, manual nudges allowed, but no consumer (Standings, public Event view, official schedule export) reads this version yet. | `match_assignments` mutable. | → `published` (commits this version as the current schedule). → discarded (delete) if the organizer cancels. |
| `published` | The **current** schedule. Public views, the Matches list, the Schedule grid, and integrations all read from this version. Exactly one per tournament at a time. | `match_assignments` mutable only via **Move** (which writes new rows; doesn't update in place once history is involved) and **RepairJob** actions. | → `superseded` when a newer version is published (the engine writes `superseded_at` + `superseded_by_schedule_version_id` on the old version atomically with the new one going `published`). |
| `superseded` | A prior published version, kept for audit and "undo this regeneration" workflows. | Read-only. | (none — terminal) |

**Publish semantics.** Publishing a draft / preview version (a) sets the new version's `status = 'published'` and `published_at = now()`, and (b) writes `superseded_at = now()` + `superseded_by_schedule_version_id = {new id}` on the previously-published version, all in one transaction. The handoff is atomic so consumers always see exactly one published version.

**Discard semantics.** A `draft` or `preview` version can be deleted outright if it has no downstream references. Once published, a version persists forever as `superseded` (after the next publish).

**Scope.** `schedule_versions.scope_type` / `scope_id` allow per-division or per-wave regenerations (e.g. "regenerate just the AM wave"). A scoped version replaces only the matches inside that scope when published, leaving other matches untouched.

### 10. Score reporting and winner resolution

The current product scope only needs enough scoring data to determine winners and support standings and bracket advancement. Set-level scores are worth storing now because tie-breaking and validation often depend on them.

Required records and fields:
- `match_result_reports`: `id`, `match_id`, `reported_by_user_id`, `reported_at`, `status`.
- `match_sets`: `id`, `match_id`, `set_number`, `home_score`, `away_score`, `winner_tournament_team_id`.
- On `matches`: `winning_tournament_team_id`, `losing_tournament_team_id`, `completed_at`, `forfeit_reason nullable`.
  - **`forfeit_reason`** is the canonical "this match was forfeited, not played" signal. Enum: `no_show`, `withdrew_mid_match`, `disciplinary`, `injury`, `other`, `double_forfeit`. Non-null implies `match_status = 'final'` and zero `match_sets` (or partial sets that don't count toward standings — see "Forfeit handling (V1 default)" later in this section). Written by `POST /matches/{id}:forfeit`, surfaced in [`ScoreDialog`](ui/dialogs.md#scoredialog)'s Forfeit tab, never inferred from score data.

Recommended additions:
- Result correction workflow with accepted report id, superseded report id, and correction reason.
- Status transition validation so only valid score shapes can mark a match complete.

#### Standings tally (per pool, per **TournamentTeam**)

The Standings page and the FinishPosition snapshot are computed live from `match_sets` and `matches`. There is no canonical denormalized standings table; `pool_standings_snapshots` (when introduced) is a cache only.

Per-team aggregates inside one **Pool**:

| Aggregate | Definition |
| --- | --- |
| `MP` (matches played) | Count of `matches` in the pool with `match_status = final` where the team is on either side. |
| `W` / `L` | `matches.winning_tournament_team_id` vs `losing_tournament_team_id`. |
| `SW` / `SL` | Count of `match_sets.winner_tournament_team_id = team` vs `<> team` across the team's matches in the pool. |
| `PF` / `PA` | `SUM(home_score)` / `SUM(away_score)` reoriented to the team's perspective in `match_sets`. |
| `set_ratio` | `SW / (SW + SL)`, undefined when `SW + SL = 0`. |
| `point_ratio` | `PF / (PF + PA)`, undefined when `PF + PA = 0`. |

Set and point comparisons are always **ratio**-based, never differential.

#### Tiebreaker presets

`tournament_divisions.tiebreaker_preset` selects the resolution chain. Four V1 presets (per [§3](#3-division-creation)):

| Preset | Resolution chain |
| --- | --- |
| `WINS_H2H_SETS_POINTS` | `W` desc → head-to-head record (subset rows) → `set_ratio` desc → `point_ratio` desc |
| `WINS_SETS_POINTS` | `W` desc → `set_ratio` desc → `point_ratio` desc |
| `SETS_POINTS` | `set_ratio` desc → `point_ratio` desc *(used when `W` is not the headline rank, e.g. fixed-set jamborees)* |
| `H2H_SETS_POINTS` | head-to-head record → `set_ratio` desc → `point_ratio` desc |

**Head-to-head rules.** When a head-to-head step applies and exactly two teams are tied at the prior step, head-to-head is `W` between those two. When three or more teams are tied, head-to-head is recomputed as a mini-pool over the matches between those teams (their `W`, then their `set_ratio`, then their `point_ratio` within that subset). If head-to-head still does not separate them, the chain advances to the next overall criterion.

**Forfeit handling (V1 default).** A forfeit counts as a `W` for the non-forfeiting team and contributes nothing to `SW`, `SL`, `PF`, or `PA`. This convention is documented as the V1 default; tournaments needing per-set forfeit credit are out of scope.

**Stable order.** When the chain still cannot separate teams, ties are broken by **Ranking** ascending (the team with the better `division_rankings.rank` is ordered first), then by `tournament_team_id` ascending as a deterministic final fallback. The Standings UI shows the resolved order without exposing the final tiebreaker fallback.

**Audit.** The standings response includes the `tiebreaker_preset` used so the UI can show which chain produced the order. When a result correction changes the standings, the previous order is preserved by audit data on `match_result_reports` and `repair_jobs`, not by snapshotting the whole standings table.

### 11. Repair and live adjustment

This is the area that differentiates the product from rigid tournament platforms. Team withdrawals, court outages, and referee reassignment are modeled as first-class repair operations with history, not as silent edits.

Required records and fields:
- Team withdrawal data on `tournament_teams`: `withdrawn_at`, `withdrawal_reason`, `effective_after_match_id nullable`.
- `repair_jobs`: `id`, `tournament_id`, `division_id nullable`, `trigger_type`, `created_by_user_id`, `created_at`, `status`, `preview_diagnostics_json nullable`.
- `repair_actions`: `repair_job_id`, `action_type`, target ids, before/after payload.
- `court_outages`: `court_id`, `starts_at`, `ends_at`, `reason`.
- Reassignment records: old assignment, new assignment, affected match count, affected team count, penalty score.
- **`trigger_type`** enum (extended): `team_withdrawal`, `court_outage`, `referee_reassignment`, **`work_team_reassignment`**, **`schedule_reflow`**, **`pool_court_swap`**. `work_team_reassignment` covers any change to `match_officials.work_team_tournament_team_id` (or `r1_*` / `r2_*`) that crosses the org-configurable "audit threshold" (default: >1 match touched, or any change after the schedule is published). `schedule_reflow` covers organizer-initiated cascade shifts that aren't tied to a court outage or withdrawal — e.g. "Court 3 ran 20 min long; slide everything on that court 20 min". `pool_court_swap` covers "move all remaining `not_started` matches in Pool X from courts {A, B} to courts {C, D}" — distinct from `court_outage` (which says "this court is gone, scatter its matches") because it pins the moved matches to specific replacement courts and updates the structural `pool_court_allocations` so future regenerates use the new courts too. See [Pool court swap](#pool-court-swap) below.

Recommended additions:
- Freeze rules that prevent already-started or completed matches from being changed by repair jobs.
- Preview mode for repair plans before applying them — see [Reflow preview](#reflow-preview) below.

#### Reflow preview

The **Reflow preview** is a non-persistent simulation of a proposed change. It runs against the currently-loaded `ScheduleVersion`, computes the cascade, and returns a diff. No rows are written until the organizer applies the preview.

**Inputs to a preview:**
- The current `ScheduleVersion` and its `match_assignments`.
- The proposed change set — either a single edit (move match M to court C at time T) or a structured trigger payload (mark court C unavailable from `start` to `end`; withdraw TournamentTeam T; reassign WorkTeam on match M).
- The division's freeze rules (matches already `in_progress` or `final` are immovable; matches with `locked_from_reschedule = true` are immovable).

**Output of a preview:** a `ReflowPreviewDto` containing:
- The full diff of `match_assignments` (rows added, removed, mutated) keyed by `match_id`.
- The full diff of `match_officials` when work-team reassignment is part of the cascade.
- The conflict report (same shape as `schedule_versions.diagnostics_json`) for the post-apply state — counts of court conflicts, referee conflicts, wave conflicts, team rest-time conflicts.
- A human-readable summary string (e.g. "12 assignments shifted by 20 min on Court 3; 1 WorkTeam reassigned; no new conflicts").
- A `apply_token` that the apply step echoes back to prevent a stale preview from being committed against a schedule version that has since changed.

**Apply semantics:**
- If the loaded `ScheduleVersion.status = 'draft'` or `'preview'`, applying writes the diff onto that same version's `match_assignments`. The organizer can then **Publish** the version.
- If the loaded `ScheduleVersion.status = 'published'`, applying first forks a new `draft` version (copying the current `match_assignments`), writes the diff onto the fork, and leaves the previously-`published` version untouched. The fork is loaded into the schedule grid in `preview` state. This is the same fork-on-mutate rule that the schedule grid's Move action follows ([§9](#schedule-version-status-lifecycle), [`features/schedule.md`](features/schedule.md#versions)).
- The apply step **always** writes a `repair_jobs` row (with `preview_diagnostics_json` set from the preview output) and one or more `repair_actions` rows for the audit trail, regardless of whether the loaded version was `draft` or `published`. This is what differentiates Reflow preview's apply from a plain schedule generator pass — the audit chain is preserved.

**Stale-preview protection:** if the `apply_token` doesn't match the current state of the loaded `ScheduleVersion` at apply time (e.g. another organizer published a new version or scored a match in between), the server rejects the apply and returns the preview with a "re-run" diagnostic. The UI re-runs the preview and asks the organizer to confirm again.

The preview engine reuses the existing scheduler's conflict-check and reassignment logic in dry-run mode — same code path, just doesn't persist. See [`features/court-status.md`](features/court-status.md#reflow-preview) for the UI shape and [`features/schedule.md`](features/schedule.md#interactions) for how the schedule grid's Move dialog also uses this preview when the loaded version is `published`.

#### Pool court swap

A pool court swap is "the organizer wants to move a **Pool** from its current court set to a new court set, starting now, with all already-`final` matches staying where they were and all `not_started` matches relocating". It's distinct from a court outage because it pins the moved matches to **specific** target courts rather than scattering them.

**Trigger payload:**
- `pool_id` — the pool moving.
- `target_court_ids[]` — the new court set (one or more courts; the scheduler's pool placement logic picks among them for individual matches).
- `effective_from_match_order` *(optional, default 1 + max(match_order of any non-`not_started` match in the pool))* — matches with `match_order` ≥ this value are subject to the swap; earlier matches are untouched.

**Reflow preview output** (same shape as [Reflow preview](#reflow-preview)):
- Diff of `match_assignments` rows for the affected pool matches: rewritten `court_id` and possibly `scheduled_start` (when the target court has a conflict at the original time, the scheduler queues the match at the next available slot on a target court).
- Diff of `pool_court_allocations` rows: the new structural court list (preserves any existing `allocation_type` semantics; new courts default to `preferred`).
- Conflict report for the post-apply state — other divisions / pools on the target courts at the new times.
- `apply_token` for stale-preview protection.

**Apply semantics:**
- Writes the `pool_court_allocations` changes and the `match_assignments` changes inside one transaction.
- Always opens a `repair_jobs` row with `trigger_type = 'pool_court_swap'`, one `repair_actions` row per moved match (`action_type = 'match_assignment_move'`) and one or more `repair_actions` rows for the `pool_court_allocations` diff (`action_type = 'pool_court_allocation_change'`).
- If the loaded `ScheduleVersion.status = 'published'`, the apply forks a new `draft` version (same fork-on-mutate rule as the schedule grid's Move).

**Constraints:**
- The target court set must not be empty.
- Pool matches that are `in_progress` or `final` are immovable — the engine surfaces them in the preview but applies no diff to them.
- Cross-division check: the preview reports (but does not block) overlaps where a target court is already heavily used by another division at the new times — the organizer makes the call.

### 12. Tournament templates

Templates capture the structural subset of a real **Tournament** so an organizer running a similar event next month doesn't re-enter everything. Templates are explicitly *structure only* — they never carry team data, schedule data, or scores — which keeps them safe to share inside an organization across recurring events.

Required records and fields:
- `tournament_templates`: `id`, `organization_id`, `name`, `description nullable`, `source_tournament_id nullable`, `created_by_user_id`, `created_at`, `updated_at`, `usage_count`.
  - `source_tournament_id` is the tournament this template was captured from. Nullable so organizations can author templates from scratch in a future "blank template editor" surface (out of scope for V1).
  - `usage_count` is incremented every time the template is instantiated.
- `template_divisions`: `id`, `template_id`, `name`, `gender`, `age_group`, `skill_level`, `format_flags`, `best_of_sets`, `tiebreaker_preset`, `wave_code nullable`, `target_court_count nullable`, `display_order`.
- `template_stages`: `id`, `template_division_id`, `stage_number`, `stage_type`, `name`, `seeding_source`.
- `template_pools` (under a pool-type `template_stage`): `id`, `template_stage_id`, `name`, `sequence`, `target_size`.
- `template_pool_settings`: `template_stage_id`, `pool_count`, `teams_per_pool_target`, `pool_size_strategy`, `play_style`, `court_allocation_mode`, `ref_assignment_mode`.
- `template_brackets` (under a bracket-type `template_stage`): `id`, `template_stage_id`, `name`, `type`, `team_count`, `sequence`, `format_flags`.
- `template_advancement_rules`: `id`, `template_id`, `source_template_stage_id`, `source_template_pool_id nullable`, `source_finish_position`, `target_template_bracket_id`, `target_seed_slot`, `order_by`, `priority`.
- `template_waves`: `id`, `template_id`, `code` (`AM` or `PM`), `default_start_local nullable`, `display_order`.

**What templates explicitly do NOT carry:**
- `tournament_teams`, `division_rankings`, `pool_entries`, `bracket_seed_slots.resolved_tournament_team_id`, `bracket_seed_slots.manual_tournament_team_id` (overrides are tournament-specific decisions).
- `matches`, `match_assignments`, `match_sets`, `match_officials`.
- Venue or court references — venues are organization-scoped facilities that exist independently and are linked to a new tournament at instantiation time, not captured by the template.
- Dates, time zones, default match duration, or anything else on the `tournaments` row itself other than what `tournament_templates.name` / `description` describe — the organizer fills these in fresh on the new tournament.

**Capture flow.** When an organizer clicks **Save as template** on a tournament, the server reads the structural subset above and writes a new `tournament_templates` row plus its children inside one transaction. Captures are immutable snapshots — the template doesn't update if the source tournament continues to be edited afterwards. The organizer can re-capture (which creates a new template row) or edit the template directly (out of scope for V1; future template editor).

**Instantiation flow.** When an organizer clicks **Use template** (or **Clone tournament** — see below), the server:
1. Creates a new `tournaments` row with `status = 'draft'`, copying nothing structure-related yet.
2. Walks the template's children and writes `tournament_divisions`, `stages`, `pools` (with `target_size`), `pool_settings`, `pool_court_allocations = NULL` (organizer reassigns courts), `brackets`, `bracket_seed_slots` (empty — `resolved_tournament_team_id`, `manual_tournament_team_id`, `advancement_rule_id` all NULL initially), `advancement_rules`, and `waves`.
3. Increments `tournament_templates.usage_count`.
4. Redirects the organizer to the new tournament's editor.

The new tournament's stages all start at `Stage status = 'draft'` (no rankings, no team placements). The organizer still goes through team import, ranking, **Snake Rankings into Pools**, **Generate brackets**, and **Publish to public** for the new tournament — the template just saves the structural authoring time.

**Clone tournament** is the same flow with a hidden template — the server captures the source tournament to a `tournament_templates` row, instantiates it immediately, and presents the resulting new tournament as if it were a direct clone. The template row is retained for audit (`source_tournament_id` points back) and reusable; the organizer can later open it from the templates list to instantiate again.

## Additional items to include

The model is strong, but several supporting concerns are explicitly included because they usually become painful if omitted early.

### Audit and change history

A scheduling product needs a clear history of who changed rankings, pool assignments, bracket mappings, schedule assignments, and results. Audit data is especially important when organizers need to understand why a schedule changed or restore confidence after a repair operation.

Recommended pattern:
- Audit columns on all key tables.
- Dedicated change-log records for high-risk workflows such as ranking edits, pool regeneration, schedule publication, score correction, and repair actions.

### Concurrency control

Multiple admins may edit divisions, schedules, or results at the same time, so concurrency is named in the data model instead of left for implementation cleanup. EF Core-friendly optimistic concurrency on aggregates like `Division`, `Match`, and `ScheduleVersion` is a good fit for this rules-heavy domain.

Recommended pattern:
- Concurrency token on important aggregate roots.
- User-facing conflict message when stale edits are submitted.

### Status lifecycle definitions

The model already implies many statuses; allowed transitions are defined explicitly so business rules are not spread across controllers and UI code.

Important status lifecycles:
- **Tournament** status (`draft` → `published` → `archived`, with `published → draft` permitted) — see [§2](#tournament-status-lifecycle). Controls public visibility.
- **Division checklist** (coarse UI gating only) — **derived** from stages + teams via **DivisionReadinessProjection**; not a persisted aggregate lifecycle in V1 ([`adr/0003-status-lifecycle-per-aggregate.md`](adr/0003-status-lifecycle-per-aggregate.md)).
- **Stage** status (`draft` → `configured` → `seeded` → `in_progress` → `completed`) — see [§6](#6-stages-the-round-container).
- Tournament team status.
- Match status (`not_started` → `in_progress` → `final`, plus `canceled`). The **Match clock** columns (`matches.started_at` / `matches.completed_at`) carry the precise wall-clock anchors that the Court Status & Delay Dashboard reads independently of this coarse status — see [§8 Match clock writes](#match-clock-writes).
- **Schedule version** status (`draft` → `preview` → `published` → `superseded`) — see [§9](#schedule-version-status-lifecycle).
- Repair job status. New triggers `work_team_reassignment` and `schedule_reflow` walk the same lifecycle as the existing triggers.

### Domain events and integration hooks

Even though the system remains state-based rather than event-sourced, key domain events are listed because they help clarify side effects and future integrations.

Recommended events:
- `TournamentCreated`
- `TournamentPublished`
- `TournamentUnpublished`
- `TournamentArchived`
- `TournamentTemplateCaptured`
- `TournamentInstantiatedFromTemplate`
- `DivisionCreated`
- `TournamentTeamImported`
- `DivisionSeedingFinalized`
- `PoolsGenerated`
- `BracketConfigured`
- `SchedulePublished`
- `MatchStarted`
- `MatchResultReported`
- `WorkTeamReassigned`
- `ReflowPreviewApplied`
- `TournamentTeamWithdrawn`
- `RemainingMatchesReassigned`

### Validation and diagnostics storage

The product's flexibility depends heavily on explaining why an auto-generated schedule or repair plan did what it did. Storing diagnostics, warnings, and validation outcomes makes the scheduler feel trustworthy rather than opaque.

Recommended pattern:
- Diagnostics json on schedule generation and repair jobs.
- Human-readable warning summary for admins.

### Soft-delete and archival strategy

This document defines whether high-level entities are archived, soft-deleted, or hard-deleted. This matters because tournaments, teams, and schedules often need historical preservation even after operational use ends.

Recommended pattern:
- Archive tournaments and leagues.
- Soft-delete only for records that may need recovery.
- Never hard-delete records that affect standings, schedules, or audit history.

## Priority recommendation

For the first implementation pass, the highest-priority data groups are tournament metadata, division configuration, **stages** (the round container) with `seeding_source`, tournament entries, ranking/seeding (`division_rankings` as the single source of truth), pools (with `target_size` so empty pools can be authored), brackets (with `bracket_seed_slots` carrying `advancement_rule_id`, `manual_tournament_team_id`, and `resolved_tournament_team_id`), advancement rules (with `order_by`), matches (with `match_winner` / `match_loser` participant source types, plus the `started_at` / `completed_at` **Match clock** columns), `match_officials` carrying the **WorkTeam** + R1 + R2 assignments and their audit log, schedule versions, assignments, and set-level results. The **Reflow preview** dry-run path on the scheduler and the `tournament_templates` family are also V1 because both ship UIs (Court Status dashboard reflow, Clone tournament action) on top of them. Audit, concurrency, status definitions, and repair-job records (including the new `work_team_reassignment` and `schedule_reflow` triggers) are also part of the model from the start because they influence the shape of the core schema.

The engine ships in V1 even though the V1 wizard only exposes a subset (1 pool stage + 1 bracket stage, `division_rankings` seeding only, no challenge matches). This protects Power Pool, multi-pool-round, Challenge Phase, and other tournament-engine features from requiring schema migrations later.
