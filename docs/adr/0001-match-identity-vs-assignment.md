# Match identity is stable; MatchAssignment is versioned and replaceable

A volleyball **Match** (the participants, the **Stage** it belongs to, the sets played, the winner, the **Wave** session pin) is modeled separately from its **MatchAssignment** (the court, scheduled start, scheduled end / duration). Assignments are owned by a **ScheduleVersion**; reassigning a match — including the schedule page's "Move" action and any **RepairJob**-driven reflow — creates or amends a `match_assignments` row inside the current version rather than mutating scheduling fields on the `matches` row. This buys clean audit history, safe schedule regeneration, and non-destructive repair operations (court outage, team withdrawal, referee reassignment), at the cost of version-scoped reads/writes for every "where and when" question.

## Status

**Accepted (partial)** — target model locked (2026-06-17 review). Implementation migration is phased; see [Migration phases](#migration-phases) below. Until phase **5b** ships, some code paths still dual-write scheduling fields on `matches` (legacy shim).

## Decisions (2026-06-17)

1. **North star:** `match_assignments` on a **ScheduleVersion** are the authoritative source for court and time. Finish the migration; dual-write is not the long-term model.
2. **End state on `matches`:** Remove duplicated scheduling columns (`court_id`, `scheduled_start_utc`, `duration_minutes`). Add **`current_match_assignment_id`** (nullable FK), refreshed when a **ScheduleVersion** is **published**, for fast reads of the live schedule. **`Wave` stays on `Match`** — it is competition/session structure (division pin), not schedule execution.
3. **Read rules by surface:**
   - **`/schedule` (admin):** reads **MatchAssignment** rows for the **loaded** version (draft / preview / published) — the editing surface.
   - **Court Status** (Cards, Grid, TV), **Public event**, **Matches list**, bracket time/court chips: read the **published** assignment via **`current_match_assignment_id`** (or join published version). Court Status is tournament-day ops on the **live** schedule only — never draft/preview.
   - **Court Status grid:** only matches with a **published** assignment appear; unscheduled matches are omitted (not TBD placeholders on the court×time matrix). Other surfaces may show TBD for time/court until schedule is published.
4. **Before schedule generation:** **Match** rows exist from materialization; **MatchAssignment** rows do not. Pointer is `NULL`. Display TBD on non-ops surfaces; omit from Court Status grid.
5. **Generate schedule:** writes assignments into the working **ScheduleVersion** only. Pointer updates on **publish**, not on generate.

## Migration phases

| Phase | Scope | Backlog |
| --- | --- | --- |
| **5a** | Stop dual-write: move, reflow, generate, repairs update **`match_assignments` only**; remove `Match.AssignCourt` / `Reschedule` from mutation paths | ADR-M01 |
| **5b** | Switch reads per [Read rules](#decisions-2026-06-17) above; add pointer + publish refresh; remove `?? match.ScheduledStartUtc` fallbacks | ADR-M02 |
| **5c** | Schema cleanup: drop deprecated scheduling columns on `matches`; backfill pointer from published assignments | ADR-M03 |

## Considered alternatives

- **Mutate `matches` directly on move/reschedule.** Simpler reads, no joins. Rejected — reflow, version fork, and repair audit all require versioned assignments.
- **Keep scheduling columns as a permanent denormalized cache.** Rejected — two sources of truth; sync bugs. Pointer to published assignment is enough.
- **Event-source the schedule.** Rejected — rest of system is state-based per [`../architectural-prd.md`](../architectural-prd.md).

## Consequences

- Scoring and **Match clock** (`started_at` / `completed_at`) stay on **Match**; moves stay on **MatchAssignment**. Delay math uses assignment `scheduled_start` on the **published** schedule (see ADR-0006 **MatchClockDelaySignal**).
- **RepairJob** flows in [`../features/repairs.md`](../features/repairs.md) shift assignments; they do not destroy match identity.
- ADR-0004 reflow/move **Apply** must follow the same assignment-only write rule after phase **5a**.
