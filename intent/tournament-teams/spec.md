# Spec: Tournament teams

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack. This slice uses the .NET API, Blazor WebAssembly with MudBlazor, Entity Framework Core, and local Postgres. It does not call WorkOS, OR-Tools, or Supabase.

A **Club** is a long-lived organization record that owns teams. A **Team** is a long-lived record owned by one club. A **TournamentTeam** is one team's entry in one tournament and one division. Inside the Teams page, "team" in UI copy means the TournamentTeam. Data, domain code, and API contracts use the precise terms. This slice does not use Wave, Stage, Pool, Bracket, Ranking, or Seed.

Presentation follows [../look-and-feel/spec.md](../look-and-feel/spec.md). Division behavior follows [../tournament-divisions/spec.md](../tournament-divisions/spec.md) except where this spec changes division and tournament delete.

## What the director can do

The director is not signed in and never chooses an organization. Every club, team, and tournament belongs to Test Organization.

The drawer gains a second link, **Teams**, with the `Groups` icon, going to `/teams`. It is marked active on that page. Tournaments stays first.

On the Teams page the director picks a tournament, then lists, adds, edits, deletes, and imports that tournament's teams.

## Choosing the tournament

The page heading is "Teams." Below it is a **Tournament** select listing the organization's tournaments by name, in the same order as the tournament list. The prompt is "Select a tournament."

- The page remembers one tournament per browser. Picking a tournament here remembers it. Opening an existing tournament in the tournament editor also remembers it.
- On load, the page selects the remembered tournament. If nothing is remembered, or the remembered tournament no longer exists, nothing is selected and the remembered value is cleared.
- With nothing selected, the page shows "Select a tournament to see its teams." Add and Import CSV are disabled.
- With no tournaments at all, the page shows "No tournaments yet. Create one on the Tournaments page." with a link to `/`.
- Changing the selection reloads the divisions and teams and resets the filter and search.

## Clubs

A club belongs to the organization. The same club is used in every tournament.

- **Name.** Required. Blank or whitespace-only is rejected. Surrounding whitespace is removed before storing. There is no maximum length.
- Clubs are matched by name, ignoring case and surrounding whitespace. "Ohio State University" and " ohio state university " are the same club.
- A club is created the first time a saved team names it. The stored spelling is the spelling of that first save. Later saves that match it do not change its spelling.
- Clubs are never deleted or renamed in this slice. A club with no teams entered anywhere still exists.

## Teams

A team belongs to one club and is used in every tournament it enters.

- **Name.** Required. Blank or whitespace-only is rejected. Surrounding whitespace is removed before storing. There is no maximum length.
- Within a club, teams are matched by name, ignoring case and surrounding whitespace. Two teams in the same club cannot share a name. Two clubs may each have a team with the same name.
- A team's club is set when it is created and never changes.
- Renaming a team renames it everywhere it is entered. The new name must not match another team in the same club. Changing only the case of a team's own name is allowed and is a rename.
- Teams are never deleted in this slice. Removing a team from a tournament removes only its entry.

## Entries

A TournamentTeam is one team entered in one tournament, in one division of that tournament.

- A team is entered in a tournament at most once.
- The team's gender is its division's gender. Nothing else stores a gender.
- Changing the entry's division moves that team within the tournament.
- Deleting the entry removes the team from that tournament. The team and its club remain.

## The teams table

When a tournament is selected, its teams appear in one table with the columns Club, Team, Division, and actions. Division shows the name and gender, such as "14U Open (Female)." The actions are Edit and Delete icon buttons. Their accessible labels name the team and club, such as "Edit Ohio St U, Ohio State University."

Rows are ordered by division, in the same order as the Divisions list on the tournament editor, then by team name ignoring case, then by club name ignoring case.

Above the table:

- An **Add** button with the accessible label "Add team," and an **Import CSV** button.
- A **Division** filter select with "All divisions" as its default and each division as "Name (Gender)."
- A **Search** box that matches any part of the team or club name, ignoring case.
- A count. With no filter or search it reads "157 teams." With either applied it reads "50 of 157 teams." One team reads "1 team."

Empty and loading:

- While divisions and teams load, an indeterminate progress bar shows and the table does not.
- If the tournament has no divisions, the page shows "Add a division to this tournament before adding teams." with a link to that tournament's editor. Add and Import CSV are disabled.
- If the tournament has divisions but no teams, the page shows "No teams yet. Use Add or Import CSV."
- If the filter and search match nothing, the table shows "No teams match."

Below MudBlazor's small breakpoint, each row stacks into a labeled block, as the tournament list does.

## Add

Add opens a modal dialog titled "Add team." The fields are Club, Team, and Division, in that order.

