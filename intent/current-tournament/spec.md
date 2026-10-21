# Spec: Current tournament

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack. This slice changes only the Blazor WebAssembly app. The API, the domain, and the database do not change.

A **Tournament** is one competitive event. The dropdown lists tournaments. It does not list Events.

Presentation follows [../look-and-feel/spec.md](../look-and-feel/spec.md). Team behavior follows [../tournament-teams/spec.md](../tournament-teams/spec.md) except where this spec moves the Teams tournament picker.

## The dropdown

A page that works inside a tournament shows one shared dropdown of the tournaments available to the director. Those are the tournaments of Test Organization, the same tournaments as the tournament list, in that list's order: start date, then name.

The dropdown sits in the page header, on the same line as the page title, at the far right. A label reading **Tournament** sits immediately to its left, on that same line, vertically centered with the control. The label is tied to the dropdown for screen readers. When that line also has actions, those actions sit to the left of the label. On the Teams page that order is Import CSV, Add, then the label and dropdown.

The closed dropdown shows the current tournament's name. With no current tournament, it shows the prompt **Select tournament**. It shows that same prompt when the director has no tournaments yet. Opening it then lists no tournaments.

Each option is the tournament's name. Two tournaments may share a name, as tournament setup already allows. Both appear, in list order, and either can be chosen.

The director cannot clear the dropdown back to empty. Switching means choosing another tournament. The prompt returns only when nothing is current: nothing has been remembered, or the remembered tournament is no longer in the list.

On a narrow screen the header may wrap, so the label and dropdown stay together under the title instead of forcing the page to scroll sideways. At 360 px wide the page still does not scroll sideways.

The dropdown is reachable from the keyboard. The director can open it, move through the tournaments, and choose one.

While the tournament list is loading, the page shows its progress indicator and the dropdown is not shown yet.

## The current tournament

There is one current tournament, shared by every page that uses the dropdown. It is the tournament the director last chose in the dropdown, or the tournament they last opened in the tournament editor. That memory is the one the Teams page already keeps: stored in the browser, and still there after the browser is closed.

- Choosing a tournament in the dropdown makes it the current tournament and reloads that page's tournament-scoped content.
- Opening an existing tournament in the editor still sets the current tournament, as the teams spec already requires. The editor does not show the dropdown.
- Saving a new tournament opens its editor, which sets the current tournament the same way.
- A page that uses the dropdown opens on the remembered tournament when that tournament is still in the list.
- When nothing is remembered, or the remembered tournament is gone, nothing is current, the prompt is shown, and the remembered value is cleared.

A later page that needs the current tournament uses this same dropdown in its header. It does not add a second picker or a second memory.

## Where it appears

**Teams.** The heading stays "Teams." The dropdown replaces the tournament select that sat under that heading. The prompt under the heading, "Select a tournament," is gone.

The rest of the Teams page stays as the teams spec describes it:

- With nothing selected, the page shows "Select a tournament to see its teams." Add and Import CSV are disabled.
- With no tournaments at all, the page shows "No tournaments yet. Create one on the Tournaments page." with a link to `/`.
- Changing the tournament reloads the divisions and teams and resets the filter and search.
- The no-divisions message, the team table, Add, Edit, Delete, and Import CSV are unchanged.

**Tournament list.** The list at `/` does not show the dropdown.

**Tournament editor.** Neither `/tournaments/new` nor `/tournaments/{id}` shows the dropdown.

**Not found.** The unknown-route page does not show the dropdown.

## How this is checked

- The Teams header shows the label "Tournament" immediately to the left of the dropdown, on the title line, to the right of Import CSV and Add.
- With a remembered tournament that still exists, the dropdown shows that tournament's name and the page loads its teams.
- With nothing remembered, the dropdown shows "Select tournament" and the page shows "Select a tournament to see its teams."
- With no tournaments, the dropdown shows "Select tournament" and the page shows "No tournaments yet. Create one on the Tournaments page."
- Choosing a tournament remembers it, loads that tournament's teams, and resets the filter and search.
- A remembered tournament that is no longer in the list leaves nothing selected and clears the memory.
- The tournament list, the tournament editor, and the not-found page render no tournament dropdown.
- Opening an existing tournament in the editor still remembers it, and Teams then opens on that tournament.
- The app bar still has no tournament selector.

## Decisions this spec makes that the intent did not

The intent left these to the spec. Change any of them before a plan is written.

- **Names only.** Options show the tournament name, in tournament-list order. Shared names are not disambiguated with dates.
- **No clear.** The director cannot blank the current tournament from the dropdown.
- **Same memory.** The dropdown uses the browser memory the Teams page and the tournament editor already share. Opening a tournament in the editor still sets it.
- **Teams copy stays.** The page messages for nothing selected and for no tournaments stay. Only the picker moves, and its prompt becomes "Select tournament".
- **Wraps on a narrow screen.** The dropdown stays in the header and the page does not scroll sideways at 360 px.

## Deliberately not in this slice

- A tournament selector in the app bar.
- Selecting the most recent tournament when nothing is remembered.
- A dropdown on the tournament list, the tournament editor, or the not-found page.
- Any new page. Only Teams adopts the dropdown.
- A change to the API or the database.
