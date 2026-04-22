# Volleyball Tournament Management API + Blazor Web App PRD (V1, Admin-Only)

## 1. Product Overview

**Working name**: CourtFlow (TBD)

CourtFlow is a web-based tournament management platform focused on volleyball events, designed to replace rigid tools like AES and SportsWrench by making schedule and bracket changes fast, safe, and self-service for tournament directors. V1 delivers a multi-tenant API and Blazor Web App (admin-only) with a host-agnostic Razor Class Library so a .NET MAUI Blazor Hybrid client and/or public web views can be added later without major rewrites.[^1][^2][^3][^4]

In this version, only authenticated admins use the application; coaches, players, and spectators are assumed to receive information through separate channels (e.g., printed sheets, exports, or future web/mobile clients).[^5]

## 2. Goals and Non-Goals

### 2.1 Goals (V1)

- Enable admins to configure events (divisions, pools, waves), generate schedules, and adjust brackets and courts without contacting a vendor.[^1]
- Make last-minute court/time changes and bracket edits safe and fast, automatically propagating impacts to subsequent matches and displays.
- Provide a modern web experience for admins with:
  - Full control over events, brackets, courts, schedules, and live operations.
  - An at-a-glance court status dashboard showing all courts, their current match state, and how far behind or ahead schedule they are based on expected versus actual score-entry times.
- Establish a clean domain model and API for tournaments, matches, waves, and constraints (e.g., ref conflicts, partner tournaments) that can power future public views and native clients.[^1]
- Architect all reusable UI in a Razor Class Library (RCL) so it can be reused by a future MAUI Blazor Hybrid app and future public web views.[^2][^3]

### 2.2 Non-Goals (V1)

- Public-facing views for coaches, players, or spectators (no public site in this version).
- Team registration and payments (no registration flows in this version).
- Deep integrations with external roster or league systems.
- Advanced billing models (per-court billing, marketplace, payouts) beyond basic organization-level subscriptions.
- Multi-sport support beyond volleyball.
- Full AES/SportsWrench data import or two-way integration.

## 3. Target Users and Personas

### 3.1 Admin (Tournament Director / Organizer)

- Sets up events, divisions, pools, and brackets.
- Manages schedules, court assignments, and last-minute changes.
- Monitors live progress across all courts and identifies which courts are behind schedule.
- Needs full visibility into conflicts (waves, refs, court reuse) and fast tools to fix them.[^1]

## 4. Product Scope (V1)

### 4.1 Core Functional Areas

- Organizations & tournaments (multi-tenant): organizations, tournaments, venues, courts.
- Scheduling & waves: AM/PM waves as first-class constraints; court/time slots; ref conflicts.[^1]
- Pools & brackets: pool play, seeding, and configurable bracket types with flexible editing.
- Court assignment & changes: fast re-mapping of matches to courts/times.
- Court status & delay dashboard: real-time view of all courts and how far they are behind or ahead.
- Results & standings: scores, match states, pool standings, tiebreakers, and bracket progression.
- Authentication & roles: single admin role; no public or anonymous access.

## 5. Detailed Functional Requirements

### 5.1 Organizations, Users, and Access Control

- Support multiple organizations (clubs, leagues, tournament operators) in a single deployment (multi-tenant), with each org’s data logically isolated.
- Each admin user belongs to one or more organizations.
- Roles in V1:
  - Admin: full management rights for the organizations and tournaments they belong to.
- There is no public or anonymous access to any UI in V1; all access is authenticated.
- Authentication uses ASP.NET Core Identity with basic email/password in V1.

### 5.2 Tournaments, Venues, and Courts

- A Tournament belongs to an Organization and has:
  - Name, description, start/end dates.
  - One or more Venues, each with a set of Courts.
  - Time zone and default match duration.
- Courts:
  - Each Court belongs to a Venue and has a name/identifier (e.g., "Court 1").
  - Courts can be grouped or tagged (e.g., "AM wave", "main gym") to improve scheduling UX.
- Partner tournaments (TournamentPartnerId) can be modeled to support constraints where some teams may play in connected events, preventing scheduling conflicts across tournaments.[^1]

### 5.3 Divisions, Pools, and Waves

- Divisions:
  - A Tournament has one or more Divisions (e.g., 18 Open, 17 Club).
  - Each Division has a max team count and competition format (pool only, pool + bracket, bracket only).
- Waves:
  - Waves (AM, PM) are first-class properties on teams and matches, not just labels.
  - Every team is assigned to exactly one Wave within a Division.
  - Scheduling must respect wave constraints, never placing an AM team into a PM time block and vice versa.
- Pools:
  - Each Division can have one or more Pools, with teams assigned.
  - The system generates pool-play matches using standard round-robin logic.
  - Pools can be manually adjusted (e.g., moving teams between pools before play starts).

