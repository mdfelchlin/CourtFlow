# Happy path: from CSV to a built tournament

This is the **end-to-end walkthrough** of an organizer setting up a brand-new volleyball tournament in CourtFlow, from creating the event to having all matches ready to play. It is the cohesion test for the docs — if you read this and the linked feature PRDs, you should be able to recreate the app.

Vocabulary is pinned in [`../CONTEXT.md`](../CONTEXT.md). The canonical schema is [`data-model.md`](data-model.md). Every step here is a thin pointer at the doc that owns it.

---

## At a glance

```
Tournament (DRAFT, invisible to public) → Venues → Courts → Waves      (tournaments.md)
   ↓                          ↑ alternative: Clone from prior tournament (tournament templates)
Divisions (gender / age / skill / wave / tiebreaker preset)            (divisions.md)
   ↓
Import teams (CSV)                                                     (imports.md)
   ↓
Rankings (drag-reorder, persistent panel)                              (division-detail.md)
   ↓
Pool Structure  →  Bracket Structure  →  Advancement Rules             (division-detail.md, structure-first wizard)
   ↓
Snake Rankings into Pools         (writes pool_entries + generates pool round-robin matches)
   ↓
WorkTeam auto-rotation populated on every pool match                   (work-teams.md)
   ↓
Generate brackets                 (writes bracket_seed_slots + generates bracket tree matches)
   ↓
Court allocation per pool / division                                  (division-detail Step 1 courts strip; DivisionDialog; schedule + data-model §9)
   ↓
Publish tournament to public      (tournaments.status: draft → published; Teams tab now visible)
   ↓
Schedule generate → preview → publish     (Schedule tab becomes visible to public)
   ↓
Tournament day: Start match clock, score, reflow on delays, repair    (court-status.md, matches.md, repairs.md)
   ↓
Auto-archive N days after end_date        (tournaments.status: published → archived; moves to Past Events)
```

