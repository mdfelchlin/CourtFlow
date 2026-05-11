# Status lifecycles live on each aggregate root

CourtFlow encodes **Status** and **Lifecycle** (legal transitions, guards, side effects) **inside each aggregate root** in `CourtFlow.Domain` — not in controllers, not in MediatR handlers as the primary source of truth, and not behind a shared cross-cutting "lifecycle micro-framework" in V1.

Relevant aggregates each carry their own lifecycle: **Tournament** (`draft` → `published` → `archived`), **Division** (coarse checklist), **Stage** (`draft` → `configured` → `seeded` → `in_progress` → `completed`), **ScheduleVersion** (`draft` → `preview` → `published` → `superseded`), **Match**, **TournamentTeam**, **RepairJob**, etc. The tables live in [`../data-model.md`](../data-model.md#status-lifecycle-definitions).

## Status

accepted — aligns [`../architecture.md`](../architecture.md) DDD conventions with implementation guidance for the V1 fresh-start.

## Considered alternatives

- **Shared `StatusTransitionGuard` service** used by all aggregates. Rejected for V1: lifecycles differ enough (fork-on-publish, repair triggers, rollback rules) that a generic guard risks either (a) becoming a second schema expressed in code, or (b) leaking aggregate-specific rules back into the guard. Revisit only if the same transition pattern is copy-pasted identically across **three or more** aggregates without variation.
- **Lifecycle only in Application handlers.** Rejected: violates DDD — illegal transitions would compile; aggregates would be anemic bags of fields.

## Consequences

- Each aggregate exposes explicit transition methods (`Publish`, `Seed`, `TransitionStage`, …) that throw on illegal moves and emit **Domain events** per [`../data-model.md`](../data-model.md#domain-events-and-integration-hooks).
- MediatR handlers load the aggregate, call one transition method, persist — they do not embed transition tables.
- Tests: table-driven tests **per aggregate** for allowed and forbidden transitions.
- **DivisionReadinessProjection** (see [`0006-read-models-tiebreaker-delay-public-division-readiness.md`](0006-read-models-tiebreaker-delay-public-division-readiness.md)) composes coarse **Division** **Status** with fine **Stage status** for *read* gating without splitting lifecycle ownership across two write models.