### 5.4 Scheduling and Constraints

- Time slots:
  - The system models discrete time slots per Court and Wave (e.g., Court 1, AM wave, 8:00, 9:00, etc.).
  - Match duration is configurable per Tournament, with court buffers optional.
- Constraints engine:
  - Prevents a team from being scheduled in overlapping time slots.
  - Enforces Wave constraints.
  - Enforces referee conflict rules (e.g., a team cannot play and ref at the same time).
- Ref assignment:
  - For each match, a "ref team" may be assigned according to division rules.
  - Conflict checks run at the database level across all scheduled matches for the event (not just those currently loaded in the client view).[^1]

### 5.5 Brackets

- Bracket types supported in V1:
  - Single-elimination brackets.
  - Double-elimination brackets with optional “if-necessary” grand final match.[^1]
- Bracket configuration:
  - Number of teams, seeding source (pool placements, manual seeding).
  - Option to enable/disable reset/if-necessary final.
- Bracket generation:
  - System generates initial bracket match tree, including winners/losers paths for double-elimination.
  - Bracket structure is stored as a data model referencing matches and advancement rules.
- Bracket editing:
  - Admin can drag-and-drop teams into different slots before matches start.
  - Admin can re-assign winners/losers mapping if needed (within safety checks).
  - Admin can insert or cancel matches (e.g., consolation or crossover matches), with warnings when this breaks standard progression.

### 5.6 Court and Time Changes

- Court reassignment:
  - Admin can move a match to a different Court and/or time slot via a simple UI workflow.
  - System automatically:
    - Updates any dependent bracket matches.
    - Refreshes admin views.
    - Runs conflict checks (team, ref, court double-booking).
- Bulk adjustments:
  - V1 supports basic bulk operations (e.g., delay all matches on a Court by one time slot) to handle real-world delays.
  - System re-validates constraints after bulk operations and highlights conflicts requiring manual resolution.

### 5.7 Court Status & Delay Dashboard

- Admin home/dashboard view:
  - Displays a card or row for every Court in the tournament.
  - For each Court, shows:
    - Current match (teams, division, match ID).
    - Scheduled start time for the current match.
    - Actual start time (if tracked) and/or last score update time.
    - Computed "minutes behind/ahead" based on current time versus scheduled start or expected finish.
    - Status indicator (e.g., On Time, Slightly Behind, Behind, Idle/Empty).
  - Supports filtering by venue, division, and wave.
- Detail view:
  - Clicking a court shows its upcoming schedule and historical matches with timestamps and delays.
- Data model considerations:
  - Matches store timestamps for state transitions (Created, In Progress, Final) and last score update.
  - Delay calculations are derived from schedule versus actual timestamps.

### 5.8 Match Results and Standings

- Match states: Not Started, In Progress, Final, Canceled.
- Score reporting:
  - Scores entered via the Blazor Web admin UI.
  - Matches track set scores and winner/loser.
- Standings (pool play):
  - Standings support configurable tiebreakers (e.g., match record, set percentage, point percentage, head-to-head) and produce a ranked list used for bracket seeding.[^1]
- Bracket progression:
  - Winning/losing a match automatically advances teams through the bracket.
  - For double-elimination, when the losers-bracket champion wins the first final, an if-necessary match is created if configured; otherwise the winners-bracket champion is declared overall winner.[^1]

### 5.9 Admin Web Application (Blazor)

- All admin views are built as host-agnostic Razor components in a shared RCL, designed for reuse in a future MAUI Blazor Hybrid app and in any future public web views.[^3][^4][^2]
- Admin shell:
  - Navigation for Organizations, Tournaments, Venues, Courts, Divisions, Pools, Brackets, Schedules, and the Court Status dashboard.
  - Authenticated-only access; no anonymous routes.
- Key admin flows:
  - Create/edit tournament, venues, courts.
  - Create/edit divisions, pools, waves.
  - Generate and adjust schedules.
  - Manage brackets and match assignments.
  - Monitor courts and delays via the dashboard.
  - Enter scores and finalize events.

## 6. API Requirements

### 6.1 API Principles

- RESTful JSON API with resource-oriented endpoints (e.g., `/api/tournaments/{id}/divisions`).
- Authenticated using bearer tokens (JWT) backed by ASP.NET Core Identity.
- Multi-tenant: every request is scoped to an Organization via claims and routing or headers.
- Versioned (e.g., `/api/v1/...`) to allow non-breaking evolution.

### 6.2 Core Domain Resources (Indicative)

- Organizations: CRUD for orgs (admin only), org settings.
- Users: endpoints for managing admin memberships for organizations.
- Tournaments: CRUD, including dates, venues, top-level settings.
- Venues & Courts: CRUD, including court metadata and status.
- Divisions & Waves: CRUD, wave assignment, division configuration.
- Teams & Pools: CRUD, team assignment to pools and divisions.
- Matches & Brackets:
  - Create/list/update matches.
  - Set scores and state transitions.
  - Bracket configuration and generation endpoints.
