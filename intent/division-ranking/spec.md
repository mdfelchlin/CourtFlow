# Spec: Division ranking

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack. This slice uses the .NET API, Blazor WebAssembly with MudBlazor, Entity Framework Core, and local Postgres. It does not call OR-Tools. The ranking rules live in the domain model. The application layer holds the use cases that load a tournament, change a ranking, and save.

A **Ranking** is the ordered list of a division's **TournamentTeams**, strongest first. A team's **rank** is its position in that list. This slice does not use **Seed**, which means a team's later position inside a pool. It does not use Stage, Pool, Bracket, or Wave. In UI copy, "team" means the TournamentTeam.

Presentation follows [../look-and-feel/spec.md](../look-and-feel/spec.md). The tournament dropdown follows [../current-tournament/spec.md](../current-tournament/spec.md). Entries and import follow [../tournament-teams/spec.md](../tournament-teams/spec.md). Archived tournaments follow [../tournament-lifecycle/spec.md](../tournament-lifecycle/spec.md). Roles follow [../sign-in-and-access/spec.md](../sign-in-and-access/spec.md). This spec adds to each of them only what is written here.

## Navigation

The drawer gains a third member link, **Format**, with the `AccountTree` icon, going to `/format`. It sits after Teams. It is marked active on `/format` and on every address under it. Everyone with a membership sees it, as they see Teams. The words Schedule and Tournament day do not appear anywhere in this slice.

## Format page

The page is at `/format`. Its title is "Format · CourtFlow", and its `h1` is "Format." The shared tournament dropdown sits on the title line, at the far right, as on Teams. It lists the same tournaments Teams lists for the same person: every tournament for owners and editors, and only Published tournaments for viewers.

- The page opens on the current tournament, as Teams does. Choosing a tournament here makes it current for every page that uses the dropdown.
- With nothing current, the page shows "Select a tournament to see its divisions."
- With no tournaments, the page shows the same message and link the Teams page shows today: "No tournaments yet. Create one on the Tournaments page." with a link to `/tournaments`.
- While the tournament list or the divisions load, an indeterminate progress bar shows and the table does not.
- If the current tournament has no divisions, the page shows "Add a division to this tournament before ranking teams." with a link to that tournament's editor.
- If the tournament is Archived, the note "This tournament is archived. Restore it to make changes." shows above the table, as on Teams.

### The divisions table

One row per division, in the order of the Divisions list on the tournament editor: by name ignoring case, then Female before Male. Columns:

- **Division.** Name and gender, such as "14U Open (Female)."
- **Teams.** The number of teams entered, such as "12 teams." One team reads "1 team." None reads "0 teams."
- **Ranking.** One of:
  - "Not ranked"
  - "Ranked"
  - "Ranked, 3 new teams at the bottom." One reads "Ranked, 1 new team at the bottom."
  - "Add teams first," when the division has no teams. This replaces the ranking status.
- **Actions.** An Edit icon button with the accessible label and tooltip "Edit 14U Open (Female)." For a viewer, or for an Archived tournament, the button is View, with the `Visibility` icon and the label "View 14U Open (Female)." It opens the same editor, read-only.

A division with no teams has its button disabled. Its tooltip reads "Add teams first."

Below MudBlazor's small breakpoint, each row stacks into a labeled block, as the teams table does.

## Format editor

The editor is at `/format/{divisionId}`. Each step has its own address:

- `/format/{divisionId}/ranking`
- `/format/{divisionId}/pools`
- `/format/{divisionId}/brackets`

`/format/{divisionId}` opens the Ranking step.

- **Header.** A back icon button labeled "Back to format" sits beside the `h1`, which is the division's name and gender, such as "14U Open (Female)." Below it, the tournament's name is plain text. The editor does not show the tournament dropdown.
- **Current tournament.** Opening the editor makes the division's tournament current, the same way opening a tournament in the tournament editor does. Back always lands on the Format page showing that tournament.
- **Stepper.** A horizontal stepper below the header shows Ranking, Pools, and Brackets, in that order. Each step is a link to its address. Any step can be opened at any time, in any order. The step being shown is marked. No step is marked complete in this slice.
- **Pools and Brackets.** Each shows an info alert: "Pools are coming later." or "Brackets are coming later." Nothing else.
- **Not found.** An address with a division that does not exist, belongs to another organization, or belongs to a tournament the person may not see shows "This division was not found." inside the frame, with a link back to `/format`. The API answers `404`, as it does for any other record the person may not see.
- **Archived.** For an Archived tournament, the same archived note as the Format page shows above the stepper.

