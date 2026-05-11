# CourtFlow

CourtFlow is an admin-first volleyball tournament management platform. This file pins the canonical domain language so docs, code, schema, and UI stay aligned.

## Language

**Tournament**:
The canonical domain entity for a single competitive event run by an organization. Aggregate root in the schema; used in all admin code, admin DTOs, and admin-facing docs.
_Avoid_: Event (in the admin context), competition.

**Event**:
Public-facing synonym for **Tournament**. Used only on the public surface — the public route (`/events/{id}`), public DTOs (`PublicEventDto`), and the public-facing feature docs (`features/public-landing.md`, `features/public-event.md`). Never appears in the admin surface or in domain code.
_Avoid_: Tournament (in the public context).

**Club**:
A long-lived org-scoped affiliation that owns one or more **Teams**. Lives in `clubs`. Reused across tournaments and seasons.
_Avoid_: Organization (which is the multi-tenant boundary, not a club).

**Team**:
The master, long-lived record of a competing roster, owned by a **Club**. Lives in `teams`. Persists across tournaments. The scheduler **never** references this directly — it works through **TournamentTeam**.
_Avoid_: Tournament team (when you mean the master record).

**TournamentTeam**:
The entry of a **Team** into a specific **Tournament** + **Division**. Lives in `tournament_teams`. Carries event-scoped fields: entry **Status**, check-in, withdrawal, and (once placed) pool / bracket-slot references. This is the entity the scheduler, brackets, and standings always work with. Seeding **input** lives in `division_rankings`, not on the TournamentTeam row.
_Avoid_: Team entry, registration.

