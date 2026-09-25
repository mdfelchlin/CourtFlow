# Status lifecycles live on each aggregate root

CourtFlow encodes **Status** and **Lifecycle** (legal transitions, guards, side effects) **inside each aggregate root** in `CourtFlow.Domain` — not in controllers, not in MediatR handlers as the primary source of truth, and not behind a shared cross-cutting "lifecycle micro-framework" in V1.

Illegal transitions throw **`LifecycleViolationException`** (mapped by the API to client-visible conflict errors). MediatR handlers load the aggregate, call one transition or intent method, persist — they do not embed transition tables.

Aggregate lifecycles: **Tournament**, **Stage**, **ScheduleVersion**, **Match**, **TournamentTeam**, **RepairJob**, **ImportJob**, etc. Tables in [`../data-model.md`](../data-model.md#status-lifecycle-definitions).

**Intentional exception — Division:** coarse division checklist status is **not** a write model on the **Division** aggregate. It is **derived** by **DivisionReadinessProjection** from **Stage** status + team counts (ADR-0006). Do not add a persisted `division_status` column that can drift from stages.

## Status

**Accepted (partial)** — pattern locked (2026-06-17 review). **Tournament**, **Stage**, **ScheduleVersion**, **TournamentTeam** largely comply; **Match**, **Stage** intent methods, **RepairJob**, and **`LifecycleViolationException`** adoption are backlog work.

## Decisions (2026-06-17)

1. **Division coarse status:** **Derived only** via **DivisionReadinessProjection** — no `DivisionStatus` on the **Division** aggregate in V1. Stage lifecycles are authoritative for write gating.
2. **Match:** Full lifecycle — rename **`MatchState` → `MatchStatus`**, central **`TransitionTo`**, raise **`MatchStarted`** and **`MatchResultReported`** from the aggregate (including first-score auto-start path).
3. **Stage:** Expose **intent methods** (`MarkConfigured()`, `MarkSeeded()`, `Begin()`, `Complete()`, …) wrapping transitions + events; **StageMaterialization** and handlers call these — not raw `TransitionTo` from Infrastructure.
4. **RepairJob:** Row created on **Apply** only; preview stays non-persistent (ADR-0004). Drop **`Previewing`** as a meaningful status; **`Applied`** / **`Rejected`** are terminal with guarded transitions.
5. **Exceptions:** Use **`LifecycleViolationException`** for all illegal lifecycle transitions (replace ad-hoc **`InvalidOperationException`** on aggregates over time).

## Aggregate compliance (snapshot)

| Aggregate | Write-side lifecycle | Notes |
| --- | --- | --- |
| **Tournament** | Yes | `Publish`, `Unpublish`, `Archive`, events |
| **Stage** | Partial | `TransitionTo` exists; needs intent methods (LIF-02) |
| **ScheduleVersion** | Yes | `TransitionTo`, `SchedulePublished` event |
| **Match** | Partial | Methods exist; needs `TransitionTo` + events (LIF-01) |
| **TournamentTeam** | Yes | `Withdraw`, `Restore`, event |
| **RepairJob** | Partial | Needs apply-only model (LIF-03) |
| **Division** | N/A (derived) | **DivisionReadinessProjection** only |
| **ImportJob** | Light | `MarkCompleted` / `MarkFailed` |

## Considered alternatives

- **Shared `StatusTransitionGuard` service.** Rejected for V1 — lifecycles differ too much; revisit only if identical transition patterns copy-paste across **three or more** aggregates.
- **Lifecycle only in Application handlers.** Rejected — anemic domain model.
- **Persisted `division_status` on Division.** Rejected — duplicates **Stage** truth; use projection (Decision 1).
- **Persist `Previewing` on RepairJob.** Rejected — conflicts with non-persistent preview (ADR-0004).

## Consequences

- Table-driven lifecycle tests **per aggregate** (LIF-05); Division covered by **DivisionReadinessProjection** invariant tests (ADR-0006).
- [`../data-model.md`](../data-model.md) `division_status` described as **projected**, not a stored write column in V1.
- Materialization, scheduling, and repair commands call aggregate intent methods after loading roots.

## Implementation phases

| Phase | Scope | Backlog |
| --- | --- | --- |
| **LIF-01** | **Match** `MatchStatus` + `TransitionTo` + domain events | |
| **LIF-02** | **Stage** intent methods; update **StageMaterialization** | |
| **LIF-03** | **RepairJob** apply-only creation; remove **`Previewing`** usage | ADR-0004 |
| **LIF-04** | **`data-model.md`**: division checklist = projection | |
| **LIF-05** | Table-driven lifecycle tests per aggregate | LIF-01–03 |
