# Match identity is stable; MatchAssignment is versioned and replaceable

A volleyball **Match** (the participants, the **Stage** it belongs to, the sets played, the winner) is modeled separately from its **MatchAssignment** (the court, scheduled start, duration). Assignments are owned by a **ScheduleVersion**; reassigning a match — including the schedule grid's "Move" action and any **RepairJob**-driven reflow — creates or amends a `match_assignments` row inside the current version rather than mutating the `matches` row. This buys clean audit history, safe schedule regeneration, and non-destructive repair operations (court outage, team withdrawal, referee reassignment), at the cost of two-table reads/writes for every "where and when" question and a thin ScheduleVersion lifecycle that did not exist in earlier prototypes.

## Status

proposed — target model per [`../data-model.md`](../data-model.md#9-scheduling-and-assignment). In production code today, grid **Move** uses **`POST …/schedule-versions/preview-move`** + **`POST …/apply-move`** (`match_assignments` + `MutationSequence`); broader **draft → published** versioning and granular assignment endpoints are tracked in [`../features/schedule.md`](../features/schedule.md#gap-from-target-implementation).
## Considered alternatives

- **Mutate `matches` directly on move/reschedule.** Simpler reads, no joins, fits the V1 shim shape. Rejected because every reflow becomes a destructive rewrite — repair history, "undo this regeneration", and conflict diagnostics across schedule revisions all get harder or impossible.
- **Event-source the schedule.** Strong audit story but the rest of the system is state-based per [`../architectural-prd.md`](../architectural-prd.md); introducing event sourcing here alone would split the persistence model. Deferred.

## Consequences

- Scoring is a **Match** concern; moves are a **MatchAssignment** concern. UI labels can stay user-friendly ("Move", "Score") but the dialogs and endpoints split along this boundary — see [`../ui/dialogs.md`](../ui/dialogs.md#matchmovedialog).
- "Current schedule" reads need a current-effective-assignment pointer (called out in [`../data-model.md`](../data-model.md#9-scheduling-and-assignment) Recommended additions) to keep the schedule grid fast.
- **RepairJob** flows in [`../features/repairs.md`](../features/repairs.md) rely on this split — a court outage repair shifts assignments; it never destroys match identity.
