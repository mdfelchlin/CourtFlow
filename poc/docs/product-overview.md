# CourtFlow — Product Overview

This is the **product** doc. Read this first to understand why CourtFlow exists, who it's for, and what V1 includes. For everything else:

- **How do all the features fit together end-to-end?** See [`happy-path.md`](happy-path.md) — the CSV-to-built-tournament walkthrough.
- **Why these aggregates and tables?** See [`data-model.md`](data-model.md).
- **How is it built and deployed?** See [`architectural-prd.md`](architectural-prd.md), [`architecture.md`](architecture.md), [`azure-plan.md`](azure-plan.md).
- **What does each screen look like?** See [`features/`](features/) and [`ui/`](ui/).

---

## 1. Product intent

### 1.1 Mission

CourtFlow gives volleyball tournament directors **fast, safe, self-service control** over schedules, brackets, and courts. The directors should never have to email a vendor to fix a real-world problem.

### 1.2 Problems we are solving

Existing tools (AES, SportsWrench, and the like) are rigid in exactly the moments tournament directors most need flexibility:

- **Last-minute change is painful.** Moving a match to a different court, delaying a court by one slot, or fixing a bracket pairing requires vendor intervention or destructive edits with no audit trail.
- **Conflicts are silent.** Team / ref / court collisions are missed because checks run against whatever happens to be loaded in the UI rather than the full schedule in the database.
- **Operational reality isn't modeled.** Withdrawals, court outages, ref reassignments, and schedule reflow happen as off-record edits that corrupt history.
- **Visibility into delay is poor.** Directors can't tell at a glance which courts are running behind and by how much; they walk the gym to find out.
- **Long-lived team and club identity collides with event-specific entries.** Schedulers reach for the global team id when they should reach for the per-tournament entry, which makes repairs even more dangerous.

### 1.3 What we are building (one sentence)

A multi-tenant API and Blazor admin web app — designed so the same UI components can later host inside a public site or a MAUI Blazor Hybrid app — that turns those problems into **first-class, audited, repairable** workflows.

### 1.4 How CourtFlow is different

