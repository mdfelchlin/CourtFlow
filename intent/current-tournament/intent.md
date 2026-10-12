# Intent: Current tournament
Author: product owner. Status: accepted.

## Problem
A page that works inside a tournament has no shared way to show which tournament that is. Teams puts its own tournament dropdown under the page title. The next page that needs the same thing would place its own control somewhere else. The director cannot see the current tournament in the same spot on every such page, or switch it from the corner of the page.

## Proposed outcome
Every page that needs to know the current tournament uses one reusable dropdown of the tournaments available to the director.

The dropdown sits in the far upper-right of the page, on the same line as the page title. A label sits immediately to its left. When that line also has other actions, those actions sit to the left of the label.

The dropdown shows the current tournament. Choosing another tournament makes that the current tournament for every page that uses the dropdown. A page opens on the tournament the director last chose.

When no tournament is current, the dropdown shows "Select tournament". It shows the same prompt when the director has no tournaments yet.

The Teams page uses this dropdown. It no longer has its own tournament picker under the title.

The tournament list does not show the dropdown. That page is the list of tournaments, not work inside one. The tournament editor does not show it either. That page already opens one tournament from its address.

## Affected users and systems
Directors use the Blazor WebAssembly app. This slice adds the shared dropdown and moves the Teams page onto it. The API and the database do not change. Coaches, players, and spectators do not use it. The proof of concept is reference only.

## Constraints
- The dropdown is on the page, in line with the title. It is not in the app bar.
- One current tournament is shared by the pages that use the dropdown.
- The tournament list and the tournament editor do not show it.
- With no current tournament, the dropdown shows "Select tournament".
- New work stays outside the proof of concept.

## Open questions
None.