- **Club** is a search box over the organization's clubs, matching any part of the name, ignoring case. Typing a name that matches no club offers it as a new club, shown as "Add new club: …" in the list. The new club is created only when the team is saved.
- **Team** is disabled until a club is chosen. It searches that club's teams. Typing a name that matches no team in that club offers it as a new team, shown as "Add new team: …". Typing a name that matches an existing team, ignoring case and surrounding whitespace, picks that team rather than creating one. Teams already entered in this tournament are shown with "(already entered)" and cannot be picked. Changing the club clears the team.
- **Division** is a select of this tournament's divisions as "Name (Gender)," in the Divisions list order, with the prompt "Select a division." Nothing is preselected the first time the dialog opens.

The buttons are Cancel on the left, and "Save and add another" and Save on the right.

- **Save** saves the team, closes the dialog, reloads the table, and shows "Saved."
- **Save and add another** saves the team and shows "Saved." The dialog stays open. Club and Division keep their values. Team is cleared and focused. The table reloads when the dialog closes.
- **Cancel**, Escape, and clicking outside close the dialog without saving the current form. Teams saved earlier in the same dialog stay saved.
- A rejected save or a failed call leaves the dialog open, keeps every field, and shows the failure.

## Edit

Edit opens a modal dialog titled "Edit team." It shows Club as read-only text, Team as a text field holding the team's name, and Division as the same select as Add with the entry's division selected.

- Save renames the team if the name changed and moves the entry if the division changed. It closes the dialog, reloads the table, and shows "Saved."
- Cancel, Escape, and clicking outside save nothing.
- A rejected save or a failed call leaves the dialog open and shows the failure.

The club cannot be changed. To use a different club, the director deletes the row and adds the right team.

## Delete

Delete asks the director to confirm. The title is the team and club, such as "Ohio St U (Ohio State University)." The body is "The team stays available for other tournaments." The buttons are "Delete" and "Cancel." Cancel, Escape, and clicking outside delete nothing.

Confirming removes the entry, shows "Deleted," and reloads the table.

## Import CSV

Import CSV opens a large modal dialog titled "Import teams."

### Choosing a file

The dialog opens with a drop zone that accepts one `.csv` file by drag and drop or by clicking to browse. It has a "Download template" link. The template is a file named `teams-template.csv` holding only the header row `Club,Team,Division,Gender`.

Choosing a file sends it for a preview. Nothing is written. Choosing another file replaces the preview.

### Reading the file

These problems reject the whole file. The dialog shows the message and no row table.

| Problem | Message |
| --- | --- |
| Larger than 2 MB | "The file is larger than 2 MB." |
| Not valid UTF-8 | "Save the file as CSV UTF-8 and try again." |
| No header row | "The file is empty." |
| A required column is missing | "The file is missing the Club column." Each missing column is listed. |
| A header row but no data rows | "The file has no teams." |

- The required headers are Club, Team, Division, and Gender. Headers match ignoring case and surrounding whitespace. Columns may be in any order. Other columns are ignored. If a required header appears twice, the file is rejected with "The file has more than one Club column."
- A leading UTF-8 byte order mark is ignored.
- Standard CSV quoting applies. A quoted value may contain commas, quotes written as two quotes, and line breaks.
- Every value is trimmed. A line with no values, or only empty values, is skipped and does not count as a row.
- A row with fewer values than the header treats the missing values as blank. A row with more values than the header is rejected with "This row has more values than the header."
- Line numbers in the preview are the file's line numbers, with the header as line 1.

### Checking each row

Each row is checked in this order. The first failure rejects the row with its message.

1. Club is blank: "Club is required."
2. Team is blank: "Team is required."
3. Division is blank: "Division is required."
4. Gender is not M, F, Male, or Female, ignoring case: "Gender must be M, F, Male, or Female."
5. No division in this tournament matches the division name, ignoring case and surrounding whitespace, and that gender: "Add the division \"Men's Division II (Male)\" to the tournament before importing." The message uses the row's division name and the gender it resolved to.
6. Another row in the file names the same club and team, ignoring case and surrounding whitespace: "This team is on more than one row (lines 4 and 9)." Every such row is rejected and lists every line.

A row that passes is matched against existing records:

- The club is found by name, or will be created. Rows in the same file that name the same new club, with any case, create one club. Its spelling comes from the first such row.
- The team is found by name within that club, or will be created.
- If the team is already entered in this tournament in the same division, nothing changes.
- If it is entered in a different division, the entry moves to the row's division.
- Otherwise, the team is entered in the row's division.

### The preview

Above the table, a summary gives counts, for example: "157 rows · 12 new clubs · 140 new teams · 150 to add · 3 to move · 4 unchanged · 0 rejected."

The table lists every row in file order with Line, Club, Team, Division, Gender, and Outcome. Outcome is one of:

- "Add, new club and team"
- "Add, new team"
- "Add, existing team"
- "Move from 14U Open (Female)"
- "No change"
- "Rejected: " followed by the message

Rejected rows use the error color and an error icon, so color is not the only signal. When any row is rejected, a "Rejected only" switch appears above the table and narrows it to rejected rows.

