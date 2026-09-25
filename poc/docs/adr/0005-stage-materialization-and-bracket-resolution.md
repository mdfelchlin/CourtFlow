# Stage materialization and bracket resolution share one precedence story

**StageMaterialization** is the umbrella **Application module** for idempotent, **Stage**-gated operations that turn declared structure into concrete teams and **Match** rows: **Snake Rankings into Pools**, **Resolve Bracket Slots**, **Generate brackets**. They share vocabulary — re-runnable, overwrite warnings, which transitions advance **Stage status**, when a **RepairJob** is required vs in-wizard mutation — and belong in one place for **locality**.

**BracketResolution** is the precedence engine inside that family: **BracketSlotOverride** (manual pin on `bracket_seed_slots.manual_tournament_team_id`) outranks **AdvancementRule**-driven resolution, which outranks unresolved slots. The same precedence feeds bracket **Match** `match_participant_sources` and resolver diagnostics in [`../data-model.md`](../data-model.md#7-bracket-configuration-and-advancement-inside-a-bracket-stage).

## Status

accepted — complements ADR-0001 (**Match** identity vs **MatchAssignment**): materialization creates **Match** identity and structure; scheduling assigns **MatchAssignment** rows.

## Considered alternatives

- **Four independent command handlers** with no shared module. Rejected: duplicated gating matrices and idempotency prose across [`../features/division-detail.md`](../features/division-detail.md).
- **Merge bracket resolution into EF-only triggers.** Rejected: business rules must stay testable in **Application** / **Domain**, not hidden in DB triggers.

## Consequences

- One module (or a small internal family: `StageMaterialization` + `BracketResolution` as a private collaborator) owns the matrix of **StageType** × **Stage status** × action.
- Power Pool and Challenge Phase (engine-ready, wizard-deferred per [`../../CONTEXT.md`](../../CONTEXT.md)) add new **adapters** that call the same module with new parameters — they do not fork the precedence story.
- Tests: matrix tests for allowed/blocked actions; bracket precedence tests (override vs rule vs null).
