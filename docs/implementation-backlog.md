# CourtFlow implementation backlog

Gap analysis against [`product-overview.md`](product-overview.md), [`happy-path.md`](happy-path.md), [`data-model.md`](data-model.md), and [`features/`](features/). Use this as the working task list until V1 matches the docs.

**Legend:** `[ ]` not started · `[~]` partial · `[x]` done (update as you ship)

**Last reviewed:** 2026-05-19 (V1 backlog closure: tiebreaker preset, templates, domain events, ops/public polish)

---

## Executive summary

CourtFlow V1 core is **shipped end-to-end**: Clerk auth, multi-tenant API, division structure wizard, schedule versioning with public gates, tournament-day court status + reflow, work teams, public event surface, templates (capture + create), and domain-event outbox wiring for publish/withdraw/reflow.

**Remaining intentional deferrals:** print outputs (OPS-13), Power Pool / Challenge Phase / multi-stage beyond 1 pool + 1 bracket, and V1 out-of-scope items (registration, multi-sport, AES import, SignalR).

**Minor polish still possible** (not blocking V1 demo): bracket drag-reorder UI, full MudStepper on division wizard, server-side import preview endpoint, Terraform production hardening notes.

---

## Phase 0 — Foundations & model alignment

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | F-01 | **Pick canonical team/ranking model** | [`data-model.md` §4–5](data-model.md#4-team-import-and-event-entry) |
| [x] | F-02 | **Introduce Stage lifecycle in application layer** | [`data-model.md` §6](data-model.md#6-stages-the-round-container) |
| [x] | F-03 | **Waves API + tournament editor panel** | [`happy-path.md` §2](happy-path.md) |
| [x] | F-04 | **Unify scheduling source of truth** | [`happy-path.md` §13](happy-path.md), ADR-0001 |
| [x] | F-05 | **Schedule version lifecycle** | [`data-model.md` §9](data-model.md#schedule-version-status-lifecycle) |
| [x] | F-06 | **Resolve Team vs TournamentTeam** — withdraw on Teams page; repairs use `TournamentTeamId` | [`repairs.md`](features/repairs.md) |

---

## Phase 1 — Setup path (happy path §1–6)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | S-01 | Tournaments CRUD + venues/courts inline; `CloneTournamentDialog` with structure-only | [`tournaments.md`](features/tournaments.md) |
| [x] | S-02 | Tournament editor: **Waves** panel | [`tournaments.md`](features/tournaments.md) |
| [x] | S-03 | Tournament **publish / unpublish / archive / restore** strip | [`tournaments.md`](features/tournaments.md) |
| [x] | S-04 | Divisions list — format/stage hint column, wave column | [`divisions.md`](features/divisions.md) |
| [x] | S-05 | CSV import both formats + client preview | [`imports.md`](features/imports.md) |
| [x] | S-06 | Imports write **division_rankings** | [`imports.md`](features/imports.md) |
| [x] | S-07 | Teams page: filters, add/edit, withdraw → repair | [`teams.md`](features/teams.md) |

---

## Phase 2 — Division structure (happy path §6–10)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | D-01 | Rankings panel with drag-reorder → `division_rankings` | [`division-detail.md`](features/division-detail.md) |
| [x] | D-02 | **Step 1 — Pool structure** wizard | [`division-detail.md`](features/division-detail.md) |
| [x] | D-03 | **Pool courts strip** — `pool_court_allocations` | [`division-detail.md`](features/division-detail.md) |
| [x] | D-04 | **Step 2 — Bracket structure** wizard | [`division-detail.md`](features/division-detail.md) |
| [x] | D-05 | **BracketSlotOverride** API + UI path via override endpoint | [`division-detail.md`](features/division-detail.md) |
| [x] | D-06 | **Generate brackets** → materialize slots + tree | [`data-model.md` §8](data-model.md#when-matches-are-created) |
| [x] | D-07 | **Step 3 — Advancement rules** editor + blocking strip | [`division-detail.md`](features/division-detail.md) |
| [x] | D-08 | **Snake Rankings into Pools** | [`happy-path.md` §10](happy-path.md) |
| [x] | D-09 | **Resolve Bracket Slots** with pool-stage gating | [`happy-path.md` §15](happy-path.md) |
| [x] | D-10 | Stage status chips; lock rankings when stage `in_progress` | [`division-detail.md`](features/division-detail.md) |
| [x] | D-11 | Standings preview respects **tiebreaker_preset** | [`standings.md`](features/standings.md) |

---

## Phase 3 — Schedule & publication (happy path §12–13)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | SCH-01 | Schedule grid: generate, conflicts, move preview/apply via assignments | [`schedule.md`](features/schedule.md) |
| [x] | SCH-02 | **Version chip + Versions panel** | [`schedule.md`](features/schedule.md) |
| [x] | SCH-03 | **Publish schedule** action; public Schedule tab gated | [`public-event.md`](features/public-event.md) |
| [x] | SCH-04 | Schedule cell: **Start match**, **Score**, **Change referee** | [`schedule.md`](features/schedule.md) |
| [x] | SCH-05 | Fork new schedule version when editing after publish | [`schedule.md`](features/schedule.md) |
| [x] | SCH-06 | Brackets admin page + `BracketView`; manage-in-division link | [`brackets.md`](features/brackets.md) |

---

## Phase 4 — Tournament day (happy path §14)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | OPS-01 | Court status API + grid/cards/TV | [`court-status.md`](features/court-status.md) |
| [x] | OPS-02 | Court status **Cards view** + **Grid view** toggle | [`court-status.md`](features/court-status.md) |
| [x] | OPS-03 | **TV mode** route | [`court-status.md`](features/court-status.md) |
| [x] | OPS-04 | **Match clock** | [`court-status.md`](features/court-status.md) |
| [x] | OPS-05 | **Reflow preview** + Apply → `RepairJob` | ADR-0004 |
| [x] | OPS-06 | **Delay court** from dashboard | [`court-status.md`](features/court-status.md) |
| [x] | OPS-07 | Matches list + score dialog | [`matches.md`](features/matches.md) |
| [x] | OPS-08 | Matches list: move, start, change referee | [`matches.md`](features/matches.md) |
| [x] | OPS-09 | Standings (admin + public) with tiebreaker chip | [`standings.md`](features/standings.md) |
| [x] | OPS-10 | Repairs list + withdrawal/outage API | [`repairs.md`](features/repairs.md) |
| [x] | OPS-11 | Repair: **referee / work-team reassignment** | [`work-teams.md`](features/work-teams.md) |
| [x] | OPS-12 | **Work team auto-rotation** | [`work-teams.md`](features/work-teams.md) |
| [ ] | OPS-13 | **Print outputs** (deferred) | [`print-outputs.md`](features/print-outputs.md) |

---

## Phase 5 — Public surface (happy path §12, §17)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | PUB-01 | Public landing — stats on cards (venues/divisions/teams) | [`public-landing.md`](features/public-landing.md) |
| [x] | PUB-02 | Public landing — **Past events** | [`public-landing.md`](features/public-landing.md) |
| [x] | PUB-03 | Public event detail — Schedule / Brackets / Standings tabs + readiness | [`public-event.md`](features/public-event.md) |
| [x] | PUB-04 | Public event — **Teams** tab | [`public-event.md`](features/public-event.md) |
| [x] | PUB-05 | Public event — **My-team filter** | [`public-event.md`](features/public-event.md) |
| [x] | PUB-06 | Per-tab readiness gates | [`public-event.md`](features/public-event.md) |
| [x] | PUB-07 | **Auto-archive** job | [`happy-path.md` §17](happy-path.md) |

---

## Phase 6 — Clone & templates (happy path clone shortcut)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | TPL-01 | `POST /tournaments/{id}/clone` + `CloneTournamentDialog` (structure-only) | [`tournaments.md`](features/tournaments.md) |
| [x] | TPL-02 | Clone **structure only** | [`data-model.md` §12](data-model.md#12-tournament-templates) |
| [x] | TPL-03 | **Capture template** — `StructureJson` snapshot | [`data-model.md` §12](data-model.md#12-tournament-templates) |
| [x] | TPL-04 | **Create tournament from template** + `CreateFromTemplateDialog` | [`ui/dialogs.md`](ui/dialogs.md) |

---

## Phase 7 — Application seams & tests (ADRs)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | ENG-01 | **StageMaterialization** module | ADR-0005 |
| [x] | ENG-02 | **DivisionReadiness** / **PublicDivisionReadiness** | ADR-0006 |
| [x] | ENG-03 | **Tiebreaker** read model | ADR-0006 |
| [x] | ENG-04 | **DelayCalculation** (`IDelayCalculation`) | ADR-0006 |
| [x] | ENG-05 | Engine integration tests | ADR-0007 |
| [x] | ENG-06 | Domain events / outbox (publish, withdraw, reflow, schedule publish) | [`data-model.md`](data-model.md) |

---

## Phase 8 — Infrastructure & ops

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | INF-01 | Clerk JWT on API + WASM client | [`auth.md`](features/auth.md) |
| [~] | INF-02 | Terraform + manual deploy workflows | [`azure-plan.md`](azure-plan.md) — note in azure-plan sufficient for V1 |
| [x] | INF-03 | Turso/libSQL default non-local (`appsettings.Production.json`) | [`azure-plan.md`](azure-plan.md) |
| [x] | INF-04 | CI deploy on merge | [`azure-plan.md`](azure-plan.md) |
| [x] | INF-05 | Observability: Serilog + `CourtFlowMetrics` (schedule/conflicts) | [`product-overview.md` §5](product-overview.md) |
| [x] | INF-06 | Aspire AppHost + DbMigrator on startup | [`local-development.md`](local-development.md) |

---

## Phase 9 — Doc hygiene

| Status | ID | Task |
|--------|-----|------|
| [x] | DOC-01 | Update [`division-detail.md` § Gap](features/division-detail.md) |
| [x] | DOC-02 | Update [`repairs.md`](features/repairs.md) |
| [x] | DOC-03 | Reconcile [`product-overview.md` §3.3](product-overview.md#33-planned-but-not-yet-shipped) |

---

## Out of scope (V1 product — do not track here)

Per [`product-overview.md` §3.2](product-overview.md#32-out-of-scope-v1): registration/payments, external roster integrations, multi-sport, AES import, realtime push.

**Engine-ready, wizard-deferred:** Power Pool, Challenge Phase, multi-stage beyond 1 pool + 1 bracket.

**Explicitly deferred in this pass:** OPS-13 print outputs.

---

## How to use this file

1. Pick the next open task in the current milestone.
2. Mark `[~]` when a PR partially delivers it; `[x]` when acceptance matches the linked feature PRD.
3. If implementation deliberately diverges from docs, **update the feature PRD or data model first**, then adjust this backlog.
