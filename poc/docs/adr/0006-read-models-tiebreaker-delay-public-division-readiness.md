# Read models: TiebreakerChain, MatchClockDelaySignal, PublicEventReadiness, DivisionReadinessProjection

Four **read-model** or **projection** modules live in **Application** (or `Infrastructure` queries invoked only through Application queries). They are **not** aggregate roots — they **derive** views from persisted state so many UI surfaces share one **interface** contract.

## 1. TiebreakerChain

**Owns:** Given **TournamentTeam**s in a **Pool**, their **Match** results, and the **Division**'s **TiebreakerPreset**, produce a stable order plus which chain step broke each tie. Same ordering feeds live **Standing**, `advancement_rules.order_by = tiebreaker_rank` wildcard expansion, and the read-only chain preview in **DivisionDialog**.

**Tests:** Table-driven cases for all four V1 presets; head-to-head subset when three+ teams tie; forfeit convention; stable **Ranking** fallback.

## 2. MatchClockDelaySignal

**Owns:** Per-**Match** delay minutes from **Match clock** (`matches.started_at` / `matches.completed_at`) and the current **MatchAssignment**'s `scheduled_start`, per the canonical formula in [`../data-model.md`](../data-model.md#match-clock-writes). **NULL** means "no delay signal yet" (future on-schedule **Match**); implementations **must not** coerce NULL to `0` for aggregation — NULL matches are excluded from "worst positive delta" for court chips. Band thresholds map to design-system `--cf-tint-*` tokens ([`../ui/design-system.md`](../ui/design-system.md#semantic-surface-tints)).

**Tests:** Golden vectors over (`started_at`, `scheduled_start`, `now`, `match_status`); aggregation ignores NULL.

## 3. PublicEventReadiness

**Owns:** Which tabs on the public **Event** page render vs show placeholders, composed from **Tournament.Status**, published **ScheduleVersion**, resolved **BracketSeed**s, and whether scores exist — per [`../features/public-event.md`](../features/public-event.md).

**Tests:** Cartesian tests for tab × upstream state.

## 4. DivisionReadinessProjection

**Owns:** How coarse **Division** checklist **Status** relates to fine **Stage status** so Division detail blocking strips and validators never contradict each other. If `division_status` is cached, it is **derived** from **Stage** rows, not independently authoritative for gating.

**Tests:** Invariant tests that UI gates cannot disagree across the two levels.

## Status

accepted.

## Considered alternatives

- **Inline the formulas in each Razor page.** Rejected: guaranteed drift (already caught once: NULL vs `0` for future matches in delay math — fixed in docs; implementation must follow **MatchClockDelaySignal**).

## Consequences

- Single injectable query/service per module for Blazor pages and MediatR handlers.
- [`../architecture.md`](../architecture.md#deep-modules-named-application-seams) table links here for the one-line summary.