## Ranking step

### What it shows

A list of every team entered in the division, in rank order. Each row has, from left to right:

- a drag handle,
- a **Rank** number field holding the team's rank,
- the club,
- the team name,
- a "New" label when the team joined the ranking after it was last saved.

Ranks always run from 1 to the number of teams, with no gaps and no ties. That holds on screen while the director edits as well as in what is saved.

Above the list sit the ranking status, using the same words as the Format page, and the buttons Randomize, Discard, and Save.

A division that has never been ranked shows its teams in name order, as the teams table orders teams: by team name ignoring case, then club name ignoring case. Its status reads "Not ranked." Under it, an info alert reads "Teams are shown in name order. Save to keep this order, or change it first."

A division with no teams, opened by address, shows "Add teams first." and the list, Randomize, Discard, and Save do not show.

### Reordering

- **Drag.** The director drags a row by its handle to a new position. The rows between shift by one to make room. Every rank updates at once.
- **Type a rank.** The director types a number in a row's Rank field and presses Enter or leaves the field. The team moves to that position, and the rows between shift, exactly as if it had been dragged there.
  - Typing the team's own rank changes nothing.
  - A blank value, a value that is not a whole number, or a number outside 1 to the team count is rejected. A warning snackbar reads "Enter a rank from 1 to 12," using the team count. The field returns to the team's current rank, and nothing moves.
- **Randomize.** Shuffles every team into a random order. Every order is equally likely, except that the result is never the order on screen. It asks no confirmation. It is disabled when the division has one team. A randomized ranking is saved like any other. Nothing records that it was random.

### Saving and discarding

Every change stays on screen until the director presses Save.

- **Save** stores the order on screen as the division's ranking. It shows "Saved." The division becomes "Ranked," and every "New" label clears.
- Save is enabled when the order on screen differs from the saved ranking, when the division is "Not ranked," or when it has new teams at the bottom. Saving an unchanged order therefore ranks a never-ranked division in name order, or accepts new teams where they sit. Otherwise Save is disabled.
- **Discard** reloads the saved ranking from the API, including any team changes made since the editor opened, and throws away the order on screen. It asks no confirmation. It is enabled only when the order on screen differs from the last order loaded. For a never-ranked division it returns to name order.
- A failed call keeps the order on screen and shows the failure, as other saves do.

### Leaving with unsaved changes

The order on screen is unsaved when it differs from the last order loaded.

- **Inside the app.** Moving to another step, pressing Back, choosing a drawer link, or following any other in-app link opens a confirmation. Title: "Discard ranking changes?" Body: "Your changes to this ranking have not been saved." Buttons: "Discard" and "Keep editing." Discard drops the changes and continues. Keep editing, Escape, and clicking outside stay on the step with the changes intact.
- **Leaving the app.** Reloading, closing the tab, typing an address, or signing out triggers the browser's own "leave site?" prompt. Its wording comes from the browser and cannot be changed.
- With nothing unsaved, nothing asks.

### When the teams changed while editing

Someone may add, move, or delete a team in this division while the editor is open, in another tab or as another editor. The saved ranking always matches the division's teams, so the order on screen can fall out of date.

When Save finds that the teams on screen are not exactly the division's current teams, the API rejects it and writes nothing. The editor then reloads the division's teams and keeps the director's order: teams no longer in the division are removed and the rows below move up, and teams that joined are added at the bottom, marked "New." A warning snackbar reads "The teams in this division changed. Review the ranking and save again." The changes stay unsaved.

Two people saving different orders for the same teams is not detected. The last save wins.

## When teams change

These rules hold however the entry changes: the Add dialog, "Save and add another," the Edit dialog, Delete, or CSV import. Each entry change and its ranking change are saved together. Neither is ever saved without the other.

