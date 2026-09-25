# CourtFlow implementation backlog

Gap analysis against [`product-overview.md`](product-overview.md), [`happy-path.md`](happy-path.md), [`data-model.md`](data-model.md), and [`features/`](features/). Use this as the working task list until V1 matches the docs.

**Legend:** `[ ]` not started · `[~]` partial · `[x]` done (update as you ship)

**Last reviewed:** 2026-06-17 (ADR gap analysis → Phase 10 task list)

---

## Executive summary

CourtFlow V1 core is **shipped end-to-end**: Clerk auth, multi-tenant API, division structure wizard, schedule versioning with public gates, tournament-day court status + reflow, work teams, public event surface, templates (capture + create), and domain-event outbox wiring for publish/withdraw/reflow.

**Remaining intentional deferrals:** print outputs (OPS-13), Power Pool / Challenge Phase / multi-stage beyond 1 pool + 1 bracket, and V1 out-of-scope items (registration, multi-sport, AES import, SignalR).

**Bracket flow UI is shipped:** admin **`/brackets`**, public Brackets tab, and Division detail Step 2 preview all use **BracketTree** (match cards + SVG connectors + layout engine) instead of flat column/table layouts.

**Minor polish still possible** (not blocking V1 demo): bracket drag-reorder UI, full MudStepper on division wizard, server-side import preview endpoint, Terraform production hardening notes, optional bracket-tree extras (fit-to-width / zoom hint parity, resize-driven layout refresh on unusual viewports).

