# Reflow preview is one Application module with a stable contract

**Reflow preview** is a single **Application-layer module** (not a scatter of per-page logic). Every entry point — Court Status drawer, Schedule Move when the loaded **ScheduleVersion** is `published`, **RepairJob** dialogs that touch more than one **MatchAssignment** — crosses the same **seam**: proposal in, diff + diagnostics + `apply_token` out; **Apply** commits or forks per [`../data-model.md`](../data-model.md#reflow-preview).

This ADR names the module so five UI **adapters** cannot drift on stale-token handling, fork-on-publish, or **RepairTrigger** pairing.

## Status

accepted — contract summarized in [`../data-model.md`](../data-model.md#11-repair-and-live-adjustment) and feature PRDs; this ADR locks the *architectural* decision to implement as one cohesive unit.

## Considered alternatives

- **Per-trigger duplicate preview logic** in each MediatR handler. Rejected: high drift risk on `apply_token` invalidation and fork rules.
- **Persist preview rows** in the database. Rejected: preview must remain non-persistent; only **Apply** writes.

## Consequences

- One folder under `Application` (e.g. `Scheduling/ReflowPreview/`) owns: run preview (delegates to scheduler / constraint checker in dry-run mode), validate `apply_token`, apply mutation (transaction + **RepairJob** + optional new **ScheduleVersion** fork).
- Controllers expose thin endpoints that delegate to this module; OpenAPI documents the shared DTO shape once.
- Tests: golden vectors parameterized by **RepairTrigger** (court slide, outage, withdrawal, **WorkTeam** reassignment, pool court swap, schedule reflow) — same harness, different proposal payloads.
