# Intent: Division brackets
Author: product owner. Status: draft.

## Problem
A director can rank a division's teams and put them into pools, but cannot set up the elimination play that follows. There is no way to say what brackets a division has, how big they are, or which teams from pool play end up where. Without that, bracket matches cannot be scheduled and the division has no finish. Today the Brackets step of the Format editor is an empty placeholder.

## Proposed outcome
On the Brackets step of a division's format editor, the director sets up the division's brackets and how teams get into them. The director sees the bracket shape before any pool match is played. Teams are filled into it once pool play decides them.

## Affected users and systems
Directors and organizers (owners and editors) use the Blazor WebAssembly app. Viewers can look but cannot change anything. The .NET API stores each division's brackets with Entity Framework Core in Postgres. This builds on the division pools slice, which decides who advances, and on the division ranking slice. The later schedule and tournament day slices will read the brackets. Coaches, players, and spectators are not affected by this slice. This slice uses **Bracket** for one elimination tree, **Advancement rule** for how a pool finish feeds a bracket slot, and **BracketSeed** for a team's slot in a bracket.

## Constraints
- Brackets belong to one division. Teams in different divisions never meet in a bracket.
- Brackets are set up on the Brackets step of the division's format editor, not on a page of their own.
- The bracket shape can be set up before pool play is played. Which team fills each slot is decided later.
- Owners and editors can change brackets. Viewers can see them but cannot change them. An archived tournament's brackets are read-only.
- Each organization's brackets stay separate from every other organization's.
- Scheduling bracket matches onto courts and times is not part of this slice. That belongs to the Schedule page.
- Running bracket play on the day (scoring, advancing winners) is not part of this slice. That belongs to the Tournament day page.

## Open questions
- **Bracket types.** Which types are supported: single elimination, double elimination, or both? Are a third-place match and consolation matches options?
- **Several brackets.** Can a division have more than one bracket, such as Gold and Silver? How many?
- **Who advances.** How does the director say which teams go where? For example, "top 2 from each pool to Gold, the rest to Silver." Are there ready-made choices, and can they be fine-tuned?
- **Comparing across pools.** When slots are filled from different pools (for example, the best two third-place teams), how are those teams compared?
- **Bracket order.** Is a team's bracket slot taken from its pool finish in pool order (Pool A 1st, Pool B 1st, …), or from a comparison across pools?
- **Bracket-only divisions.** Can a division skip pools and seed its bracket straight from the ranking?
- **Pool-only divisions.** Can a division have no bracket at all?
- **Byes.** How are bracket sizes that are not 4, 8, or 16 handled?
- **Manual placement.** Can the director pin a specific team to a specific slot, overriding the rule? Is that recorded?
- **Step layout.** Is how teams advance part of the Brackets step, or a fourth step of its own?
- **Preview.** Does the director see a drawn bracket tree, or a list of slots?
- **Filling in.** When pool play ends, do teams move into the bracket automatically, or does the director confirm it?
- **Locking.** When do brackets stop being editable? For example, once the schedule is published or once a bracket match has started.
- **Public.** When, if ever, do coaches and spectators see the brackets?
