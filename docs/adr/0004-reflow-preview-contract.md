# Reflow preview is one Application module with a stable contract

**Reflow preview** is a coordinated **Application-layer** pattern (not a scatter of per-page logic). Every entry point — Court Status reflow, Schedule **Move** when the loaded **ScheduleVersion** is `published`, **RepairJob** dialogs that touch more than one **MatchAssignment** — crosses the same **invariants**: proposal in, diff + diagnostics + `apply_token` out when preview is required; **Apply** commits through a shared **`ScheduleMutationPolicy`** (fork-on-publish, stale-token checks, **RepairJob** audit).

Organizer intents stay **separate commands/handlers** (DDD-aligned); shared policy prevents drift on fork rules, tokens, and repair recording.

## Status

**Accepted (partial)** — contract and structure locked (2026-06-17 review). **`ScheduleMutationPolicy`**, shared preview DTO, and repair preview runners are tracked in [Implementation phases](#implementation-phases) below.

## Decisions (2026-06-17)

1. **Structure (DDD):** **Shared kernel + separate preview runners** — not one mega-engine with a `RepairTrigger` switch. Each intent (move, reflow, court outage, withdrawal, pool court swap, work-team bulk) gets its own preview logic; **`ScheduleMutationPolicy`** owns apply-time invariants.
2. **Preview required when (threshold-based):**
   - **Yes:** multi-match cascades (reflow, court outage, withdrawal, pool court swap); any apply that **forks** because loaded version is **`published`**; work-team/referee changes touching **>1** match.
   - **No:** single **Move** on **draft/preview** version (constraint re-check on apply is enough); single-match work-team/referee change.
3. **API boundary:** Per-intent **request** DTOs; shared response core **`ScheduleMutationPreviewDto`** (assignment diff, optional officials diff, conflict/diagnostics report, human summary, `apply_token`, `can_apply`).
4. **Naming:** Shared kernel = **`ScheduleMutationPolicy`** under `Application/Scheduling/` (or `ScheduleMutations/`). **`ReflowPreviewEngine`** remains one **preview runner** for court-delay reflow — not the name for the whole module. Consolidate duplicate apply-token issuers into one used by the policy.
5. **Persistence:** Preview stays **non-persistent**; only **Apply** writes (assignments via ADR-0001, **RepairJob** + **RepairAction** for audit).

## Implementation phases

| Phase | Scope | Backlog |
| --- | --- | --- |
| **4a** | Introduce **`ScheduleMutationPolicy`** + single apply-token issuer; route existing move/reflow **Apply** through it | ADR-R01 |
| **4b** | Shared **`ScheduleMutationPreviewDto`**; align move + reflow preview responses | ADR-R02 |
| **4c** | Preview runners + policy for multi-match repairs (outage, withdrawal, pool court swap) | ADR-R03 |

## Considered alternatives

- **One engine, many proposal types.** Rejected — transaction-script risk; fights separate organizer intents in ubiquitous language.
- **Per-trigger duplicate preview logic with no shared policy.** Rejected — high drift risk on `apply_token`, fork-on-publish, and **RepairTrigger** pairing.
- **Persist preview rows in the database.** Rejected — preview must remain non-persistent; only **Apply** writes.
- **Always preview every mutation.** Rejected — unnecessary friction for single moves on draft schedules.

## Consequences

- MediatR handlers (or thin controllers) delegate preview to intent-specific runners and **Apply** to **`ScheduleMutationPolicy`**.
- OpenAPI documents the shared preview response once; requests stay per endpoint.
- Tests: golden vectors per **RepairTrigger** / intent; shared policy tests for fork, stale token, and repair job creation.
- Complements ADR-0001 (assignment-only writes after migration **5a**) and planned ADR-0011 (**RepairJob** operational model).
- [`../architecture.md`](../architecture.md#deep-modules-named-application-seams) **ReflowPreview** row refers to this pattern; implementation name for the kernel is **`ScheduleMutationPolicy`**.
