# Spec: Tournament divisions

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack. This slice uses the .NET API, Blazor WebAssembly with MudBlazor, Entity Framework Core, and local Postgres. It does not call WorkOS, OR-Tools, or Supabase. Supabase is the hosted Postgres when deployment is in scope.

**Tournament** is one competitive event. **Organization** is the boundary that owns tournaments. A **Division** is a competitive grouping inside one tournament. Teams in different divisions never play each other. This slice does not enter teams, so that rule is not exercised here. This slice does not use Event, Club, Team, Wave, Stage, Pool, or Bracket.

Presentation follows [../look-and-feel/spec.md](../look-and-feel/spec.md). Tournament and venue behavior follows [../tournament-setup/spec.md](../tournament-setup/spec.md) except where this spec changes tournament delete.

## What the director can do

The director is not signed in and never chooses an organization. The app already has one organization, named Test Organization. Every tournament belongs to it. Its name is not shown.

The director opens a tournament that already exists. On that editor, below Venues, a Divisions section lists that tournament's divisions. From there they add a division, edit one, or delete one.

Before the tournament has been saved, Divisions stays hidden. The existing note, "Details must be saved first," still covers that. There is no second note.

A tournament may have no divisions. The empty section says "No divisions yet. Use Add to create one."

If the director opens a tournament that does not exist, the editor shows that it was not found. Divisions is not shown.

## Division

A division belongs to one tournament. It is not reused by another tournament. Deleting the tournament deletes its divisions.

Each division has:

- **Name.** Required. Blank or whitespace-only is rejected. The saved name has surrounding whitespace removed. There is no maximum length, matching tournament and venue names.
- **Gender.** Required. The only values are Male and Female.

Two divisions in the same tournament may share a name when their genders differ. The same name and gender cannot be saved twice. Comparison ignores case and surrounding whitespace. "14U Open" Female and "14U Open" Male both save. "14U Open" Female and " 14u open " Female do not. Updating a division does not conflict with itself. Changing the Female "14U Open" to Male is rejected when a Male "14U Open" already exists. Another tournament may also have "14U Open" Female.

## The Divisions section

The editor order is Details, then Venues, then Divisions. Divisions is its own card. Its header has the heading "Divisions" and an Add button labeled "Add" with the accessible label "Add division."

Each division shows its name and its gender. Edit and Delete icon buttons sit beside it. Their accessible labels include both, such as "Edit 14U Open, Female" and "Delete 14U Open, Female," so two divisions with the same name are distinct to a screen reader.

The list is ordered by name, ignoring case, and then by gender with Female before Male. "14U Open" Female appears above "14U Open" Male. "12U" appears above "14U Open."

Adding or editing opens a modal dialog, the same way a venue does. The title is "Add division" or "Edit division." The fields are Name and Gender. Gender is a select with Male and Female and no preselected value on add. The prompt is "Select a gender." Edit opens with that division's name and gender. Cancel sits on the left and Save on the right. A successful save closes the dialog, reloads the list, and shows "Saved." A rejected save or a failed call leaves the dialog open and shows the failure. Cancel saves nothing.

## Delete

Deleting a division asks the director to confirm. The title is the division's name and gender, such as "14U Open (Female)." The dialog has no body. The buttons are "Delete" and "Cancel." Cancel, Escape, and clicking outside delete nothing. Confirming removes that division. The tournament and its other divisions stay. A successful delete shows "Deleted" and reloads the list.

Deleting a tournament asks the director to confirm, then deletes that tournament, its venues, its courts, and its divisions. The confirmation body is "Its venues, courts, and divisions are also deleted." Cancel deletes nothing.

## Rules the API enforces

The Blazor app talks only to the .NET API. The API uses Entity Framework Core to write local Postgres. The UI and the API reject the same invalid saves.

- A division with a blank or whitespace-only name is rejected. The message is "Division name is required."
- A division with no gender, or a gender other than Male or Female, is rejected. The message is "Gender is required."
- A name and gender that match another division in the same tournament, ignoring case and surrounding whitespace, is rejected. The message is "A division with this name and gender already exists." The division's own current name and gender do not count.
- A division cannot be added for a tournament that does not exist.
- A division that does not exist cannot be updated or deleted.
- A division cannot be updated or deleted on a different tournament from the one that owns it.
- A tournament, venue, court, or division that belongs to a different organization is not returned and cannot be changed. With one organization, this means the API never reads or writes another organization's rows.
- Deleting a tournament deletes its divisions. Deleting a division deletes only that division.

This slice does not detect simultaneous edits. The last save wins.

## Decisions this spec makes that the intent did not

The intent left these to the spec. Change any of them before a plan is written.

- **Modal dialog.** Add and edit use a modal dialog, matching venues.
- **Stored name.** Surrounding whitespace is removed before the name is stored and before the uniqueness check.
- **Rejected duplicate.** A repeated name and gender is a rejected save. It is not a warning that still saves.
- **Tournament delete copy.** The existing tournament confirmation now mentions divisions.
- **No name length limit.** Matching tournament and venue names.

## Deliberately not in this slice

- A separate Divisions page, a tournament selector, or a Manage action into stages, pools, and brackets.
- Format, max teams, tiebreaker, age group, skill, wave, and match start times. This spec stores gender and does not store a wave.
- A uniqueness rule on name alone. This spec rejects only a repeated name and gender.
- Blocking delete because teams are placed or matches exist. This slice has no teams and no matches.
- Clubs, team entry, rankings, CSV import, pools, brackets, schedule generation, withdrawals, publish, and the public site.
- Sign-in and choosing an organization.
- A concurrency token. Simultaneous edits are not detected.

The tournament-teams intent still asks whether a team must belong to a division. This spec does not answer that.