- **Repairs are first-class.** Withdrawals, outages, and reassignments are recorded as `RepairJob`s, not silent edits. See [`data-model.md`](data-model.md#11-repair-and-live-adjustment).
- **Conflict checks run against the full schedule** (SQL queries, not the loaded view). See [`architecture.md`](architecture.md#conflict--scheduling-correctness).
- **Schedules are versioned.** Generation, preview, and publish are explicit; history isn't destroyed by reflows. See [`data-model.md`](data-model.md#9-scheduling-and-assignment).
- **Court status is at-a-glance.** A planned dashboard surfaces every court's "behind/ahead" delta — readable as cards on the floor, as a color-coded court × time **grid** at the scorer's table, and in **TV mode** on a wall monitor. Directors intervene before a delay cascades. See [`features/court-status.md`](features/court-status.md).
- **Reuse-friendly UI.** All screens live in a host-agnostic Razor Class Library (`CourtFlow.Web.Shared`) so the future MAUI client and public web views drop in without rewrites.

### 1.5 Primary audiences

- **Authenticated admins (tournament directors / organizers)** — the only writers in V1. Use the full management UI under the admin shell.
- **Unauthenticated public visitors (coaches, players, spectators)** — read-only. Limited to the landing page and per-event detail (Teams / Schedule / Brackets / Standings tabs). The organizer controls what's visible via `tournaments.status` (master gate) + per-tab readiness (`ScheduleVersion` published, bracket slots materialized, etc.) — see [`features/public-event.md`](features/public-event.md) and [`data-model.md` §Tournament status lifecycle](data-model.md#tournament-status-lifecycle).

---

## 2. Personas

### 2.1 Admin (Tournament Director / Organizer)

- Sets up tournaments, divisions, pools, and brackets.
- Imports teams from CSV, ranks them, and assigns waves.
- Generates schedules and edits them on the fly when reality changes.
- Monitors live progress across all courts and identifies which courts are behind schedule.
- Needs full visibility into conflicts (waves, refs, court reuse) and fast tools to fix them.

### 2.2 Public visitor (V1: read-only)

- Browses current and upcoming events on the landing page.
- Drills into an event to see the published Schedule, Brackets, and Standings.
- Has no account and cannot mutate anything.

---

## 3. Scope

### 3.1 In scope (V1)

- Multi-tenant organizations and tournaments.
- Venues and courts (per-tournament linkage).
- Divisions, pools, and waves (AM/PM as a first-class scheduling constraint).
- Team CSV import (predefined-pools and ranked-teams formats).
- Schedule generation, conflict checking, and per-match move/delay.
- Bracket generation (single + double elimination, optional if-necessary final).
- Match scoring (set-level), standings, bracket progression.
- Authentication via Clerk (admin-only writes; public reads are anonymous).
- Public landing + per-event read-only views.

### 3.2 Out of scope (V1)

- Team registration and payments.
- External roster / league integrations.
- Per-court billing, marketplace, payouts.
- Multi-sport support beyond volleyball.
- Full AES/SportsWrench data import or two-way integration.
- Realtime push (SignalR/WebSockets) — V1 is refresh-based, see [`architectural-prd.md`](architectural-prd.md) §2.

### 3.3 Planned but not yet shipped

- **Print outputs** for pool sheets, score sheets, bracket sheets, daily court runs, work rotations, and standings pages — modeled in [`features/print-outputs.md`](features/print-outputs.md).
- **Capture template / create from template** — persist and instantiate `template_*` graphs — modeled in [`data-model.md` §12](data-model.md#12-tournament-templates).
- Repair-job UI polish on top of the data-layer repair model (list/detail UX beyond basic `/repairs`).
- Realtime push (refresh-only in V1).

**Recently shipped** (moved out of this list): Court Status Cards + Grid + TV mode, Match clock, Reflow preview + apply, Work team auto-rotation + division review panel, Schedule version preview/publish UI, My-team filter on public event detail, Clone tournament (full + structure-only), Public Teams tab + past events + per-tab readiness gates, Auto-archive job.

---

## 4. Functional surface (per feature)

Each user-visible feature has its own UI-focused PRD. Start there for screen-level detail.

| Area | Feature PRD |
|------|-------------|
| Sign-in / sign-out / authorization redirects | [`features/auth.md`](features/auth.md) |
| Public events landing | [`features/public-landing.md`](features/public-landing.md) |
| Public event detail (Teams / Schedule / Brackets / Standings tabs + my-team filter) | [`features/public-event.md`](features/public-event.md) |
| Tournaments list + tournament editor + clone (details, venues, courts, waves) | [`features/tournaments.md`](features/tournaments.md) |
| Divisions table | [`features/divisions.md`](features/divisions.md) |
| Division detail (stages, pools, brackets, rankings, work rotation) | [`features/division-detail.md`](features/division-detail.md) |
| Teams table + filters | [`features/teams.md`](features/teams.md) |
| CSV imports (predefined-pools / ranked-teams) | [`features/imports.md`](features/imports.md) |
| Schedule grid, conflict checking, per-match start/move/score | [`features/schedule.md`](features/schedule.md) |
| Brackets viewer + generation | [`features/brackets.md`](features/brackets.md) |
| Flat matches list + scoring + start | [`features/matches.md`](features/matches.md) |
| Standings per pool | [`features/standings.md`](features/standings.md) |
| Repair Job history + mid-tournament corrections | [`features/repairs.md`](features/repairs.md) |
| Court status & delay dashboard (Cards + Grid + TV mode, match clock, reflow preview) | [`features/court-status.md`](features/court-status.md) |
| Work team rotation (cross-cutting affordances) | [`features/work-teams.md`](features/work-teams.md) |
| Print outputs (pool / score / bracket / court-run sheets) | [`features/print-outputs.md`](features/print-outputs.md) |

---

## 5. Non-functional requirements

| NFR | Target |
|-----|--------|
| **Performance** | Core admin pages (tournament dashboard, schedule, bracket, court status) load in under 2 seconds for typical tournament sizes (up to ~150 teams and ~30 courts). |
| **Scalability** | Multiple concurrent tournaments per organization; dozens of organizations per deployment. |
| **Reliability** | 99.9% uptime target for API and web app, excluding planned maintenance. |
| **Security** | Per-organization data isolation enforced server-side; admin-only writes; HTTPS everywhere. |
| **Observability** | Centralized structured logging for schedule generation, bracket changes, conflict-check failures, delay calculations; basic request/error/slow-query metrics. |
| **Cost** | Scale-to-zero API + static UI hosting + low-baseline DB tier (Turso/libSQL). See [`azure-plan.md`](azure-plan.md). |

Architecture detail behind these targets: [`architectural-prd.md`](architectural-prd.md) §8.

---

## 6. Success metrics (V1)

- An admin sets up a typical tournament (80 teams, 12 courts, AM/PM waves) in under 60 minutes without vendor assistance, or in under 10 minutes when cloning a similar prior tournament.
- Common change workflows (move a match to a new court/time, adjust bracket pairing) complete in under 30 seconds from the admin UI.
- Tournament-day reflow workflows (Court 3 ran 20 min over, slide everything) complete in under 60 seconds end-to-end (open Court Status → Reflow preview → review diff → Apply).
- Zero known cases where the system silently allows a scheduling or ref conflict after using built-in tools (all conflicts surface as visible warnings or hard blocks).
- Admins report that the court status dashboard makes it easy to identify behind-schedule courts and intervene, compared to AES/SportsWrench.
- Coaches and parents using the public event page's **My-team filter** can answer "where am I playing next?" without scrolling or filtering by hand.

---

## 7. Future work

Tracked separately in the data and architectural PRDs, but at a product level:

- Public-facing schedule, pool, and bracket views beyond V1's read-only event tabs.
- Native scoring clients on MAUI (per-court scoring, offline-first behavior, device login for per-court billing).
- Team registration and payment flows.
- Deep integrations with external roster or league systems.
- Analytics dashboards (court utilization heatmaps, revenue analytics).