The **Publish to public** step is a hard line — everything to the left of it is private to the organizer; everything to the right is on the public event page (per the per-tab readiness gates documented in [`features/public-event.md`](features/public-event.md)). **Court allocation** detail: Step 1 **Courts strip** on each Pool card and division scheduling fields — [`features/division-detail.md`](features/division-detail.md), [`ui/dialogs.md` · DivisionDialog](ui/dialogs.md#divisiondialog), [`features/schedule.md`](features/schedule.md), [`data-model.md` §9](data-model.md#9-scheduling-and-assignment). The organizer can choose to publish before or after the schedule is ready: publishing earlier makes Teams visible to the public while the schedule remains hidden behind "Schedule hasn't been published yet."

**Cloning a prior tournament** short-circuits the first half of this flow — clicking **Clone** on the Tournaments table instantiates a new `draft` tournament pre-filled with the source tournament's divisions, stages, pools, brackets, advancement rules, waves, and tiebreaker presets. The organizer still imports teams, ranks them, materializes pools / brackets, and publishes from scratch — only structure is reused. See [`features/tournaments.md`](features/tournaments.md#interactions) and [`data-model.md` §12 Tournament templates](data-model.md#12-tournament-templates).

---

## 0. Sign in

The admin signs in via Clerk. The header tournament selector starts empty for new accounts.

→ [`features/auth.md`](features/auth.md)

---

## 1. Create the Tournament and add Venues + Courts

The admin opens **Tournaments**, clicks **Add**, fills in name, dates, time zone, and default match duration. After saving, the editor reveals the **Venues & courts** panel. They add each venue and inline-add the courts under it.

Writes: `tournaments`, `tournament_venues`, `venues`, `courts`, `court_availability_windows`.

→ [`features/tournaments.md`](features/tournaments.md), [`data-model.md` §2](data-model.md#2-tournament-creation)

---

## 2. Define Waves (AM / PM / 1 / 2)

In the tournament editor's **Waves** panel, the admin adds the waves their tournament uses — typically `AM` and `PM`, but some events use `1` / `2`. Waves are first-class scheduling constraints; pinning a division to a wave keeps its matches inside that session.

Writes: `waves`.

→ [`features/tournaments.md`](features/tournaments.md), [`data-model.md` §2](data-model.md#2-tournament-creation), **Wave** in [`../CONTEXT.md`](../CONTEXT.md)

---

## 3. Create Divisions

The admin opens **Divisions** and adds each competitive grouping (e.g. *Girls 14U Open*, *Boys 18U Club*). For each division they set format flags, scheduling defaults, **`tiebreaker_preset`**, and the **wave** it belongs to. They do **not** need to define pools or brackets yet — those live in the structure-first wizard.

Writes: `tournament_divisions`.

→ [`features/divisions.md`](features/divisions.md), [`data-model.md` §3](data-model.md#3-division-creation)

---

## 4. Import teams (CSV)

The admin uploads a CSV via **Imports**. Two formats are supported:

- **Predefined-pools** (`PoolName`, optional `SeedHint`).
- **Ranked-teams** (`Rank`).

The importer parses, validates, and writes `clubs` / `teams` / `tournament_teams` / `division_rankings`. **Ranking** is written directly from `Rank` (ranked-teams) or derived from `SeedHint` order (predefined-pools). There is no `tournament_teams.seed` column.

→ [`features/imports.md`](features/imports.md), [`data-model.md` §4](data-model.md#4-team-import-and-event-entry), [`data-model.md` §5](data-model.md#5-ranking-and-seeding)

---

## 5. Manually add, edit, or withdraw teams

The **Teams** page handles individual TournamentTeam edits (add a late entry, fix a misspelling, withdraw a team via a **RepairJob**).

→ [`features/teams.md`](features/teams.md), [`features/repairs.md`](features/repairs.md)

---

## 6. Adjust Rankings (persistent panel on Division detail)

The admin opens a division's detail page. The **Rankings** panel on the left lists every TournamentTeam in `division_rankings.rank` order. They drag rows to reorder until they're happy with the seeding input, then **Save**.

Rankings are independent of the wizard — they update any time, and lock once the first stage transitions to `in_progress`.

→ [`features/division-detail.md` · Rankings panel](features/division-detail.md#rankings-panel-left-persistent)

---

## 7. Structure-first wizard — Step 1: Pool Structure

The wizard's first step authors **pool structure**, with **no teams required**:

- Pool count, per-pool `target_size`, names.
- Drag-reorder pools.
- Per-pool **Courts** multi-select (`pool_court_allocations`) for which courts the pool plays on.

Slot rows render placeholder labels (`Pool A · 1`, `Pool A · 2`, …) until teams are placed.

Writes: `pools` (with `target_size`), `pool_settings`, `pool_court_allocations`.

→ [`features/division-detail.md` · Step 1](features/division-detail.md#step-1--pool-structure), [`data-model.md` §6.1](data-model.md#61-pool-generation-inside-a-pool-stage)

---

## 8. Structure-first wizard — Step 2: Bracket Structure

The wizard's second step authors **bracket structure**, still **no teams required**:

- Bracket count (e.g. Gold + Silver), per-bracket `team_count`, type (single elim / double elim / grand-final reset / third-place match / if-necessary final).
- Drag-reorder bracket seed positions within a bracket.
- Per-slot affordances: **Mark as BYE**, **Pin team to this slot** (BracketSlotOverride, audit-logged).

The **Generate brackets** action materializes `bracket_seed_slots` and **generates the full bracket-tree `matches`** (round 1 via `bracket_seed_slot` participant sources; round 2+ via `match_winner` / `match_loser` sources).

Writes: `brackets`, `bracket_seed_slots`, `matches` (bracket tree), `match_participant_sources` for the tree.

→ [`features/division-detail.md` · Step 2](features/division-detail.md#step-2--bracket-structure), [`data-model.md` §7](data-model.md#7-bracket-configuration-and-advancement-inside-a-bracket-stage), [`data-model.md` §8 · When matches are created](data-model.md#when-matches-are-created)

---

## 9. Structure-first wizard — Step 3: Advancement Rules

The wizard's third step wires pool finishes to bracket slots:

- Quick presets: **Top N from each pool → Gold**, **Remaining → Silver**, **All 1st-place finishers → Bracket X**, **Re-rank top-N by tiebreaker**.
- Editor grid for fine-tuning: source pool (or wildcard), finish position, target bracket, target slot start, **`order_by`** (`pool_sequence` *default* / `tiebreaker_rank`), priority.
- Continuous validation feeds the bracket Stage's blocking-reasons strip.

Writes: `advancement_rules`.

The Stage status moves to `configured` once Step 3 validates.

→ [`features/division-detail.md` · Step 3](features/division-detail.md#step-3--advancement-rules), [`data-model.md` §7 · Wildcard rule expansion](data-model.md#wildcard-rule-expansion)

---

## 10. Snake Rankings into Pools (materialize pool play)

With Rankings populated and Step 1 structure valid, the admin clicks **Snake Rankings into Pools** in the left panel. This:

1. Writes `pool_entries` per the configured `seeding_method` (snake by default).
2. Transitions the pool stage from `configured` → `seeded`.
3. **Generates pool-play matches** — a full round-robin per pool. Each match writes `match_participant_sources` rows that point directly at the placed `pool_entries.tournament_team_id`s.

Re-runnable. Warns before overwriting manual placements; matches already started block the regenerate (requires a **RepairJob**).

Writes: `pool_entries`, `matches` (pool play), `match_participant_sources`.

→ [`features/division-detail.md` · Materialize actions](features/division-detail.md#materialize-actions-left-column-below-rankings), [`data-model.md` §8 · When matches are created · Trigger 1](data-model.md#when-matches-are-created)

---

## 11. Initial WorkTeam (referee) assignments

Once pool matches exist, `match_officials.work_team_tournament_team_id` is auto-populated per the division's `ref_assignment_mode` — typically `next_in_pool` for pool matches and `loser_of_prior_match_on_court` for bracket matches. Each match's **WorkTeam** is another **TournamentTeam** in the pool (for pool matches) that fields the officiating crew (scorer, libero tracker, two line judges, and R2 in most divisions). R1 and R2 can be overridden to a different team when region rules require adult coverage (e.g. 12U/13U playoffs). The organizer reviews and tweaks assignments in three places: the Division detail **Work rotation** panel, the Schedule cell's **Change referee** action, and the Matches list's **Change referee** action. Any change beyond a single match writes a **RepairJob** with `trigger_type = 'work_team_reassignment'` for the audit trail.

→ [`features/work-teams.md`](features/work-teams.md), [`data-model.md` §8 · Work team auto-rotation](data-model.md#work-team-auto-rotation), [`features/repairs.md` · Trigger 3](features/repairs.md#trigger-3-referee-reassignment-workteam--r1--r2)

---

## 12. Publish the tournament to public

The admin opens the tournament editor and clicks **Publish to public** in the Publish status strip. The confirmation modal explains "It will appear on the public landing page immediately. You can move it back to draft later." On confirm:

- `tournaments.status` transitions `draft → published`.
- `published_at` is recorded.
- The tournament appears on `/` (public landing).
- The public Event detail page at `/events/{id}` starts returning 200 instead of 404.

The public sees the tournament with **Teams visible immediately** (assuming pools are seeded; the Teams tab shows divisions and pool rosters). The Schedule, Brackets, and Standings tabs render their empty-state copy until their underlying data is ready:

- **Schedule tab** stays at "Schedule hasn't been published yet." until step 13 publishes a `ScheduleVersion`.
- **Brackets tab** shows the bracket *shape* (with TBD slots) if Generate brackets has run in Step 2 of the wizard.
- **Standings tab** shows "Standings appear once pool matches are scored." until pool play starts.

Publishing is reversible (**Move to draft**) with a strong confirm, but **shared `/events/{id}` links break** when a tournament leaves `published`, so the admin is warned. Reordering 12 and 13 is intentional and legitimate — many organizers publish the tournament with teams visible weeks before the schedule is finalized.

Writes: `tournaments.status`, `tournaments.published_at`. Emits `TournamentPublished`.

→ [`features/tournaments.md` · Publish status strip](features/tournaments.md#tournament-editor), [`data-model.md` §Tournament status lifecycle](data-model.md#tournament-status-lifecycle), [`features/public-landing.md`](features/public-landing.md), [`features/public-event.md` · Visibility model](features/public-event.md#visibility-model)

---

## 13. Schedule generate → preview → publish

The admin opens **Schedule** and clicks **Generate**. The engine creates a draft `ScheduleVersion` and writes `match_assignments` against it, running full-DB conflict checks. The admin reviews the **preview** state and clicks **Publish** when satisfied — at which point the draft becomes the current schedule **and the public Schedule tab starts showing real content**.

If structure changes after publish, a **RepairJob** is the path to amend.

Writes: `schedule_versions` (transitions `preview → published`), `match_assignments` mutations under that version.

→ [`features/schedule.md`](features/schedule.md), [`data-model.md` §9](data-model.md#9-scheduling-and-assignment), [`data-model.md` §Schedule version status lifecycle](data-model.md#schedule-version-status-lifecycle)

---

## 14. Tournament day: play, score, advance, repair

Once the schedule is published, the operational surface takes over. The TD's main screen is the **Court Status & Delay Dashboard** — every court's chip, current match, and "+N min behind" delta on one page. The dashboard ships with two layouts: **Cards view** (one card per court, action-dense, for the organizer walking the floor) and **Grid view** (court × time matrix, color-driven, for scanning the whole gym at once). On tournament day the default is **Grid view**, and a third option — **TV mode** — strips the chrome and projects the grid on a wall monitor at the scorer's table. See [`features/court-status.md`](features/court-status.md#layouts).

- **Start matches** explicitly from a court card, schedule cell, or matches row — writes `matches.started_at = now()` and transitions `match_status = 'in_progress'`. If scoring arrives without a Start press, the same value auto-fallback-writes on first set save. The Court Status dashboard's chips read these timestamps to color each court. ([`features/court-status.md`](features/court-status.md), [`data-model.md` §8 · Match clock writes](data-model.md#match-clock-writes))
- **Score matches** from the schedule cell or matches list. Scoring updates `match_sets` + `matches.winning_tournament_team_id`. ([`features/matches.md`](features/matches.md), [`features/schedule.md`](features/schedule.md))
- **Standings** update live, ordered by the division's **TiebreakerPreset**. ([`features/standings.md`](features/standings.md))
- **Move matches** between courts/times — creates new `match_assignments` rows on the current `ScheduleVersion`. ([`features/schedule.md`](features/schedule.md))
- **Reflow preview** when a court runs over — pick a court card on the dashboard, click **Reflow preview**, slide the affected court forward, review the cascade diff, and **Apply**. Writes a `schedule_reflow` **RepairJob**. ([`features/court-status.md` · Reflow preview drawer](features/court-status.md#reflow-preview-drawer))
- **Withdraw teams**, **mark courts unavailable**, **change referees / WorkTeam** — opens a **RepairJob**. ([`features/repairs.md`](features/repairs.md))
- **Print** pool sheets, score sheets, daily court runs, work-rotation sheets as needed. ([`features/print-outputs.md`](features/print-outputs.md))

The public surface picks up changes automatically — coaches and parents using the [My-team filter](features/public-event.md#my-team-filter-mobile-first-lookup) on `/events/{id}` see the next-match card update with delay deltas as the organizer scores and reflows.

---

## 15. Bridge to bracket play

When pool play completes for a stage:

- Every pool's `pool_entries.finish_position` is written.
- The pool stage transitions `in_progress → completed`.
- The admin clicks **Resolve Bracket Slots** in the left panel. This writes `bracket_seed_slots.resolved_tournament_team_id` per `advancement_rules` + any **BracketSlotOverride**s. The participant sources on round-1 bracket matches now resolve to real teams.
- Bracket play begins; `match_winner` / `match_loser` participant sources cascade as winners are determined.

→ [`features/division-detail.md` · Resolve Bracket Slots](features/division-detail.md#materialize-actions-left-column-below-rankings), [`data-model.md` §7 · Resolve Bracket Slots](data-model.md#resolve-bracket-slots-the-materialization-action)

---

## 16. Tournament complete

Every stage in every division has `status = completed`. Public surfaces continue to show the final schedule, brackets, and standings — `tournaments.status` is still `published` at this moment. History (every `ScheduleVersion`, `RepairJob`, `bracket_seed_slot_override`, ranking change) is preserved per the archival rules in [`data-model.md`](data-model.md#soft-delete-and-archival-strategy).

---

## 17. Auto-archive (or manual archive)

N days after `tournaments.end_date` (default `N = 14`, configurable per organization), the auto-archive job transitions `tournaments.status` from `published → archived`:

- The tournament leaves the active "Current & upcoming events" grid on the public landing.
- It appears in the **Past events** section on `/` (or is hidden entirely if the organization opted out of showing archived events).
- Deep-link `/events/{id}` still resolves; the hero card now carries an **Archived** chip.
- Admin pages remain editable through **RepairJob** / un-archive paths only.

The organizer can archive manually at any time before the auto-archive trigger using the **Archive tournament** action on the tournament editor. Un-archive restores to `published`.

Writes: `tournaments.status`, `tournaments.archived_at`. Emits `TournamentArchived`.

→ [`data-model.md` §Tournament status lifecycle](data-model.md#tournament-status-lifecycle) (auto-archive policy + transitions)

---

## Future expansions (engine-ready, wizard deferred)

- **Power Pool** — a second pool stage sourced from the first via `stages.seeding_source = 'prior_stage_finish_positions'`.
- **Challenge Phase** — inter-bracket play-in matches authored via `match_winner` / `match_loser` participant sources.
- **Multi-stage divisions beyond 1+1** — the schema places no cap on stage count.

All three ship in the V1 **schema**; the V1 **wizard** exposes only the 1 pool + 1 bracket case. See [`features/division-detail.md` · Future-wizard sections](features/division-detail.md#future-wizard-sections-engine-ready-wizard-deferred).

---

## Where to next

- **Just here for the data?** [`data-model.md`](data-model.md).
- **Just here for the UI?** [`features/`](features/).
- **Just here for ops?** [`azure-plan.md`](azure-plan.md), [`architectural-prd.md`](architectural-prd.md).