- Scheduling & Constraints:
  - Endpoints for generating schedule drafts.
  - Endpoints to run constraint checks and return conflict reports.
- Court Status:
  - Endpoints that return per-court status, including last score timestamp, current match info, and computed delay metrics.

### 6.3 Real-Time and Notifications (Optional V1)

- Use SignalR hubs to push live updates (match state changes, court changes) to the Blazor Web admin app and future clients.
- Topics:
  - Match updates by tournament/division/court.
  - Court schedule and delay updates.

## 7. Technical Architecture

### 7.1 Backend

- ASP.NET Core Web API.
- Entity Framework Core for data access.
- Relational database (Turso/libSQL with SQLite-compatible EF model).
- ASP.NET Core Identity for auth and roles.

### 7.2 Web Front End

- Blazor Web App (Server or WebAssembly) for admin UI.
- Razor Class Library containing reusable components, services, and shared models.
- Responsive design to ensure admin views work on desktop and mobile browsers.

### 7.3 Future Public and MAUI Integration

- Future public web views will reuse components from the shared RCL and consume the same API.
- A future MAUI Blazor Hybrid app will reference the shared RCL and reuse components where appropriate.[^2][^3]
- Platform-specific services (offline storage, local notifications, device integrations) will be abstracted behind interfaces in the shared library, with web implementations in V1 and MAUI implementations added later.[^3][^2]

## 8. Non-Functional Requirements

- Performance:
  - Load core pages (tournament dashboard, schedule, bracket, court status) in under 2 seconds for typical tournament sizes (e.g., up to 150 teams and 30 courts).
- Scalability:
  - Support multiple concurrent tournaments per organization and dozens of organizations overall.
- Reliability:
  - Target 99.9% uptime for the API and web app, excluding planned maintenance.
- Security:
  - Enforce per-organization data isolation and admin-only write access.
  - Use HTTPS everywhere and secure cookie/session practices.
- Observability:
  - Centralized logging for key events (schedule generation, bracket changes, conflict check failures, delay calculations).
  - Basic metrics (requests per second, error rates, slow queries).

## 9. Out of Scope / Future Work

- Public-facing schedule, pool, and bracket views.
- Native scoring clients in MAUI (per-court scoring, offline-first behavior, device login for per-court billing).
- Team registration and payment flows.
- Deep integrations with external roster or league systems.
- Robust analytics dashboards (heatmaps of court utilization, revenue analytics).

## 10. Success Metrics (V1)

- Time for an admin to set up a typical tournament (e.g., 80 teams, 12 courts, AM/PM waves) is under 60 minutes without vendor assistance.
- Common change workflows (moving a match to a new court/time, adjusting bracket pairing) can be completed in under 30 seconds from the admin UI.
- Zero known cases where the system silently allows a scheduling or ref conflict after using built-in tools (all conflicts result in visible warnings or hard blocks).
- Admins report that the court status dashboard makes it easy to identify behind-schedule courts and intervene, compared to AES/SportsWrench.

---

## References

1. [Here are the four drop-in PRD sections:

5.2.4a — AM/PM Waves (new section, insert after scheduling intro)
Waves are a first-class scheduling concept, not just a label. Every team in an event is assigned to exactly one wave — either AM or PM — and th...

...t check trivial.
And the ref conflict check needs to run as a database query against scheduled match windows, not in-memory — otherwise it will silently miss conflicts when the schedule has more matches than are currently loaded in the client's view.](https://www.perplexity.ai/search/4f29c62b-17fc-4526-a629-4dd9986fc9ac) - These are good additions. They close several of the remaining product-model gaps, especially around ...

2. [Combine Blazor Hybrid with .NET MAUI and Razor class libraries ...](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/class-libraries-best-practices?view=aspnetcore-10.0) - This guide demonstrates the recommended pattern for creating Razor class libraries (RCLs) that combi...

3. [Reuse Razor components in ASP.NET Core Blazor Hybrid apps](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/reuse-razor-components?view=aspnetcore-10.0) - This article explains how to author and organize Razor components for the web and Web Views in Blazo...

4. [Share assets across web and native clients using a Razor class ...](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/class-libraries?view=aspnetcore-10.0) - Learn how to share Razor components, C# code, and static assets across web and native clients using ...

5. [help me put together a prd that I can provide to cursor for this application. I want the full replacement, API, admin, website and such](https://www.perplexity.ai/search/dd0ebabf-7209-4a33-b1e3-e3a87084a4ee) - Below is a concise PRD you can drop into Cursor and iterate on. It’s written for a combined PM/engin...

