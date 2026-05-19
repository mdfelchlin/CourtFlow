# CourtFlow implementation backlog

Gap analysis against [`product-overview.md`](product-overview.md), [`happy-path.md`](happy-path.md), [`data-model.md`](data-model.md), and [`features/`](features/). Use this as the working task list until V1 matches the docs.

**Legend:** `[ ]` not started · `[~]` partial · `[x]` done (update as you ship)

**Last reviewed:** 2026-05-19 (codebase snapshot on `main` working tree)

---

## Executive summary

CourtFlow has a solid **foundation**: Clerk auth, multi-tenant API, most admin routes, CSV import, basic schedule/brackets/standings, tournament publication, and public landing/event pages. The largest gaps are **not greenfield features** but **closing the distance between documented target behavior and what ships today**:

1. **Domain model drift** — Many tables exist (`stages`, `division_rankings`, `pool_entries`, `bracket_seed_slots`, `match_assignments`, `waves`) but runtime code still uses legacy paths (`Team.SeedHint`, `Team.PoolId`, court/time on `Match`, `bracket_nodes` advancement).
2. **Division detail** — Documented structure-first wizard; shipped page is a rankings + Pools/Brackets prototype (`DivisionDetail.razor`).
3. **Schedule versioning** — Documented preview → publish on `ScheduleVersion` + `match_assignments`; generate/move still centers on `Match` rows.
4. **Tournament day ops** — Court status dashboard, reflow preview, work-team rotation, match clock, and print outputs are documented but minimal or absent in UI.
5. **Public polish** — Teams tab, my-team filter, past events, schedule gated on published schedule version.

Work in **happy-path order** when possible so each milestone is demoable end-to-end.

---

## Phase 0 — Foundations & model alignment

