# Architecture Notes

CourtFlow V1 follows a layered solution with strong domain boundaries:

- `Domain` - pure entities, value objects, enums, domain events. No framework dependencies.
- `Application` - use-case orchestration via MediatR command/query handlers, DTOs, FluentValidation rules, and abstractions (e.g. `IAppDbContext`, `ITenantContext`, `IClock`, scheduling/bracket/standings interfaces).
- `Infrastructure` - EF Core DbContext, migrations, Clerk/JWT-adjacent helpers (no ASP.NET Identity in the target stack), multi-tenancy, and concrete engines (`IScheduleGenerator`, `IConstraintChecker`, `IBracketBuilder`, `IBracketProgressionService`, `IStandingsCalculator`, `ICourtStatusService`).
- `Api` - ASP.NET Core host, JWT bearer auth, OpenAPI, controllers wiring MediatR.
- `ApiClient` - typed HTTP client (generated via NSwag) used by Blazor.
- `Web.Shared` - RCL with all reusable Razor components, layouts, and view models. Host-agnostic so a future MAUI Blazor Hybrid app can reuse it.
- `Web` - Blazor Web App (Server interactive) host; cookie auth; exchanges the user's login for a JWT and forwards it to the API.

## Patterns

The codebase consistently follows these patterns. New work should respect all of them.

### Clean Architecture

Dependencies point inward: `Api`/`Web` depend on `Application`; `Application` depends on `Domain`; `Infrastructure` implements abstractions defined in `Application` and is wired at composition time. This keeps tournament rules testable and swap-friendly.

### Domain-Driven Design (DDD)

The schema and domain layer are organized around aggregates with explicit invariants and transactional boundaries (see [`data-model.md`](data-model.md) for the canonical aggregate list). Conventions:

- **Aggregate roots** carry an optimistic concurrency token. Examples: `Organization`, `Tournament`, `Division`, `Stage`, `TournamentTeam`, `Pool`, `Bracket`, `Match`, `ScheduleVersion`, `RepairJob`, `Venue`, `ImportJob`.
- **Cross-aggregate references** are id-only; no navigation across aggregate boundaries inside a single transaction.
- **Value objects** for things without identity (e.g. score lines, address blocks, time-of-day slot windows).
- **Domain events** raised from aggregates and dispatched after persistence (e.g. `SchedulePublished`, `MatchResultReported`, `TournamentTeamWithdrawn` — full list in [`data-model.md`](data-model.md#domain-events-and-integration-hooks)).
- **Repositories** are abstracted via `IAppDbContext` rather than per-entity repository interfaces.
- **Status lifecycles** are first-class and live in `Domain`, not in controllers — see [`adr/0003-status-lifecycle-per-aggregate.md`](adr/0003-status-lifecycle-per-aggregate.md) for the enforcement pattern (per-aggregate roots; no shared lifecycle micro-framework until duplication proves it).

### CQRS via MediatR

Each use case is either a command (mutating) or a query (read), implemented as a MediatR handler in `Application/Features/...`. Controllers are thin and delegate to MediatR. See [`architectural-prd.md`](architectural-prd.md) §5.2 for the full rationale.

### Multi-tenancy via tenant filter

Every aggregate root carries `OrganizationId`. An `ITenantContext` is resolved per-request from the JWT `org_id` claim. EF Core global query filters apply the tenant filter everywhere so cross-tenant reads are impossible by default.

## Conflict & scheduling correctness

Ref/team/court conflict checks run as SQL queries against the full schedule (not in-memory), so they cannot miss conflicts outside the currently loaded view (see [`product-overview.md`](product-overview.md) scheduling requirements).

## Deep modules (named Application seams)

These are **not** new aggregates — they are **Application-layer modules** (or read-model projections) with a **small interface** and **high leverage**: many UI entry points and MediatR handlers cross the same seam. Implement each as one cohesive unit (one folder + one primary handler or service + shared DTOs) so tests and docs stay aligned. Full rationale and ADR links: [`adr/0004-reflow-preview-contract.md`](adr/0004-reflow-preview-contract.md), [`adr/0005-stage-materialization-and-bracket-resolution.md`](adr/0005-stage-materialization-and-bracket-resolution.md), [`adr/0006-read-models-tiebreaker-delay-public-division-readiness.md`](adr/0006-read-models-tiebreaker-delay-public-division-readiness.md).

| Module | Owns | Primary consumers |
| --- | --- | --- |
| **ReflowPreview** | Dry-run cascade against a loaded **ScheduleVersion**; returns diff + diagnostics + `apply_token`; **Apply** forks when the version is `published` and opens the correct **RepairTrigger**. | Court Status drawer, Schedule Move, repair dialogs (`CourtOutage`, `WithdrawTeam`, `PoolCourtSwap`, etc.). |
| **StageMaterialization** | Idempotent **Stage**-gated operations: **Snake Rankings into Pools**, **Resolve Bracket Slots**, **Generate brackets** — shared rules for re-run, overwrite warnings, and which **RepairJob** vs in-wizard path applies. | Division detail wizard, `happy-path.md` materialize steps. |
| **BracketResolution** *(inside StageMaterialization family)* | Precedence: **BracketSlotOverride** → **AdvancementRule** → null; feeds **BracketSeed** and bracket **Match** participant sources. | Same as materialization + bracket viewer. |
| **TiebreakerChain** | Pure ordering from **Match** results + **TiebreakerPreset** (ratio chains, head-to-head subset, forfeit, stable **Ranking** fallback). | Standings, `order_by = tiebreaker_rank` on **AdvancementRule**, Division dialog chain preview. |
| **MatchClockDelaySignal** | Canonical per-**Match** delay from **Match clock** + **MatchAssignment**; band thresholds; "worst positive delta" aggregation for court chips. | Court Status (Cards + Grid + TV), Schedule cell chip, public my-team strip. |
| **PublicEventReadiness** | Composed tab visibility from **Tournament** + **ScheduleVersion** + bracket materialization + scoring state. | Public event page tabs. |
| **DivisionReadinessProjection** | How coarse **Division** checklist **Status** relates to fine **Stage status** so gating never contradicts itself. | Division detail blocking strips, validators. |

Glossary entries for these names live in [`../CONTEXT.md`](../CONTEXT.md#named-application-modules-read-models).

## Engine interfaces (seams policy)

`Application` declares engine **interfaces** (`IScheduleGenerator`, `IConstraintChecker`, `IBracketBuilder`, …). Per [`adr/0007-engine-seams-and-testing.md`](adr/0007-engine-seams-and-testing.md):

- **Default:** ship **one production adapter** per interface; callers depend on the interface for testability.
- **Tests:** primary coverage is **integration tests** against the real adapter + in-memory or SQLite DB; **contract / fake adapters** are added only when a second implementation is planned or when fakes materially shrink test runtime without hiding integration bugs.
- **Avoid hypothetical seams:** do not introduce an `I*` whose only implementation is the concrete class next to it with no test fake and no alternate provider — collapse to a concrete type until a second adapter exists.

## Naming

Tables and columns use snake_case via `EFCore.NamingConventions` (e.g. `tournaments.scheduled_start_utc`). Tables are grouped by aggregate; child tables of an aggregate share the root's prefix where it improves discoverability (`match_sets`, `match_assignments`, `bracket_seed_slots`, etc.).
