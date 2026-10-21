# Intent: Tournament divisions
Author: product owner. Status: accepted.

## Problem
A director has created a tournament, but cannot create the divisions they will add teams to. Without those divisions, the tournament has no competitive groupings for later team entry.

## Proposed outcome
For one tournament they already created, a director edits that tournament, scrolls to the Divisions section, and creates, updates, and deletes divisions. Each division has a name, such as 14U Open, and a required gender of Male or Female. Wave and further division details come in a later slice.

## Affected users and systems
Directors use the Blazor WebAssembly app. The .NET API persists the result with Entity Framework Core to local Postgres. Coaches, players, and spectators do not do this.

## Constraints
- The tournament already exists. This slice does not create tournaments, venues, or courts.
- Divisions are a section on the tournament editor, below Venues. The page is Details, then Venues, then Divisions. The section stays hidden until the tournament has been saved. This slice does not add a separate Divisions tab.
- A tournament may have no divisions. The empty section tells the director to add one.
- A division has a required name and a required gender. Gender is Male or Female. The section shows both, so two divisions with the same name are distinguishable. Wave, format, max teams, tiebreaker, and similar details are a later enhancement.
- Two divisions in the same tournament may share a name when their genders differ. The same name and gender cannot be saved twice. Comparison ignores case and surrounding spaces. Updating a division does not conflict with itself. The same name may be used in another tournament. A blank name is rejected.
- A division belongs to one tournament. It is not reused by another tournament.
- Deleting a division asks the director to confirm, then removes that division. Cancel leaves it in place. The tournament and its other divisions stay. This slice has no teams on a division, so delete does not touch clubs or teams.
- Deleting a tournament also deletes its divisions, along with its venues and courts. Cancel deletes nothing.
- This slice does not add teams. Team entry stays with the tournament teams slice.
- One fixed local organization owns the data. There is no sign-in and no organization picker.
- No waves, pools, brackets, schedule generation, rankings, withdrawals, or public site.

## Open questions
None.