- **Added to a ranked division.** The team goes to the bottom of its ranking and counts as new.
- **Moved into a ranked division** from another division of the same tournament, through Edit or import. Same as added.
- **Several at once.** Teams added or moved in one import go to the bottom in file order. Teams added one by one go to the bottom in the order they were saved.
- **Deleted from, or moved out of, a ranked division.** The team is removed from that ranking, and the teams below it move up one. If it was new, the count of new teams drops by one.
- **Added to, or moved into, a division that is not ranked.** Nothing is stored. The division stays "Not ranked," and the editor still opens in name order.
- **The last team leaves a ranked division.** The division goes back to "Not ranked." Teams added later start in name order.
- **A team that leaves and comes back** counts as new again, at the bottom.
- **Status.** A ranked division reads "Ranked, N new teams at the bottom" while N of its teams joined after its last save. Saving the ranking again resets N to zero, wherever those teams now sit.
- **No effect.** Renaming a team, editing an entry without changing its division, an import row with no change, and renaming a division or changing its gender leave the ranking alone.
- **Deletes.** A division can already be deleted only when it has no teams. Its ranking goes with it. Deleting a tournament deletes its rankings.

## Who can do what

- **Owners and editors**, on an Unpublished or Published tournament, can drag, type ranks, randomize, discard, and save.
- **Viewers** see the Format page, the editor, and the ranking for Published tournaments only, as everywhere else. They see no drag handles. Ranks show as plain numbers, not fields. Randomize, Discard, and Save do not show. Never-ranked divisions show in name order with the status "Not ranked" and no alert.
- **Archived tournaments** look the same as a viewer's for everyone. The archived note shows.
- Nothing unsaved is possible in a read-only editor, so it never asks before leaving.

## Rules the API enforces

The Blazor app talks only to the API. The API is the authority, and the app rejects the same invalid saves.

- The division list for the Format page returns each division's team count, its ranking status, and its number of new teams.
- A division's ranking returns its teams in rank order with their club, team name, rank, and whether each is new, plus whether the division is ranked. For a division that is not ranked, it returns the teams in name order and says so.
- A save sends the division's full order as a list of entry IDs.
  - It must list every entry in the division exactly once and nothing else. Otherwise it is rejected with "The teams in this division changed. Review the ranking and save again." Nothing is written.
  - It is rejected for an Archived tournament with "This tournament is archived. Restore it to make changes."
  - It is rejected with `403` for a viewer.
  - A division or tournament the person may not see answers `404`.
- Every entry add, move, delete, and import applies the ranking rules in "When teams change" in the same database change as the entry.
- A ranking belongs to its division, inside its tournament, and so to one organization. Another organization's rankings are never returned or changed.
- At the database, an entry appears in at most one ranking, at most once, and ranks within a division are unique.

The ranking is part of the tournament in the domain model, alongside its divisions and entries. Adding, moving, and removing an entry update the ranking in the same operation, so no use case can change one without the other.

## Accessibility

- The Rank field is the keyboard way to reorder. It is labeled with the team, such as "Rank of Ohio St U, Ohio State University."
- The drag handle is labeled "Drag Ohio St U, Ohio State University."
- After a team moves, by drag or by typing, a polite live region announces "Ohio St U moved to rank 3 of 12."
- After Randomize, it announces "Ranking randomized."
- The "New" label is text, not color alone.
- The stepper's steps are links reachable by keyboard. The current step is marked for screen readers.

## Phone width

At 360 px wide the page does not scroll sideways. Rows keep the handle, the Rank field, the club, and the team, with club and team wrapping as needed. Typing a rank works at every width. Whether dragging works by touch depends on MudBlazor's drop container. See "Flagged for the owner."

## How this is checked

- **Domain tests:**
  - Saving an order that is not exactly the division's entries is rejected.
  - Ranks run 1 to N after every save, add, move, and delete.
  - Adding and moving in append at the bottom in order and count as new. Removing and moving out close the gap.
  - Saving clears the new count. The last team leaving returns the division to not ranked.
  - Entry changes on a not-ranked division store nothing.
  - An Archived tournament's ranking cannot be saved.