**Division**:
A volleyball competitive grouping inside a **Tournament** — typically by gender, age group, and skill (e.g. "Girls 14U Open", "Boys 18U Club"). Teams in different divisions never play each other. A division has its own format settings (best-of-sets, pool play on/off, bracket play on/off), its own seeding, and its own competition structure (its **Stages**).
_Avoid_: Bracket (one of a division's stages, not the division itself), Group.

**Stage**:
A phase of competition inside a **Division** — the "round" organizers describe in plain English ("pool round in the morning, bracket round in the afternoon"). A Stage has a **StageType** (`pool` or `bracket`) and contains **one or more** Pools (when type is `pool`) or **one or more** Brackets (when type is `bracket`); a single Stage never mixes the two. A division typically has one or two stages (pool-only, bracket-only, or pool then bracket) but can have more (e.g. preliminary pool round, qualifying pool round, then bracket round). Lives in `stages`. `matches.stage_id` is the canonical reference for which stage a match belongs to; the leaf reference (`matches.pool_id` or `matches.bracket_id`) tells you which specific Pool or Bracket inside the stage. Each Stage also carries a **SeedingSource** (see below) that picks where its initial team placement comes from.
_Avoid_: Round (a sub-unit inside a Bracket — see **BracketRound**), Phase, Wave (a Tournament-level scheduling block, not a Division-level competition round).

**SeedingSource**:
The enum on a **Stage** that selects where the stage's initial team placement comes from: `division_rankings` (the **Ranking** of the parent division — used by the first pool-type Stage and by bracket-only divisions) or `prior_stage_finish_positions` (uses **FinishPosition** from an earlier stage — used by a second pool-type Stage in Power Pool formats, and by any bracket Stage following a pool Stage via **AdvancementRules**). Stored as `stages.seeding_source`. V1 wizard only writes `division_rankings` for the first pool Stage; the engine supports `prior_stage_finish_positions` from day one so Power Pool wizards can ship later without schema changes.
_Avoid_: Seeding mode (`seeding_method` is the within-pool snake/balanced choice, a separate concept).

**StageType**:
The enum on a **Stage** that selects which competition shape that stage runs: `pool` (round-robin groups) or `bracket` (elimination tree). The StageType determines which leaf entity (Pool or Bracket) the stage contains and how the **AdvancementRules** flowing out of it are computed (FinishPosition for pool stages, bracket-round result for bracket stages).
_Avoid_: Stage format, Round type.

**Pool**:
A single volleyball round-robin group — typically 3–5 **TournamentTeams** that play each other to produce a **FinishPosition** order. A Pool lives inside exactly one **Stage** whose **StageType** is `pool`; a pool-type Stage with N pools is the normal "pool round" of a division (e.g. Pool A, Pool B, Pool C). Lives in `pools` (owned by `stages.id`); team membership lives in `pool_entries`.
_Avoid_: Group, Pool round (use **Stage** when you mean the round containing several pools), Round-robin (the format, not the entity).

**Bracket**:
A single volleyball elimination tree — single or double elimination, with optional consolation, third-place, and grand-final-reset variants. A Bracket lives inside exactly one **Stage** whose **StageType** is `bracket`; a bracket-type Stage with multiple brackets is how "Gold + Silver" or "Championship + Consolation" formats are modeled (one Stage, two Brackets). Seeded from prior-stage **FinishPosition** via **AdvancementRules**, or directly from **Ranking** when no pool stage precedes it. Lives in `brackets` (owned by `stages.id`); the slot map lives in `bracket_seed_slots`; the connections from a prior stage to bracket slots live in `advancement_rules`. A **BracketSlotOverride** may manually pin any individual slot to a chosen **TournamentTeam** without rewriting the source rule.
_Avoid_: Tournament tree, Knockout (casual aside only), Bracket round (use **Stage** when you mean the round containing several brackets).

**BracketRound**:
A column inside a single **Bracket** — Round 1, Round 2, Quarterfinals, Semifinals, Final, etc. Implicit in the slot numbering of `bracket_seed_slots`; not a separate table. Surface label only.
_Avoid_: Round (without "Bracket" qualifier when ambiguity is possible).

**Wave**:
A scheduling block inside a **Tournament**, usually a session of the day — `AM`, `PM`, or numbered (`1`, `2`) for multi-session tournaments that run several waves on the same courts in one day. A division can be pinned to a wave so its matches stay inside that session. Lives in `waves`; referenced by `tournament_divisions.wave_id` and used by the scheduler for time-slot constraints.
_Avoid_: Session, Slot (which means a single court × time-of-day cell), Block.

**Match**:
A single scheduled volleyball game between two **TournamentTeams** in one **Division**, belonging to exactly one **Stage** and to exactly one leaf inside that stage (one Pool when `StageType = pool`, one Bracket when `StageType = bracket`). Identity is stable — participants, stage, and the set of sets played do not change when the match is moved to another court or time. Lives in `matches` with `stage_id` (canonical) plus `pool_id` or `bracket_id` (leaf). The "where and when" lives separately as a **MatchAssignment** (see below).
_Avoid_: Game (which in volleyball usually means a single set), Fixture.

**MatchAssignment**:
The court + start time + duration assigned to a **Match** inside a specific **ScheduleVersion**. Lives in `match_assignments`, owned by `schedule_versions`. Reassigning a match (the "Move" action in the schedule grid) creates or amends a MatchAssignment; it does not mutate the Match.
_Avoid_: Booking, Slot (slot is the grid cell, not the assignment record).

**ScheduleVersion**:
A snapshot of all match assignments for a **Tournament**. Generating a schedule creates a draft version; publishing it makes it the current version. Older versions are kept for audit and "undo this regeneration" workflows.
_Avoid_: Schedule (use ScheduleVersion when the version dimension matters; "schedule" is fine as casual UI label for the current published version).

**Status**:
The current named phase of an entity in its competition lifecycle — e.g. a Division is `draft` until its teams are loaded, a Match is `not_started` until its first set is reported, a TournamentTeam is `active` until it withdraws. Stored as `*_status` columns in the schema (`division_status`, `match_status`, `tournament_team_status`, `schedule_version_status`, etc.). The DTO suffix is `Status` (e.g. `MatchStatusDto`).
_Avoid_: State (which is reserved for transient UI conditions like "loading state", "empty state"), Phase, Step.

**Lifecycle**:
The set of legal `Status → Status` transitions for an entity, plus the rules guarding each transition (who can trigger it, what side effects fire, what events are emitted). The Match Lifecycle, the Division Lifecycle, the TournamentTeam Lifecycle, etc. Documented per-entity in [`docs/data-model.md`](docs/data-model.md#status-lifecycle-definitions); enforced inside aggregate roots in `Domain` per [`docs/architectural-prd.md`](docs/architectural-prd.md).
_Avoid_: Status flow, State machine (technically accurate but uses the avoided term "state").

**Stage status**:
The **Status** of a **Stage** in the five-step pipeline `draft` → `configured` → `seeded` → `in_progress` → `completed`. The crucial distinction: `configured` means "structure complete (pools / brackets / advancement rules defined), teams **not** yet placed" — this is the explicit **pre-build** state that lets organizers design the tournament shape weeks before teams are confirmed. `seeded` means "structure complete **and** teams placed" (pool entries written, bracket slots resolved or manually pinned). Gates what the Division detail workspace allows for that round (settings, leaf generation, team placement, structural freeze). Full table in [`docs/data-model.md`](docs/data-model.md#stage-status-lifecycle).
_Avoid_: Division status (coarser — use **Stage status** when talking about a specific round).

**Tournament status**:
The **Status** of a **Tournament** in the three-step lifecycle `draft` → `published` → `archived`. Controls **public visibility**: a `draft` tournament is invisible on the public landing and its deep link returns 404; a `published` tournament appears on `/` and its `/events/{id}` page works; an `archived` tournament appears only as a past event (or hidden, per organization settings). The transition `published → draft` is permitted with a strong confirm ("breaks shared links"). Auto-archive runs N days (default 14, per-org configurable) after `end_date` for tournaments that aren't manually archived. Distinct from **Schedule version status** (which is the *internal* draft/preview/published/superseded lifecycle of a single `ScheduleVersion`) — the two layers compose: a `published` tournament still hides its public Schedule tab until at least one `ScheduleVersion` reaches `status = 'published'`. Full table in [`docs/data-model.md`](docs/data-model.md#tournament-status-lifecycle).
_Avoid_: Event status, Public status (Tournament status is the precise term; "public visibility" is fine as casual paraphrase).

**RepairJob**:
A coordinated bundle of changes applied after something disrupts the published plan — a **TournamentTeam** withdraws, a court becomes unavailable, or officials must be swapped on short notice. Lives in `repair_jobs`. Has its own **Status** and **Lifecycle**, an actor, timestamps, and a **RepairTrigger**. Preview-before-apply is preferred when the change touches many matches.
_Avoid_: Adjustment (unless paired with schema rename), Fix-up job.

**RepairAction**:
One persisted step inside a **RepairJob** — for example "set withdrawal fields on `tournament_teams`", "shift these `match_assignments` by N minutes", "reassign **WorkTeam** on `match_officials`". Lives in `repair_actions` with before/after payloads for audit.
_Avoid_: Step (ambiguous with wizard UI steps).

**RepairTrigger**:
The reason a **RepairJob** was opened. First-class triggers in the target model include **team withdrawal**, **court outage**, and **referee reassignment**. Stored as `trigger_type` on `repair_jobs`; aligns with [`docs/data-model.md`](docs/data-model.md#11-repair-and-live-adjustment).
_Avoid_: Cause, Reason alone (use alongside RepairTrigger in prose).

**Ranking**:
The organizer-assigned, skill-based ordering of every **TournamentTeam** within a **Division** before pools are generated. Captures past performance, club reputation, scouting, manual override. Lives in `division_rankings` (`rank`, `source`, `is_manual_override`). This is the **single source of truth for seeding input** — there is no parallel "seed hint" column on `tournament_teams`. Imports (CSV `Rank`, predefined-pools `SeedHint`) write directly to `division_rankings`. Editable on the Division detail workspace's persistent Rankings panel until the first stage reaches `in_progress`; locked thereafter unless a **RepairJob** revisits it.
_Avoid_: Seed (Ranking is the input to the seeding algorithm, not a Seed itself), SeedHint (deprecated column name — was on `tournament_teams.seed` in V0 and is gone in the target schema), Power rating.

**Seed** (Pool Seed):
The pool-placement position assigned to a **TournamentTeam** when pools are generated — typically the snake/balanced output of the **Ranking**. Lives in `pool_entries.seed_in_pool`. Frozen once pools are generated. The unqualified word "Seed" in admin prose means this Pool Seed.
_Avoid_: Ranking (Ranking is the upstream input), BracketSeed (use the precise term when bracket placement is meant).

**BracketSeed**:
The bracket-slot placement of a **TournamentTeam** inside a single **Bracket**. Derived in one of two ways: (1) from a prior **Stage**'s **FinishPosition** via **AdvancementRules**, or (2) directly from **Ranking** when no pool stage precedes the bracket. Optionally **overridden** by a **BracketSlotOverride** that pins a specific team to a specific slot. Lives in `bracket_seed_slots` (`seed_position`, `expected_source_type`, `advancement_rule_id nullable`, `manual_tournament_team_id nullable`, `resolved_tournament_team_id nullable`). Use this term in any sentence that involves bracket placement to keep "Seed" unambiguous.
_Avoid_: Bracket position (use BracketSeed), Pool seed (different concept, see above).

**BracketSlotOverride**:
A manual pin that forces a specific **BracketSeed** slot to resolve to a chosen **TournamentTeam** regardless of the **AdvancementRule** that would otherwise fill it. Stored as `bracket_seed_slots.manual_tournament_team_id` and surfaced as a slot-level chip in the bracket viewer. Used for the customer-real case of "the top 3rd-seed had to go up" without rewriting the upstream rule. Every override is logged with actor, timestamp, and optional reason in `bracket_seed_slot_overrides` audit rows.
_Avoid_: Manual seed (ambiguous with "Pool Seed"), Slot edit.

**AdvancementRule**:
A single mapping that feeds **TournamentTeams** from a prior pool-type **Stage** into one or more **BracketSeed** slots of a later **Bracket**. Lives in `advancement_rules` keyed by `source_stage_id`, `source_pool_id nullable` (wildcard when null), `source_finish_position`, `target_bracket_id`, `target_seed_slot`, `order_by`, `priority`. The `order_by` enum (`pool_sequence` (default — preserves the customer's "incoming seed → outgoing seed" format) | `tiebreaker_rank` (cross-pool comparison by **TiebreakerPreset**)) controls how wildcard rules distribute resolved teams across consecutive bracket slots. V1 supports **cross-stage** flow only (pool → bracket). Within-bracket flow (double elimination winner/loser pointers, grand-final reset, third-place match) is implicit in **`brackets.type`** and is not authored as advancement rules. The wizard ships two presets — **"Top N from each pool → Gold"** and **"Remaining → Silver"** — which insert the right wildcard rules for the most common formats.
_Avoid_: Promotion rule, Advancement table (use the singular term and the plural "AdvancementRules" in prose).

**EntrySeed** _(deprecated)_:
Was the optional registration-time seed hint on `tournament_teams.seed` / CSV `SeedHint`. Removed from the target schema — imports write directly to `division_rankings` (see **Ranking**). The term and column are kept as a deprecated entry to flag any stale doc / code references during migration.
_Avoid_: Seed (without qualifier — that means Pool Seed in admin prose).

**Standing**:
A **TournamentTeam**'s position within a **Pool** computed from match results. Updates while pool play is in progress. Surfaced on the Standings page (`features/standings.md`).
_Avoid_: Ranking (Ranking is organizer-assigned input; Standing is computed from results), Final position (use **FinishPosition** when you mean the snapshot at pool close).

**FinishPosition**:
The **Standing** snapshot at the moment a **Pool** closes. Persisted as `pool_entries.finish_position`. Drives `advancement_rules` and therefore **BracketSeed**. Distinct from the live Standing because the pool may close while later matches are still being scored elsewhere.
_Avoid_: Standing (which is the live, possibly mid-play value), Bracket seed (which is downstream).

**Tiebreaker**:
The criterion (or chain of criteria) used to order **TournamentTeams** with equal results inside a **Pool**. Set and point comparisons are **ratio**-based (`SW / (SW + SL)`, `PF / (PF + PA)`), never differential.
_Avoid_: Tiebreak (use only as casual aside), Sort key.

**TiebreakerPreset**:
The named selection an organizer makes per **Division** to govern the tiebreaker chain. Stored as `tournament_divisions.tiebreaker_preset`. V1 values:
- `WINS_H2H_SETS_POINTS` — USAV-style default; wins, then head-to-head, then set ratio, then point ratio.
- `WINS_SETS_POINTS` — most common organizer choice when head-to-head is intentionally skipped.
- `SETS_POINTS` — used for round-robin / jamboree formats where wins is not the headline rank.
- `H2H_SETS_POINTS` — specialized formats that want head-to-head before any aggregate.
Full chain semantics, head-to-head subset rules, forfeit handling, and stable ordering live in [`docs/data-model.md`](docs/data-model.md#tiebreaker-presets).
_Avoid_: Tiebreaker rule, Sort preset.

**WorkTeam**:
The **TournamentTeam** assigned to officiate a specific pool **Match** — the volleyball-specific "the team that just played refs the next match" rotation. A complete officiating crew (R1 referee, R2 referee, scorer, libero tracker, two line judges) is sourced from the WorkTeam's roster on tournament day; in the schema we only track the team-level assignment (`match_officials.work_team_tournament_team_id`) plus optional named-role slots (`r1_tournament_team_id`, `r2_tournament_team_id`) for matches where a separate team or paid official covers R1. Auto-rotation policy per pool is computed from the pool's match order; organizers can override any individual match via the **Change referee** action, which writes a **RepairAction** of kind `work_team_reassignment`. Bracket-play work teams default to "loser of the prior match on the same court" per the JVA/USAV convention. Distinct from **Match official** (a generic schema term covering paid R1s, head refs, etc.); WorkTeam is the specific peer-team assignment.
_Avoid_: Officiating team (acceptable casual prose; precise term is WorkTeam), Ref team.

**Match clock**:
The pair of timestamps on a **Match** that record when it **actually** started and ended on court, distinct from its `MatchAssignment.scheduled_start`. Stored as `matches.started_at` and `matches.completed_at`. Set by an explicit **Start** action on the Court Status or Schedule grid; auto-fallback writes `started_at = now()` the first time a score arrives without a prior Start press. Drives the per-court **delay delta** ("+12 min behind"), the Court Status & Delay Dashboard chips, and the reflow preview. Independent of the canonical **Match status** transitions (which still walk `not_started → in_progress → final`); `started_at` is the precise wall-clock anchor that lets the dashboard compute deltas accurately rather than reading status timestamps that may lag.
_Avoid_: Match start time (ambiguous with scheduled start), Actual time.

**Reflow preview**:
A non-persistent simulation of what a proposed change would do to the rest of the schedule, run before any **RepairJob** or **Move** is committed. Computes the cascade — which downstream **MatchAssignment**s shift, which courts get new matches, which conflicts appear or resolve — against the currently-loaded `ScheduleVersion` and returns the diff. Surfaced from the Court Status & Delay Dashboard, the schedule grid's Move dialog, and every repair dialog that touches more than one assignment (court outage, team withdrawal). Never writes; the organizer either **Apply**s the preview (which creates the RepairJob or fork) or **Discard**s it. Implementation reads existing scheduler diagnostics in dry-run mode.
_Avoid_: Schedule simulation (acceptable casual aside), What-if.

**Tournament template**:
A reusable snapshot of a **Tournament**'s configuration — divisions, default pool counts, default bracket types, advancement-rule presets, wave codes, tiebreaker presets, default match duration — saved off a real tournament and used to seed the next one. Stored as `tournament_templates` plus child rows mirroring the subset of tournament configuration the template captures. **Templates do not capture team data, schedule data, or scores** — only structure and policy. Instantiation creates a new draft **Tournament** with the template's defaults pre-filled and leaves every materialization step (team import, **Snake Rankings into Pools**, **Generate brackets**, **Publish to public**) to the organizer. The "Clone tournament" action on the Tournaments table is sugar for "save this tournament as a template, then instantiate it" without showing the intermediate template row.
_Avoid_: Tournament copy (use **Clone** as the UI verb), Preset (which means TiebreakerPreset and AdvancementRule preset elsewhere).

**Grid view** (Court Status):
One of two layouts of the **Court Status & Delay Dashboard**. A court × time matrix where every cell renders a match colored by its delay / status (pale green = live on time, pale amber = 2–10 min behind, solid red = > 20 min behind, solid green = final, etc.). Designed for scanning the whole gym at once — the organizer reads delay as cell color instead of clicking through cards. The other layout is **Cards view** (one MudCard per court, action-dense). Both layouts share the same data, the same auto-refresh path, and the same actions (Start, Score, Move, Change referee, Reflow preview); the toggle is purely visual.
_Avoid_: Schedule grid (which is the *editing* surface at `/schedule`, not the *operational* one at `/court-status`), Live grid (acceptable casual aside).

**TV mode**:
The full-screen, chrome-stripped variant of the Court Status & Delay Dashboard's **Grid view**, designed to be projected on a wall monitor or large TV at the gym. Hides the app bar, drawer, breadcrumbs, and toolbar; bumps font sizes ~30%; forces auto-refresh on at 30 s; anchors the scroll position on the Now line at every refresh. Activated by the **TV mode** icon on the dashboard toolbar; exited via `Esc` or the in-strip Exit button. Sticky for the browser session via `sessionStorage` so an accidental refresh on a kiosk doesn't drop back to chromed view. Distinct from a hypothetical "kiosk auth session" (future enhancement) — TV mode is purely a UI layout switch on top of a normal admin session.
_Avoid_: Kiosk mode (overloaded with token-based auth — call this **TV mode** to keep the boundary clear), Wall mode, Big-screen.

### Shorthand rule

In UI labels and casual prose inside a single-tournament context (e.g. the "Teams" page, the schedule cell "Home vs Away"), **Team** is acceptable shorthand for **TournamentTeam**. The precise term is required in data, schema, domain code, and any doc that crosses tournaments (withdrawals, division moves, repair operations, imports).

## Relationships

- A **Tournament** is presented to the public as exactly one **Event**. They are the same database row; the surface name differs by audience.
- A **Club** owns one or more **Teams**.
- A **Team** has zero or more **TournamentTeam** entries — one per tournament + division it competes in.
- A **TournamentTeam** belongs to exactly one **Tournament** and exactly one **Division**.
- A **Tournament** has one or more **Divisions**.
- A **Division** has one or more **Stages**, each with a **StageType** of `pool` or `bracket`.
- A pool-type **Stage** contains one or more **Pools**; a bracket-type **Stage** contains one or more **Brackets**. A single Stage never mixes the two.
- An **AdvancementRule** connects a `source_stage_id` (the stage a team is leaving) to a `target_bracket_id` + `target_seed_slot` (the slot a team is entering). Pool-stage sources use `source_pool_id` + `source_finish_position` + `order_by`; bracket-stage sources are deferred beyond V1.
- A **BracketSlotOverride** pins an individual **BracketSeed** slot to a chosen **TournamentTeam** without modifying the upstream rule. Overrides outrank the rule at materialization time and are audit-logged.
- A **Match** belongs to exactly one **Division** and exactly one **Stage**, plus the corresponding leaf (`matches.pool_id` when the stage is `pool`, `matches.bracket_id` when the stage is `bracket`).
- A **Match** has zero or one current **MatchAssignment** inside the active **ScheduleVersion**, plus historical assignments from prior versions.
- A **Tournament** has zero or more **Waves**; a **Division** can be pinned to one wave to constrain when its matches are scheduled.
- A **RepairJob** belongs to one **Tournament** (and optionally scopes to one **Division**). It contains one or more **RepairActions** and is classified by **RepairTrigger**.
- The seeding lifecycle inside a **Division** flows in one direction: **Ranking** → **Seed** *(pool placement)* → **Standing** *(live)* → **FinishPosition** *(snapshot)* → **BracketSeed** *(with optional **BracketSlotOverride**)*. Each step has its own table and its own moment of being frozen. There is no separate "EntrySeed" stage in the target schema — imports write directly to **Ranking**.
- A **Stage**'s **SeedingSource** picks where its initial placement comes from. The first pool-type Stage of a division reads `division_rankings`. A later pool-type Stage (Power Pool — future wizard) reads `prior_stage_finish_positions`. A bracket Stage's slot resolution goes through **AdvancementRules** when a pool stage precedes it, or directly through **Ranking** when no pool stage precedes it.
- **Standing** order inside a pool is resolved by the division's **TiebreakerPreset** chain. Set and point comparisons are always ratios.
- A **Match** has at most one **WorkTeam** assignment per `match_officials.work_team_tournament_team_id`; the WorkTeam is always another **TournamentTeam** in the same **Division** (or, for cross-division coverage, the same **Tournament**). The WorkTeam never plays the match it is reffing. Override audit lives on the same row's `manual_assigned_at` / `manual_assigned_by` columns.
- A **Match**'s **Match clock** (`started_at` / `completed_at`) is independent of its `MatchAssignment.scheduled_start` and of `match_status`. The delta `started_at − scheduled_start` is the per-match component of the Court Status & Delay Dashboard's behind/ahead chip.
- A **Reflow preview** runs against the currently-loaded `ScheduleVersion` and never writes. Applying it either forks a new `draft` version (when the loaded version is `published`) or amends the currently-loaded `draft` / `preview` version.
- A **Tournament template** captures the structural subset of one **Tournament** (divisions, pool / bracket / advancement structure, wave codes, tiebreaker presets, default durations). Instantiation creates a new `draft` **Tournament**; nothing in `tournament_teams`, `division_rankings`, `match_assignments`, or `match_sets` is copied.
- The Court Status & Delay Dashboard has two layouts of the same data — **Cards view** and **Grid view** — toggled in the toolbar and persisted per-user. **TV mode** is the full-screen variant of **Grid view**, persisted per browser session.

## Example dialogue

> **Dev:** "On the admin shell, the header tournament selector lists all my **Tournaments**, right?"
> **Domain expert:** "Yes — and on the public landing the same records appear as **Events** in the cards grid."

> **Dev:** "If a **Team** withdraws mid-tournament, does it disappear from the **Club**?"
> **Domain expert:** "No — the **Club** and **Team** are untouched. Only that one **TournamentTeam** gets `withdrawn_at` set. The same **Team** can still be entered in another tournament."

> **Dev:** "The Girls 14U **Division** runs pool play in the morning then a bracket in the afternoon. Is that one stage or two?"
> **Domain expert:** "Two. The pool round is one **Stage** with `StageType = pool` containing several **Pools** (Pool A, Pool B, Pool C). The afternoon elimination is a second **Stage** with `StageType = bracket`. The bracket Stage is seeded from the prior Stage's **FinishPositions** via **AdvancementRules**."

> **Dev:** "Same division also runs Gold and Silver brackets in the afternoon. Are those two stages?"
> **Domain expert:** "No. One **Stage** of type `bracket` containing two **Brackets** — Gold and Silver. **AdvancementRules** route the top finishers from the morning pool Stage to Gold and the rest to Silver."

> **Dev:** "We split the day into AM and PM courts. Is that two **Tournaments**?"
> **Domain expert:** "No — one **Tournament** with two **Waves**. Pin each **Division** to the wave its matches should run in."

> **Dev:** "She went into the bracket as a 3-seed — is that her **Ranking**?"
> **Domain expert:** "No. Her **Ranking** was the organizer's input before pools. Her pool **Seed** placed her in Pool B as the 2-seed. After pool play her **FinishPosition** was 1st in Pool B, which advancement rules turned into **BracketSeed** 3 in the elimination tree. Four different numbers, all called 'seed' in casual talk."

> **Dev:** "Our first customer's format: 'whoever wins Pool A becomes the 1-seed in the bracket' regardless of who they were going in. How do I model that?"
> **Domain expert:** "One **AdvancementRule** with `source_pool_id = NULL` (wildcard, every pool), `source_finish_position = 1`, `order_by = pool_sequence`, `target_seed_slot = 1`. At generate time it consumes one slot per pool in pool order — Pool A 1st → bracket slot 1, Pool B 1st → bracket slot 2, etc. That's the **`pool_sequence`** default. The alternative `order_by = tiebreaker_rank` would compare those 1st-place finishers across pools by the **TiebreakerPreset** chain and rank them that way, which their customer explicitly avoids."

> **Dev:** "Same customer: 'sometimes the top 3-seed had to go up'. How is that modeled?"
> **Domain expert:** "A **BracketSlotOverride** on the slot in question. The slot's `manual_tournament_team_id` is set to that team; `bracket_seed_slot_overrides` records who, when, and why. The upstream rule is unchanged."

> **Dev:** "An organizer wants to build the whole tournament shape before any teams have entered. Can they?"
> **Domain expert:** "Yes. They open the Division detail workspace, walk the wizard's three structure steps — Pool Structure, Bracket Structure, Advancement Rules — and save. Every pool slot reads as `Pool A · 1`, every bracket slot reads as `Gold-1 ← Pool A · 1st`. The Stage stays in `configured` **Status** until teams are placed; nothing about the structure references team ids. When teams arrive, the persistent Rankings panel fills in and the **'Snake Rankings into Pools'** and **'Resolve Bracket Slots'** buttons materialize the placements."

> **Dev:** "Pool A · Match 3 — who's reffing it?"
> **Domain expert:** "The **WorkTeam**. By default it's whichever **TournamentTeam** in Pool A is sitting out match 3 per the auto-rotation (the team that just played match 2 typically). The schema stores the team-level assignment on `match_officials.work_team_tournament_team_id`. If the TD swaps it on the schedule grid, that writes a `work_team_reassignment` **RepairAction** with before/after."

> **Dev:** "Court 3 ran 20 minutes over. How does the dashboard know?"
> **Domain expert:** "The match's **Match clock** was started by the TD (or by first-score auto-fallback). `matches.started_at − match_assignments.scheduled_start = +20 min`. Aggregated across the court's upcoming matches, that's the per-court delta on the Court Status & Delay Dashboard."

> **Dev:** "Same court — the TD wants to see what happens if we slide everything 20 minutes."
> **Domain expert:** "They click **Reflow preview** on the court card. The engine computes the cascade against the current `ScheduleVersion` without writing anything and returns a diff. The TD reviews and clicks **Apply**, which either amends the loaded `draft` / `preview` version or forks a new `draft` from the currently `published` version."

> **Dev:** "Next month's regional repeats the structure. Do they re-enter everything?"
> **Domain expert:** "No. They open the Tournaments table, click **Clone** on last month's tournament, and a new `draft` **Tournament** appears pre-filled with the same divisions, pool counts, advancement-rule presets, wave codes, and tiebreaker presets. Behind the scenes the clone goes through a **Tournament template** record; the team data, schedule data, and scores are not copied."

## Named application modules (read models)

These are **Application-layer modules** (or read-model projections), not new aggregates. They give **one interface** where many UI entry points and handlers would otherwise duplicate rules. Full architecture table: [`docs/architecture.md`](docs/architecture.md#deep-modules-named-application-seams). ADRs: [`docs/adr/0004-reflow-preview-contract.md`](docs/adr/0004-reflow-preview-contract.md), [`docs/adr/0005-stage-materialization-and-bracket-resolution.md`](docs/adr/0005-stage-materialization-and-bracket-resolution.md), [`docs/adr/0006-read-models-tiebreaker-delay-public-division-readiness.md`](docs/adr/0006-read-models-tiebreaker-delay-public-division-readiness.md), [`docs/adr/0007-engine-seams-and-testing.md`](docs/adr/0007-engine-seams-and-testing.md).

**ReflowPreview** (module name; domain term remains **Reflow preview**):
The dry-run + apply bundle for schedule cascades: proposal against a loaded **ScheduleVersion** → diff + diagnostics + `apply_token` → **Apply** (fork when published, **RepairJob** + **RepairTrigger** as documented). One implementation; many UI adapters (Court Status, Schedule Move, repair dialogs).

**StageMaterialization**:
The shared idempotency and **Stage status** gating for **Snake Rankings into Pools**, **Resolve Bracket Slots**, and **Generate brackets** — one place for "re-run allowed?", overwrite warnings, and **RepairJob** vs in-wizard paths.

**BracketResolution**:
Precedence inside materialization: **BracketSlotOverride** → **AdvancementRule** → unresolved; drives **BracketSeed** and bracket **Match** participants.

**TiebreakerChain**:
Pure ordering from **Match** results + **TiebreakerPreset** (ratios, head-to-head subset, forfeit, stable **Ranking** fallback). Feeds **Standing**, `order_by = tiebreaker_rank`, and Division dialog chain preview.

**MatchClockDelaySignal**:
Canonical per-**Match** delay from **Match clock** + **MatchAssignment**; NULL when no signal yet; band mapping for Court Status / Schedule / public chips. **NULL must not be coerced to zero** for "ahead" semantics.

**PublicEventReadiness**:
Composed tab visibility on the public **Event** page from **Tournament.Status**, **ScheduleVersion** publication, bracket materialization, and scoring state.

**DivisionReadinessProjection**:
How coarse **Division** **Status** composes with fine **Stage status** so gating never contradicts.

## Flagged ambiguities

- "Event" was previously used in admin-facing prose (e.g. "set up events, divisions, pools"). Resolved: in admin contexts always use **Tournament**; "Event" is reserved for the public surface.
- "Team" is overloaded by design between the master record and the per-tournament entry. Resolved via the **shorthand rule** above: casual UI prose can say "Team" inside a single tournament; data/schema/domain/cross-tournament docs must say **TournamentTeam**.
- "Bracket" was previously used as if it were a peer of "Division" (e.g. "no brackets. Generate one above"). Resolved: a **Bracket** is a leaf inside a bracket-type **Stage** of a **Division**; a **Pool** is a leaf inside a pool-type Stage. The canonical reference is `matches.stage_id` plus the appropriate leaf id; the schedule cell's "Bracket" chip is a stage-type label, not a separate concept.
- "Stage" was previously defined as 1:1 with a Pool or Bracket. Resolved: a Stage has a **StageType** and contains one or more Pools (when `pool`) or one or more Brackets (when `bracket`). "Gold + Silver" is one bracket Stage containing two Brackets, not two stages.
- "State", "status", and "lifecycle" were previously used interchangeably for an entity's current phase. Resolved: **Status** is the current value (a column on the row), **Lifecycle** is the rules and transitions diagram, and **State** is reserved for transient UI conditions ("loading state", "empty state", "auth state"). DTOs end in `Status`, not `State`.
- **Repair** vocabulary is aligned with the schema (`repair_jobs`, `repair_actions`). **RepairJob** / **RepairAction** / **RepairTrigger** are the precise terms; admin UI copy may still say "Withdraw team", "Court unavailable", or "Change referee" without saying "repair".
- "Seed" was overloaded across `tournament_teams.seed` (V0), `pool_entries.seed_in_pool`, and `bracket_seed_slots.slot_number` (V0; renamed to `seed_position` in target). Resolved: unqualified **Seed** means the Pool Seed (the most common admin use); bracket placement is **BracketSeed**. The `tournament_teams.seed` column / **EntrySeed** concept is **deprecated** — the target schema has a single source of truth for seeding input (**Ranking** in `division_rankings`).
- "Standings" was used loosely for both the live in-pool ranking and the locked-in result. Resolved: **Standing** is the live value; **FinishPosition** is the snapshot at pool close that drives bracket advancement.
- "Manual override" on a bracket was previously vague. Resolved: a **BracketSlotOverride** pins a single slot to a chosen team via `bracket_seed_slots.manual_tournament_team_id`, leaving the upstream **AdvancementRule** untouched. Override audit lives in `bracket_seed_slot_overrides`.
- "Seeding source" of a stage was implicit. Resolved: every **Stage** carries a **SeedingSource** (`division_rankings` vs `prior_stage_finish_positions`). V1 wizard writes only the former; the latter unlocks Power Pool and other multi-pool-stage formats in future wizards.
- "Tiebreaker" was previously vague (a `data-model.md` recommendation with no shape). Resolved: organizers pick a **TiebreakerPreset** per **Division** from four V1 options; comparisons are ratio-based.
- **Stage status** vs **Division status**: division-level `division_status` is a coarse checklist (`draft` … `completed`); each **Stage** has its own five-step **Stage status** that gates the Division detail workspace per [`docs/data-model.md`](docs/data-model.md#stage-status-lifecycle).