### Importing

The buttons are Cancel on the left and Import on the right. Import is disabled while there is no preview, while the file was rejected, or while any row is rejected.

Import sends the same file again. The API repeats every check against the current data and writes all rows in one transaction.

- If every row still passes, the import is written. The dialog closes, the table reloads, and the snackbar reads "Imported 157 rows." This counts every row, including rows with no change.
- If any row now fails, because data changed since the preview, nothing is written. The dialog shows the new preview with "The teams changed since the preview. Review it and import again."
- A failed call writes nothing, leaves the dialog open, and shows the failure.

Cancel, Escape, and clicking outside write nothing.

An import never removes an entry. Teams entered in the tournament but not in the file stay.

## Division delete

Deleting a division that has teams entered is rejected. The confirmation still opens first. After the director confirms, the API rejects the delete and the editor shows the warning "This division has 3 teams. Move or remove them on the Teams page first." One team reads "1 team." The division stays.

A division with no teams deletes as before.

## Tournament delete

Deleting a tournament deletes its team entries along with its divisions, venues, and courts. The confirmation body becomes "Its venues, courts, divisions, and teams are also deleted." Clubs and teams stay with the organization. If the deleted tournament was remembered by the Teams page, the page treats it as gone the next time it loads.

## Rules the API enforces

The Blazor app talks only to the .NET API. The API uses Entity Framework Core to write local Postgres. The UI and the API reject the same invalid saves.

- Add with a blank club is rejected: "Club is required."
- Add or edit with a blank team name is rejected: "Team name is required."
- Add or edit with no division, or a division from another tournament, is rejected: "Division is required."
- Add of a team already entered in this tournament is rejected: "This team is already entered in this tournament."
- Edit that renames a team to the name of another team in the same club is rejected: "Another team in this club already has this name."
- Add, edit, delete, or import for a tournament that does not exist is rejected.
- Edit or delete of an entry that does not exist, or that belongs to another tournament, is rejected.
- Import repeats every file and row check above and writes nothing unless every row passes.
- Deleting a division with entries is rejected with the message in Division delete.
- Deleting a tournament deletes its entries. It does not delete clubs or teams.
- A club, team, entry, tournament, or division that belongs to a different organization is not returned and cannot be changed.
- At the database, club names are unique per organization after trimming and ignoring case, team names are unique per club after trimming and ignoring case, and a team has at most one entry per tournament. Two saves that race to create the same club or team fail one of them rather than create a duplicate. The failed save shows its message and can be retried.

This slice does not detect simultaneous edits to the same entry. The last save wins.

## New wrappers

The look-and-feel spec adds a wrapper when a page first needs a control. This slice needs:

- An **autocomplete** wrapper for Club and Team, with the label above the control, as the other field wrappers have.
- A **file drop zone** wrapper for the import dialog.
- A **switch** wrapper for "Rejected only."

The teams table and the preview table use MudBlazor's simple table, as the tournament list does, not a data grid.

## Accessibility

- Club and Team announce the number of matches and whether the highlighted option creates a new record.
- After "Save and add another," focus moves to Team.
- The drop zone is reachable by keyboard and opens the file browser on Enter or Space.
- The preview summary is announced when the preview appears.

## Decisions this spec makes that the intent did not

The intent left these to the spec. Change any of them before a plan is written.

- **Route and icon.** The page is `/teams` with the `Groups` icon.
- **Remembered tournament.** It is stored in the browser, survives closing the browser, and is set by the picker and by opening a tournament in the editor.
- **No-divisions message.** The page links to that tournament's editor.
- **Spelling of new clubs.** The first save's spelling is kept. Later matching saves do not respell it.
- **Rename collisions.** Renaming to another team's name in the same club is rejected.
- **Already-entered teams in Add.** They are listed but cannot be picked.
- **Division preselection.** Add starts with no division. "Save and add another" keeps the last one.
- **File limits.** 2 MB, UTF-8, header on line 1.
- **Extra values in a row.** Rejected. Missing trailing values count as blank.
- **Duplicate headers.** Rejected.
- **Preview staleness.** Import repeats every check. If any row now fails, nothing is written and the new preview is shown.
- **Import count.** The snackbar counts every row in the file, including rows with no change.
- **Division delete order.** The confirmation opens first, and the API's rejection follows.
- **Tables.** MudBlazor's simple table, not a data grid.

## Deliberately not in this slice

- A tournament selector in the app bar. The picker lives on the Teams page.
- Team codes, the AES import layout, predefined-pools and ranked-teams CSV formats, format detection, and a format help dialog.
- Waves, pools, rankings, and seed hints on import.
- Withdrawal as a repair, and adding a team after pools are seeded. This slice has no pools.
- Renaming, merging, or deleting clubs, and deleting master teams.
- Players and rosters, export, and the public site.
- Sign-in and choosing an organization.
- A concurrency token. Simultaneous edits are not detected.
