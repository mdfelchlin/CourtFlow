# Intent: Division ranking
Author: product owner. Status: accepted.

## Problem
A director can enter teams in a tournament's divisions, but cannot do anything with them after that. Before pools can be built and a schedule planned, the director has to rank each division's teams, ordering them from strongest to weakest. Sometimes the order is chosen deliberately. For a low-key tournament it can be random. There is no place in CourtFlow to do this, and no place where a division's later format work (pools, then brackets) will live.

## Proposed outcome
A Format item in the navigation drawer opens a Format page. The director picks a tournament and sees its divisions. Each division has an Edit button that opens that division's format editor. The editor has three steps: Ranking, Pools, and Brackets. In this slice only Ranking works. Pools and Brackets are empty placeholders.

Format holds the work that belongs to one division. Work that spans every division in a tournament, because the divisions share courts, gets its own pages in later slices: Schedule for planning matches onto courts and times, then Tournament day for running play. The navigation follows the order of the work: Teams, Format, Schedule, Tournament day.

Pools are filled from the ranking. When the Pools step is built in a later slice, the director will enter the parameters they want, and CourtFlow will fill the pools from the ranking. The ranking and the resulting pools will be visible side by side, so the director can see how the ranking shapes the pools. This slice only builds the ranking that feeds that step.

On the Ranking step the director orders the division's teams in one of three ways. They drag teams into place, type a team's rank number, or press Randomize for a random order. Then they save.

## Affected users and systems
Directors and organizers (owners and editors) use the Blazor WebAssembly app. Viewers can look but cannot change anything. The .NET API stores each division's ranking with Entity Framework Core in Postgres. It reads the division's team entries from the tournament teams slice and respects the tournament lifecycle and the sign-in and access rules. Coaches, players, and spectators are not affected. The proof of concept is reference only. Its glossary already uses **Ranking** for this ordered list and **Seed** for a team's later position in a pool.

## Constraints

**Format page**
- Format is an item in the navigation drawer, after Teams. "Schedule" is not used as the name. That word is kept for the tournament-wide court-and-time schedule in a later slice.
- Format covers one division at a time. Scheduling and running the tournament day span all divisions and are not part of Format.
- The page has the same tournament picker as the Teams page. It starts on the tournament the director last opened or picked. With none, it asks the director to select a tournament.
- If the selected tournament has no divisions, the page says to add a division on the tournament first.
- The page lists one row per division, in the Divisions list order. Each row shows the division with its gender, such as "14U Open (Female)," its team count, its ranking status, and an Edit button.
- The ranking status is "Not ranked," "Ranked," or "Ranked, N new teams at the bottom."
- A division with no teams has Edit disabled and says "Add teams first."

**Format editor**
- Edit opens the division's format editor on a page of its own, with its own address. Back returns to the Format page.
- A stepper across the top shows Ranking, Pools, and Brackets. The director can open any step directly. Nothing forces the steps in order.
- Pools and Brackets show an empty "coming later" placeholder.

**Ranking step**
- The step lists every team entered in the division, with its club, team name, and rank.
- Ranks always run from 1 to the number of teams, with no gaps and no ties.
- The director can drag a team to a new position. The other teams shift to make room.
- The director can type a rank number for a team. The team moves to that position and the other teams shift, the same as dragging it there. A number outside 1 to the team count is rejected.
- Randomize shuffles every team into a new random order. It asks no confirmation. A randomized ranking is stored like any other ranking. CourtFlow does not remember that it was random.
- Changes stay on screen until the director presses Save. Discard returns to the last saved ranking. Leaving the step or the page with unsaved changes asks the director to confirm.
- A division that has never been ranked opens with its teams in name order and stays "Not ranked" until it is first saved.

**When teams change**
- A team added to a ranked division goes to the bottom of its ranking. So does a team moved into it from another division, including through CSV import.
- A team deleted from a ranked division, or moved out of it, is removed from its ranking, and the teams below it move up.
- After teams are added to a ranked division, its status reads "Ranked, N new teams at the bottom" until the director saves the ranking again.

**Who and when**
- Owners and editors can change a ranking. Viewers can see the Format page, the editor, and the ranking. They cannot drag, type ranks, randomize, or save.
- Unpublished and published tournaments can have their rankings changed. An archived tournament's rankings are read-only, because an archived tournament cannot be edited.
- Each organization's rankings stay separate from every other organization's.

**Out of scope**
- Pool count, pool generation, and placing ranked teams into pools (seeding). The Pools step stays an empty placeholder.
- Bracket structure and the rules that move teams from pools into brackets. The Brackets step stays an empty placeholder.
- The Schedule and Tournament day pages, and their navigation items.
- A rank column in the team CSV import.
- Copying a ranking from another tournament or division.
- Showing rankings on the public site.
- Ranking history, or a record of who changed a ranking.
- New work stays outside the proof of concept.

## Open questions
None.