- **API tests:**
  - Save and read back a ranking. The Format list shows "Not ranked," "Ranked," and the new count.
  - Adding through the Add endpoint, moving through Edit, deleting, and importing each update the ranking in the same change, including several import rows in file order.
  - A save with a missing, extra, or repeated entry is rejected and writes nothing.
  - Archived is rejected. A viewer gets `403` on save and sees Published tournaments only. Another organization's division answers `404`.
- **Component tests:**
  - The drawer shows Format after Teams, active on `/format` and under it.
  - The Format page's empty, no-divisions, archived, and loading states. Rows in Divisions list order. Team counts and status wording, singular and plural. Edit disabled with "Add teams first."
  - The editor's three step addresses, Back, the stepper, and the two placeholders. "This division was not found."
  - Drag and typed rank produce the same order. An out-of-range rank is rejected and nothing moves.
  - Randomize never returns the order on screen, using an injected random source.
  - Save and Discard enabling. Saving an unchanged never-ranked division ranks it.
  - The leave confirmation on step change, Back, and a drawer link, and none when nothing is unsaved.
  - The teams-changed rejection keeps the order, drops gone teams, and appends new ones marked "New."
  - Read-only for a viewer and for an Archived tournament: no handles, no fields, no buttons.

## Decisions this spec makes that the intent did not

The intent left these open, or did not mention them. Change any of them before a plan is written.

- **Addresses and icon.** The Format page is `/format` with the `AccountTree` icon. Each editor step has its own address under `/format/{divisionId}`.
- **Opening the editor sets the current tournament.** So Back always lands on the right tournament, even from a link.
- **View instead of Edit.** For viewers and Archived tournaments, the row button reads View with a view icon. The intent named only Edit.
- **"New" label in the editor.** Teams that joined since the last save are marked in the list as well as counted on the Format page.
- **Save on an unchanged order.** Save is enabled for a never-ranked division and for a division with new teams, so the director can accept the order as it is.
- **Randomize always changes the order** and is disabled for a single team, so pressing it visibly does something.
- **Discard reloads from the API** and asks no confirmation, like Randomize.
- **Invalid typed rank.** Rejected with a snackbar, and the field returns to the team's rank.
- **The last team leaving** returns a ranked division to "Not ranked."
- **A team that leaves and returns** counts as new.
- **Teams changed while editing.** Save is rejected, and the editor merges the change into the director's order rather than throwing it away.
- **Unsaved-changes prompt.** CourtFlow's own dialog inside the app, and the browser's prompt when leaving it. No page in CourtFlow has asked before leaving until now.
- **Wording.** All on-screen copy above is this spec's choice, except the statuses, "Add teams first," and the archived note, which come from the intent and the lifecycle spec.

## Flagged for the owner

- **Ranking layout may change with pools.** The [division pools intent](../division-pools/intent.md) asks whether the ranking stays its own step or becomes a panel beside every step. This spec builds it as a step, as this intent says. The pools spec may move it.
- **Rankings never lock.** Here a ranking stays editable until the tournament is archived. Later slices that build pools and run play will need to say whether, and when, a ranking stops changing, and what a late change does to pools.
- **What "not ranked" means downstream.** A division can have teams and no saved ranking. The pools slice must decide whether it can fill pools from a division that is not ranked.
- **Dragging by touch.** Whether MudBlazor's drop container drags reliably on phones and tablets is unconfirmed. The plan should check it. Typing a rank works either way.
- **Leaving the app.** The browser's prompt on reload or close cannot carry CourtFlow's wording, and some browsers skip it unless the person has interacted with the page.
- **Simultaneous saves.** Two people saving different orders for the same teams are not warned. The last save wins, as elsewhere in CourtFlow.

## Deliberately not in this slice

- Pools, pool counts, seeding teams into pools, and anything on the Pools step beyond its placeholder.
- Brackets and advancing teams into them. The Brackets step stays a placeholder.
- The Schedule and Tournament day pages and their drawer links.
- A Rank column in the team CSV import.
- Copying a ranking from another tournament or division.
- Rankings on the public site.
- Ranking history, or who changed a ranking.
- Recording that a ranking was randomized, or a repeatable random seed.
- Locking a ranking once play starts.
- A saved source or a manual-override flag on a rank.