These unblock almost every feature PRD. Do before large UI rebuilds.

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [ ] | F-01 | **Pick canonical team/ranking model** — Either wire `division_rankings` + `tournament_teams` per data model, or update docs if `Team.SeedHint` stays. Imports, division detail, and snake seeding must use one source. | [`data-model.md` §4–5](data-model.md#4-team-import-and-event-entry), [`division-detail.md`](features/division-detail.md) |
| [ ] | F-02 | **Introduce Stage lifecycle in application layer** — Create default pool + bracket stages per division; persist `stage_id` on matches; enforce status transitions (`draft` → `configured` → `seeded` → `in_progress` → `completed`). | [`data-model.md` §6](data-model.md#6-stages-the-round-container), ADR-0003, ADR-0005 |
| [ ] | F-03 | **Waves API + tournament editor panel** — CRUD `waves`; pin divisions to waves; replace enum-only `Wave` on teams where doc says first-class `waves` table. | [`happy-path.md` §2](happy-path.md), [`tournaments.md`](features/tournaments.md) |
| [ ] | F-04 | **Unify scheduling source of truth** — Generate and publish into `match_assignments` under a `ScheduleVersion`; public schedule reads published version, not ad-hoc `Match` court/time. | [`happy-path.md` §13](happy-path.md), [`schedule.md`](features/schedule.md), ADR-0001 |
| [ ] | F-05 | **Schedule version lifecycle** — Implement `draft` → `preview` → `published` → `superseded` transitions; `EnsurePublishedAsync` / publish endpoint; fork-on-published when structure changes. | [`data-model.md` §9](data-model.md#schedule-version-status-lifecycle) |
| [ ] | F-06 | **Resolve Team vs TournamentTeam** — Repairs use `TournamentTeam`; UI/API mostly use `Team`. Align withdrawals and roster displays. | [`repairs.md`](features/repairs.md), [`teams.md`](features/teams.md) |

---

## Phase 1 — Setup path (happy path §1–6)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [~] | S-01 | Tournaments CRUD + venues/courts inline | [`tournaments.md`](features/tournaments.md) |
| [ ] | S-02 | Tournament editor: **Waves** panel (depends F-03) | [`tournaments.md`](features/tournaments.md) |
| [~] | S-03 | Tournament **publish / unpublish / archive / restore** strip — add public URL copy, overflow actions, delete guard when published | [`tournaments.md`](features/tournaments.md) |
| [~] | S-04 | Divisions list — format/stage hint column, wave column | [`motion`](features/divisions.md) |
| [~] | S-05 | CSV import both formats + client preview | [`imports.md`](features/imports.md) |
| [ ] | S-06 | Imports write **division_rankings** (not only `SeedHint`) per F-01 | [`imports.md`](features/imports.md) |
| [~] | S-07 | Teams page: filters, add/edit, withdraw → repair | [`teams.md`](features/teams.md), [`repairs.md`](features/repairs.md) |
| [ ] | S-08 | **WithdrawTeamDialog** + **CourtOutageDialog** (replace GUID prompts on Repairs page) | [`ui/dialogs.md`](ui/dialogs.md), [`repairs.md`](features/repairs.md) |

---

## Phase 2 — Division structure (happy path §6–10)

Rebuild `DivisionDetail.razor` toward [`division-detail.md`](features/division-detail.md) without changing the route.

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [~] | D-01 | Rankings panel with drag-reorder (today: `Team` order / `SeedHint`) | [`division-detail.md` · Rankings](features/division-detail.md#rankings-panel-left-persistent) |
| [ ] | D-02 | **Step 1 — Pool structure** wizard: pool count, `target_size`, names, drag-reorder, placeholder slot labels | [`division-detail.md` · Step 1](features/division-detail.md#step-1--pool-structure) |
| [ ] | D-03 | **Pool courts strip** — `pool_court_allocations` multi-select per pool | [`division-detail.md`](features/division-detail.md), [`data-model.md`](data-model.md) |
| [ ] | D-04 | **Step 2 — Bracket structure** wizard: bracket types, team counts, BYE, drag seed positions | [`division-detail.md` · Step 2](features/division-detail.md#step-2--bracket-structure) |
| [ ] | D-05 | **BracketSlotOverride** table + UI (replace free-text GUID fields); audit log | [`division-detail.md`](features/division-detail.md), ADR-0005 |
| [ ] | D-06 | **Generate brackets** → materialize `bracket_seed_slots` + bracket-tree `matches` + `match_participant_sources` | [`data-model.md` §8](data-model.md#when-matches-are-created), ADR-0005 |
| [ ] | D-07 | **Step 3 — Advancement rules** editor (wildcard pools, `order_by`, presets, blocking-reason strip) | [`division-detail.md` · Step 3](features/division-detail.md#step-3--advancement-rules) |
| [ ] | D-08 | **Snake Rankings into Pools** — write `pool_entries`, stage `configured` → `seeded`, generate pool RR matches | [`happy-path.md` §10](happy-path.md) |
| [ ] | D-09 | **Resolve Bracket Slots** — write `resolved_tournament_team_id` from rules + overrides | [`happy-path.md` §15](happy-path.md) |
| [ ] | D-10 | Stage status chips + progress bar; lock rankings when stage `in_progress` | [`division-detail.md`](features/division-detail.md) |
| [ ] | D-11 | Standings preview on division detail respects **tiebreaker_preset** | [`standings.md`](features/standings.md) |

---

## Phase 3 — Schedule & publication (happy path §12–13)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [~] | SCH-01 | Schedule grid: generate pool RR, conflict check, move preview/apply | [`schedule.md`](features/schedule.md) |
| [ ] | SCH-02 | **Version chip + Versions panel** (list versions, stale warnings) | [`schedule.md`](features/schedule.md) |
| [ ] | SCH-03 | **Publish schedule** action (`preview` → `published`); public Schedule tab gated on published version | [`public-event.md`](features/public-event.md), F-04 |
| [ ] | SCH-04 | Schedule cell: **Start match**, **Score**, **Change referee** | [`schedule.md`](features/schedule.md) |
| [ ] | SCH-05 | Fork new schedule version when editing after publish (repair path) | [`schedule.md`](features/schedule.md), [`repairs.md`](features/repairs.md) |
| [~] | SCH-06 | Brackets admin page + `BracketView` (separate from division wizard) | [`brackets.md`](features/brackets.md) |

---

## Phase 4 — Tournament day (happy path §14)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [~] | OPS-01 | Court status API + basic table page | [`court-status.md`](features/court-status.md) |
| [ ] | OPS-02 | Court status **Cards view** + **Grid view** toggle; default Grid on tournament day | [`court-status.md`](features/court-status.md) |
| [ ] | OPS-03 | **TV mode** route (chrome-stripped grid for wall monitor) | [`court-status.md`](features/court-status.md) |
| [ ] | OPS-04 | **Match clock** — Start / Mark complete; `started_at`; auto-set on first score if missing | [`court-status.md`](features/court-status.md), [`matches.md`](features/matches.md) |
| [ ] | OPS-05 | **Reflow preview** — real `IReflowPreviewEngine`, drawer UI, Apply → `RepairJob` | ADR-0004, [`court-status.md`](features/court-status.md) |
| [ ] | OPS-06 | Integrate **Delay court** from dashboard (today: `DelayCourtDialog` exists but not wired to court-status) | [`court-status.md`](features/court-status.md) |
| [~] | OPS-07 | Matches list + score dialog | [`matches.md`](features/matches.md) |
| [ ] | OPS-08 | Matches list: move, start, change referee | [`matches.md`](features/matches.md) |
| [~] | OPS-09 | Standings (admin + public) | [`standings.md`](features/standings.md) |
| [~] | OPS-10 | Repairs list + withdrawal/outage API | [`repairs.md`](features/repairs.md) |
| [ ] | OPS-11 | Repair: **referee / work-team reassignment** trigger + dialog | [`work-teams.md`](features/work-teams.md), [`repairs.md`](features/repairs.md) |
| [ ] | OPS-12 | **Work team auto-rotation** on pool/bracket match generation; review panel on division detail | [`work-teams.md`](features/work-teams.md) |
| [ ] | OPS-13 | **Print outputs** (pool sheets, score sheets, brackets, court runs, work rotation, standings) | [`print-outputs.md`](features/print-outputs.md) |

---

## Phase 5 — Public surface (happy path §12, §17)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [~] | PUB-01 | Public landing — current/upcoming grid | [`public-landing.md`](features/public-landing.md) |
| [ ] | PUB-02 | Public landing — **Past events** (archived tournaments) | [`public-landing.md`](features/public-landing.md) |
| [~] | PUB-03 | Public event detail — Schedule / Brackets / Standings tabs | [`public-event.md`](features/public-event.md) |
| [ ] | PUB-04 | Public event — **Teams** tab (divisions + pool rosters) | [`public-event.md`](features/public-event.md) |
| [ ] | PUB-05 | Public event — **My-team filter** (mobile-first lookup, next-match card) | [`public-event.md`](features/public-event.md) |
| [ ] | PUB-06 | Per-tab readiness gates (schedule only after published `ScheduleVersion`, etc.) | [`public-event.md`](features/public-event.md), ADR-0006 |
| [ ] | PUB-07 | **Auto-archive** job N days after `end_date` | [`happy-path.md` §17](happy-path.md) |

---

## Phase 6 — Clone & templates (happy path clone shortcut)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [~] | TPL-01 | `POST /tournaments/{id}/clone` + UI button (copies venues, pools, teams today) | [`tournaments.md`](features/tournaments.md) |
| [ ] | TPL-02 | Clone **structure only** — divisions, stages, pools, brackets, advancement rules, waves; **no teams** | [`data-model.md` §12](data-model.md#12-tournament-templates), [`happy-path.md`](happy-path.md) |
| [ ] | TPL-03 | **Capture template** — persist `template_*` child graph from tournament | [`data-model.md` §12](data-model.md#12-tournament-templates) |
| [ ] | TPL-04 | **Create tournament from template** + `CloneTournamentDialog` | [`ui/dialogs.md`](ui/dialogs.md) |

---

## Phase 7 — Application seams & tests (ADRs)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [ ] | ENG-01 | **StageMaterialization** module — pool + bracket materialize actions share one seam | ADR-0005, [`architecture.md`](architecture.md) |
| [ ] | ENG-02 | **DivisionReadiness** / **PublicDivisionReadiness** read models | ADR-0006 |
| [ ] | ENG-03 | **Tiebreaker** read model — preset-driven ordering for standings + advancement re-rank | ADR-0006, [`standings.md`](features/standings.md) |
| [ ] | ENG-04 | **DelayCalculation** read model for court status chips | ADR-0006 |
| [ ] | ENG-05 | Engine integration tests per ADR-0007 (scheduling, brackets, reflow contract) | ADR-0007 |
| [ ] | ENG-06 | Wire domain events / outbox to product flows (publish, withdraw, reflow) | [`data-model.md` · domain events](data-model.md#domain-events-and-integration-hooks) |

---

## Phase 8 — Infrastructure & ops

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [~] | INF-01 | Clerk JWT on API + WASM client | [`auth.md`](features/auth.md), [`azure-plan.md`](azure-plan.md) |
| [~] | INF-02 | Terraform + manual deploy workflows (SWA + Container Apps) | [`azure-plan.md`](azure-plan.md) |
| [ ] | INF-03 | Turso/libSQL as default non-local environment; retire legacy DB paths | [`azure-plan.md`](azure-plan.md) |
| [ ] | INF-04 | CI deploy on merge (not only `workflow_dispatch`) | [`azure-plan.md`](azure-plan.md) |
| [ ] | INF-05 | Observability: structured logs + basic metrics for schedule gen, conflicts, delay | [`product-overview.md` §5](product-overview.md) |
| [ ] | INF-06 | Aspire AppHost / local dev story matches [`local-development.md`](local-development.md) | [`local-development.md`](local-development.md) |

---

## Phase 9 — Doc hygiene (keep docs truthful)

| Status | ID | Task |
|--------|-----|------|
| [ ] | DOC-01 | Update [`division-detail.md` § Gap](features/division-detail.md#gap-from-current-v1-implementation) — reference `DivisionDetail.razor`, not `DivisionEdit.razor` |
| [ ] | DOC-02 | Update [`repairs.md`](features/repairs.md) — `/repairs` route exists |
| [ ] | DOC-03 | When F-01/F-04 land, reconcile [`product-overview.md` §3.3](product-overview.md#33-planned-but-not-yet-shipped) — move shipped items out of "planned" |

---

## Suggested sprint order (first 4 milestones)

1. **Milestone A — Model truth** (F-01, F-02, F-03): Stages + waves + one ranking source. Demo: division shows stage chips; tournament has AM/PM waves.
2. **Milestone B — Division wizard MVP** (D-02 → D-08): Structure-first flow through snake into pools. Demo: happy path §6–10 without bracket resolve.
3. **Milestone C — Schedule publish** (F-04, F-05, SCH-02, SCH-03, PUB-06): Published schedule drives public tab. Demo: happy path §13.
4. **Milestone D — Tournament day** (OPS-02–OPS-05, OPS-12): Grid court status + reflow. Demo: happy path §14 core loop.

Defer **print outputs** (OPS-13) and **template capture** (TPL-03–04) until Milestone D unless a pilot customer needs them sooner.

---

## Out of scope (V1 product — do not track here)

Per [`product-overview.md` §3.2](product-overview.md#32-out-of-scope-v1): registration/payments, external roster integrations, multi-sport, AES import, realtime push.

**Engine-ready, wizard-deferred** (schema only until division wizard matures): Power Pool, Challenge Phase, multi-stage beyond 1 pool + 1 bracket — see [`happy-path.md` · Future expansions](happy-path.md#future-expansions-engine-ready-wizard-deferred).

---

## How to use this file

1. Pick the next open task in the current milestone.
2. Mark `[~]` when a PR partially delivers it; `[x]` when acceptance matches the linked feature PRD.
3. If implementation deliberately diverges from docs, **update the feature PRD or data model first**, then adjust this backlog — not the other way around ([`README.md` conventions](README.md#conventions)).
