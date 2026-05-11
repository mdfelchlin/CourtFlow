# Engine interfaces: one production adapter unless a second adapter is real

`CourtFlow.Application` declares engine **interfaces** — e.g. `IScheduleGenerator`, `IConstraintChecker`, `IBracketBuilder`, `IBracketProgressionService`, `IStandingsCalculator`, `ICourtStatusService` — with implementations in `CourtFlow.Infrastructure`.

**Rule:** An `I*` is justified when (a) **Application** needs to substitute a fake or stub in fast unit tests **and** the fake is materially simpler than the real adapter without hiding integration bugs, or (b) a **second production adapter** is planned (alternate scheduler, alternate bracket builder). Otherwise prefer a **concrete class** registered directly — a hypothetical **seam** with one adapter adds **interface** surface without **leverage**.

## Status

accepted — refines the project table in [`../architecture.md`](../architecture.md) without removing existing interfaces on day one; new work must satisfy this rule before adding more `I*`.

## Considered alternatives

- **Delete all engine interfaces; use only static classes.** Rejected: loses test seams where fakes already pay rent (e.g. pure constraint checks).
- **Require two adapters for every interface from day one.** Rejected: ceremony without value.

## Consequences

- Code reviews ask: "second adapter or fake planned?" If no, consider collapsing to concrete registration.
- CI favors **integration tests** for scheduling/bracket paths; unit tests target pure helpers extracted *inside* the engine implementation, not duplicate of the whole engine.
