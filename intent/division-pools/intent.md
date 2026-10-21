# Intent: Division pools
Author: product owner. Status: draft.

## Problem
A director can rank a division's teams, but cannot put them into pools. Pools are the round-robin groups a division plays first, and nothing after them can be planned without them: not brackets, not a schedule, not the tournament day. Today the Pools step of the Format editor is an empty placeholder.

## Proposed outcome
On the Pools step of a division's format editor, the director sets the constraints the division's pools must meet. CourtFlow then fills the pools from the division's ranking, finding a layout that meets those constraints. The ranking and the resulting pools are visible side by side, so the director can see how the ranking shapes the pools before relying on them.

Pools are not a fixed size. How many teams a pool holds depends on how many teams the division has and on the constraints the director sets, so pools in the same division can differ in size.

## Affected users and systems
Directors and organizers (owners and editors) use the Blazor WebAssembly app. Viewers can look but cannot change anything. The .NET API stores each division's pools with Entity Framework Core in Postgres. This builds on the division ranking slice, which supplies the ordered teams, and on the tournament teams slice, which supplies the teams entered in each division. The later brackets and schedule slices will read the pools. Coaches, players, and spectators are not affected. This slice uses **Pool** for one round-robin group and **Seed** for a team's position within a pool.

## Constraints
- Pools belong to one division. Teams in different divisions are never in the same pool.
- Pools are built on the Pools step of the division's format editor, not on a page of their own.
- Pools are filled from the division's ranking, within the constraints the director sets.
- The ranking and the pools are visible together while the director works on pools.
- The director chooses the constraints. CourtFlow does the filling.
- Pool sizes are not fixed. Pools in the same division can hold different numbers of teams.
- Owners and editors can change pools. Viewers can see them but cannot change them. An archived tournament's pools are read-only.
- Each organization's pools stay separate from every other organization's.
- Scheduling pool matches onto courts and times is not part of this slice. That belongs to the Schedule page.

## Open questions
- **Constraints.** Which constraints can the director set? For example: number of pools, smallest and largest pool size, keeping teams from the same club apart, and keeping a team in a chosen pool.
- **Must or prefer.** Is each constraint a rule that must hold, or a preference CourtFlow meets when it can?
- **No layout fits.** When no layout meets every constraint, what does the director see?
- **Balance.** How does the ranking spread teams so that pools are evenly matched? Snake order (1, 2, 3, 3, 2, 1), or the best balance CourtFlow can find within the constraints?
- **Solver.** The director thinks Google OR-Tools is needed to fill pools under these constraints. The product stack already names it for scheduling. The spec should confirm it for pools.
- **Manual changes.** After CourtFlow fills the pools, can the director move or swap teams between pools by hand?
- **Preview and save.** Is the filled result a preview that the director saves, the same as the ranking step? Or is it written as soon as it is filled?
- **Refilling.** If the director fills again after making manual changes, are those changes overwritten, and are they warned first?
- **Late changes.** What happens to existing pools when the ranking changes, or when a team is added, removed, or moved after pools are built?
- **Before teams arrive.** Can the director lay out pools (count and sizes) before every team is entered, with empty slots filled in later?
- **Pool names.** Are pools named automatically (Pool A, Pool B, …), and can the director rename them?
- **Ranking layout.** Does Ranking stay its own step, with a read-only copy shown beside the pools? Or does the ranking become a panel pinned beside every step, so it can be edited from the Pools step?
- **Matches.** Does building pools also create each pool's round-robin matches, or does that happen later?
- **Courts.** Does the director tie a pool to particular courts here, or is that left entirely to the Schedule page?
- **Locking.** When do pools stop being editable? For example, once the schedule is published or once a pool match has started.
- **Public.** When, if ever, do coaches and spectators see pool assignments?