**ADR coverage gap:** Application seams (0004–0007), lifecycles (0003), styling (0002), and personal workspaces (0008) are recorded. Foundational domain pivots, platform choices, and ADR hygiene are tracked in [**Phase 10**](#phase-10--adr-coverage--doc-hygiene) below.

---

## Phase 0 — Foundations & model alignment

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | F-01 | **Pick canonical team/ranking model** | [`data-model.md` §4–5](data-model.md#4-team-import-and-event-entry) |
| [x] | F-02 | **Introduce Stage lifecycle in application layer** | [`data-model.md` §6](data-model.md#6-stages-the-round-container) |
| [x] | F-03 | **Waves API + tournament editor match start times** | [`happy-path.md` §2](happy-path.md) |
| [x] | F-04 | **Unify scheduling source of truth** | [`happy-path.md` §13](happy-path.md), ADR-0001 |
| [x] | F-05 | **Schedule version lifecycle** | [`data-model.md` §9](data-model.md#schedule-version-status-lifecycle) |
| [x] | F-06 | **Resolve Team vs TournamentTeam** — withdraw on Teams page; repairs use `TournamentTeamId` | [`repairs.md`](features/repairs.md) |

---

## Phase 1 — Setup path (happy path §1–6)

| Status | ID | Task | Doc / notes |
|--------|-----|------|-------------|
| [x] | S-01 | Tournaments CRUD + venues/courts inline; `CloneTournamentDialog` with structure-only | [`tournaments.md`](features/tournaments.md) |
| [x] | S-02 | Tournament editor: **Match start times** (AM/PM waves) | [`tournaments.md`](features/tournaments.md) |
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
| [x] | SCH-06 | Brackets admin page + **BracketTree**; manage-in-division link | [`brackets.md`](features/brackets.md) |

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

## Phase 10 — ADR coverage & doc hygiene

Gap analysis from the full ADR audit (2026-06-17). **HITL** = needs a short design pass or sign-off before writing; **AFK** = can draft from existing docs alone.

Work **10A → 10B → 10C** in order; **10D** is optional polish.

### 10A — ADR hygiene (quick wins)

| Status | ID | Type | Task | Doc / notes |
|--------|-----|------|------|-------------|
| [x] | ADR-H01 | AFK | **Promote ADR-0001 to accepted (partial)** — target locked 2026-06-17; migration phases 5a–5c in §10F | [`adr/0001-match-identity-vs-assignment.md`](adr/0001-match-identity-vs-assignment.md) |
| [ ] | ADR-H02 | AFK | **Index ADR-0008 everywhere** — add to [`docs/README.md`](README.md) ADR table, [`architecture.md`](architecture.md) deep-module links, [`architectural-prd.md`](architectural-prd.md) §4.1 traceability | [`adr/0008-personal-organizations-and-team-upgrade.md`](adr/0008-personal-organizations-and-team-upgrade.md) |
| [ ] | ADR-H03 | AFK | **Add `docs/adr/README.md`** — ADR index (0001–0008 + status), standard sections template, “when to write an ADR” one-liner, link to Phase 10 | [`docs/README.md`](README.md) |
| [ ] | ADR-H04 | AFK | **Reconcile schedule.md Phase table** with implementation reality — Phase 3 reflow vs [`implementation-backlog.md`](implementation-backlog.md) OPS-05 marked done; remove “ADR-0004-lite” wording if full ReflowPreview module ships | [`features/schedule.md`](features/schedule.md#implementation-status) |

### 10B — Domain ADRs (high priority)

| Status | ID | Type | Task | Blocked by | Doc / notes |
|--------|-----|------|------|------------|-------------|
| [ ] | ADR-D01 | AFK | **ADR-0009: Ranking as single seeding input** — `division_rankings` is the only seeding input; deprecate `tournament_teams.seed` / EntrySeed; import write paths | ADR-H03 | [`CONTEXT.md`](../CONTEXT.md), [`data-model.md`](data-model.md#5-ranking-and-seeding) |
| [ ] | ADR-D02 | AFK | **ADR-0010: Stage as round container** — `StageType` → one-or-more Pools or Brackets; Gold+Silver = one bracket Stage; `matches.stage_id` canonical | ADR-H03 | [`CONTEXT.md`](../CONTEXT.md), [`data-model.md`](data-model.md#6-stages-the-round-container) |
| [ ] | ADR-D03 | AFK | **ADR-0011: RepairJob operational model** — mid-tournament changes via `repair_jobs` + `repair_actions`; preview-before-apply; trigger taxonomy; no silent edits | ADR-H03, ADR-H01 | [`data-model.md`](data-model.md#11-repair-and-live-adjustment), ADR-0004 |
| [ ] | ADR-D04 | AFK | **ADR-0012: Match participant sources** — pool/bracket **Match** identity via `MatchParticipantSource`; cross-stage **AdvancementRule** vs within-bracket `match_winner` / `match_loser` | ADR-H03, ADR-D02 | [`data-model.md`](data-model.md#8-match-generation), ADR-0005 |
| [ ] | ADR-D05 | HITL | **ADR-0013: PoolSettings scope** — 1:1 with **Stage**; which pool config lives on Stage vs Division vs tournament; align with in-flight `PoolSettings` entity | ADR-H03, ADR-D02 | `PoolSettings.cs`, [`features/division-detail.md`](features/division-detail.md) |

### 10C — Platform ADRs (high priority)

| Status | ID | Type | Task | Blocked by | Doc / notes |
|--------|-----|------|------|------------|-------------|
| [ ] | ADR-P01 | AFK | **ADR-0014: Turso/libSQL + SQLite dual provider** — one EF model; single-writer semantics; when to revisit Postgres; link cost assumptions | ADR-H03 | [`azure-plan.md`](azure-plan.md), [`architectural-prd.md`](architectural-prd.md) §5.3 |
| [ ] | ADR-P02 | AFK | **ADR-0015: Domain events + outbox** — state-based (not event-sourced); same-transaction outbox; which events ship in V1; consumer expectations | ADR-H03 | [`data-model.md`](data-model.md#domain-events-and-integration-hooks), `OutboxPersistenceInterceptor` |
| [ ] | ADR-P03 | AFK | **ADR-0016: Multi-tenancy via EF global filter** — `org_id` claim + `ITenantContext`; interaction with ADR-0008 personal-workspace claim overwrite | ADR-H03, ADR-H02 | [`architecture.md`](architecture.md), ADR-0008 |
| [ ] | ADR-P04 | AFK | **ADR-0017: Clerk auth topology** — Clerk JWT on API; Blazor Web cookie → JWT exchange; `RequireOrg` / `RequireAdmin`; legacy Identity `410` | ADR-H03, ADR-P03 | [`architectural-prd.md`](architectural-prd.md) §5.4, [`features/auth.md`](features/auth.md) |
| [ ] | ADR-P05 | AFK | **ADR-0018: Full-schedule SQL conflict checks** — constraints checked against full DB schedule, not in-memory / viewport-scoped | ADR-H03, ADR-H01 | [`architecture.md`](architecture.md#conflict--scheduling-correctness) |
| [ ] | ADR-P06 | AFK | **ADR-0019: ScheduleVersion fork-on-publish** — global mutation rule for move, reflow, repairs, pool court swap when loaded version is `published` | ADR-H03, ADR-H01 | ADR-0004, [`data-model.md`](data-model.md#schedule-version-status-lifecycle) |

### 10D — Optional ADRs (medium priority)

| Status | ID | Type | Task | Doc / notes |
|--------|-----|------|------|-------------|
| [ ] | ADR-O01 | AFK | **ADR-0020: Tournament vs Event naming boundary** — admin **Tournament** vs public **Event**; route/DTO conventions | [`CONTEXT.md`](../CONTEXT.md) |
| [ ] | ADR-O02 | AFK | **ADR-0021: Pull-only freshness (no SignalR V1)** — explicit deferral and revisit triggers | [`architectural-prd.md`](architectural-prd.md) §9 |
| [ ] | ADR-O03 | AFK | **ADR-0022: Tournament templates / structure-only clone** — what templates capture; instantiation boundaries | [`data-model.md`](data-model.md#12-tournament-templates) |
| [ ] | ADR-O04 | AFK | **Split ADR-0006** (optional) — separate read-model ADRs if PublicEventReadiness or TiebreakerChain grow beyond one page | ADR-0006 |

### 10F — ADR-0001 migration (Match vs MatchAssignment)

| Status | ID | Phase | Task | Blocked by |
|--------|-----|-------|------|------------|
| [ ] | ADR-M01 | **5a** | Stop dual-write on move, reflow, generate, repairs — **`match_assignments` only** | ADR-H01 |
| [ ] | ADR-M02 | **5b** | Read rules by surface; add `current_match_assignment_id` + refresh on publish; remove match-column fallbacks | ADR-M01 |
| [ ] | ADR-M03 | **5c** | Drop `matches.court_id`, `scheduled_start_utc`, `duration_minutes`; backfill pointer migration | ADR-M02 |

### 10G — ADR-0004 implementation (ScheduleMutationPolicy)

| Status | ID | Phase | Task | Blocked by |
|--------|-----|-------|------|------------|
| [ ] | ADR-R01 | **4a** | Introduce **`ScheduleMutationPolicy`** + single apply-token issuer; route move/reflow Apply through it | ADR-0004 doc |
| [ ] | ADR-R02 | **4b** | Shared **`ScheduleMutationPreviewDto`**; align move + reflow preview responses | ADR-R01 |
| [ ] | ADR-R03 | **4c** | Preview runners + policy for multi-match repairs (outage, withdrawal, pool court swap) | ADR-R02 |

### 10H — ADR-0008 implementation (invite-only auth)

| Status | ID | Phase | Task | Blocked by |
|--------|-----|-------|------|------------|
| [ ] | AUTH-01 | **8a** | **`/request-access`** page + public API + `access_requests`; **`/login`** Sign in + Request access buttons; Clerk sign-up disabled in dashboard | ADR-0008 doc |
| [ ] | AUTH-02 | **8b** | **Admin:** access-request queue + **Provision org** (CourtFlow org + Clerk org + owner invite) | AUTH-01 |
| [ ] | AUTH-03 | **8c** | **Production:** remove personal auto-provision from `ClerkClaimsTransformation`; membership-only resolution; dev exception via `Development` / `Dev:AllowPersonalWorkspace` | AUTH-02 |
| [ ] | AUTH-04 | **8d** | **In-app Team invites** for org Admins + Clerk membership webhooks → `organization_memberships` | AUTH-03 |

### 10I — ADR-0003 implementation (lifecycle hardening)

| Status | ID | Task | Blocked by |
|--------|-----|------|------------|
| [ ] | LIF-01 | **Match:** `MatchStatus` + `TransitionTo` + `MatchStarted` / `MatchResultReported` events | — |
| [ ] | LIF-02 | **Stage:** intent methods (`MarkSeeded`, …); **StageMaterialization** calls those | — |
| [ ] | LIF-03 | **RepairJob:** apply-only row creation; drop **`Previewing`**; guarded transitions | ADR-R01 |
| [x] | LIF-04 | **`data-model.md`:** division checklist = **DivisionReadinessProjection** (not persisted) | ADR-0003 doc |
| [ ] | LIF-05 | Table-driven lifecycle tests per aggregate + readiness invariants | LIF-01–03 |

### 10E — Product follow-ups referenced by ADRs

| Status | ID | Type | Task | Doc / notes |
|--------|-----|------|------|-------------|
| [~] | ADR-E01 | — | ~~Convert to team workspace~~ → superseded by **AUTH-01–04** (invite-only provision) | ADR-0008 |
| [ ] | ADR-E02 | AFK | **OPS-13 Print outputs** (existing deferral) | [`features/print-outputs.md`](features/print-outputs.md) |

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
