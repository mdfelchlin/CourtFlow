# Architecture Decision Records

One short page per **non-obvious, hard-to-reverse** decision. ADRs complement [`architecture.md`](../architecture.md) and [`architectural-prd.md`](../architectural-prd.md); they do not replace [`data-model.md`](../data-model.md) for schema detail or [`features/`](../features/) for UI specs.

**Open work:** new ADRs and hygiene tasks live in [`implementation-backlog.md` § Phase 10](../implementation-backlog.md#phase-10--adr-coverage--doc-hygiene).

## When to write an ADR

Write one when:

- The choice is costly to undo (schema shape, auth/tenancy, scheduling correctness, public API contract).
- Multiple surfaces would duplicate logic without a named seam (see 0004–0007).
- The decision is spread across `CONTEXT.md`, `data-model.md`, and code comments and keeps confusing new contributors.

Skip ADRs for conventional stack choices already covered in the architectural PRD (e.g. “we use MediatR”) unless the *local* variant matters.

## Template

```markdown
# [Short title in domain language]

[One paragraph: what we decided and why it matters.]

## Status

proposed | accepted | superseded by [000N](000N-slug.md)

## Considered alternatives

- **[Option A].** [Why rejected or deferred.]

## Consequences

- [Concrete follow-on for code, tests, or docs.]
```

Filename: `NNNN-kebab-case-slug.md` (zero-padded sequence number).

## Index

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-match-identity-vs-assignment.md) | Match identity vs MatchAssignment | accepted (partial — migration 5a–5c) |
| [0002](0002-styling-strategy.md) | Styling strategy (no SASS) | accepted |
| [0003](0003-status-lifecycle-per-aggregate.md) | Status lifecycle per aggregate | accepted (partial — LIF-01–05) |
| [0004](0004-reflow-preview-contract.md) | Reflow preview contract | accepted (partial — phases 4a–4c) |
| [0005](0005-stage-materialization-and-bracket-resolution.md) | Stage materialization and bracket resolution | accepted |
| [0006](0006-read-models-tiebreaker-delay-public-division-readiness.md) | Read models: tiebreaker, delay, public & division readiness | accepted |
| [0007](0007-engine-seams-and-testing.md) | Engine seams and testing | accepted |
| [0008](0008-personal-organizations-and-team-upgrade.md) | Invite-only tenants & org provisioning | accepted (partial — phases 8a–8d) |

## Planned (Phase 10 backlog)

Reserved numbers and titles — see Phase 10 for task IDs and blockers:

| ADR | Working title |
| --- | --- |
| 0009 | Ranking as single seeding input |
| 0010 | Stage as round container |
| 0011 | RepairJob operational model |
| 0012 | Match participant sources |
| 0013 | PoolSettings scope |
| 0014 | Turso/libSQL + SQLite dual provider |
| 0015 | Domain events + outbox |
| 0016 | Multi-tenancy via EF global filter |
| 0017 | Clerk auth topology |
| 0018 | Full-schedule SQL conflict checks |
| 0019 | ScheduleVersion fork-on-publish |
| 0020+ | Optional: Event naming, pull-only freshness, templates, ADR-0006 split |
