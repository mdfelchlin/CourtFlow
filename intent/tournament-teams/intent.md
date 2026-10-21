# Intent: Tournament teams
Author: product owner. Status: accepted.

## Problem
A director has created a tournament and its divisions, but cannot bring teams into those divisions. Teams arrive one at a time from coaches and in bulk as spreadsheets. Without teams there is nothing to schedule.

## Proposed outcome
A Teams item in the navigation drawer opens a Teams page. The director picks a tournament and sees its teams. They add teams quickly through a modal with "Save and add another," edit or delete a team, or import a CSV, preview every row, and import the whole file in one step.

Each team entered in a tournament has a club, a team name, and a division. The team's gender is its division's gender.

## Affected users and systems
Directors use the Blazor WebAssembly app. The .NET API persists the result with Entity Framework Core to local Postgres. Coaches, players, and spectators do not do this. The tournament editor's division delete changes.

## Constraints

**Clubs and teams**
- A club belongs to the organization and is reused across tournaments. Clubs are matched by name, ignoring case and surrounding spaces. A club is created the first time a team names it. This slice has no page for renaming or merging clubs.
- A team belongs to one club and is reused across tournaments. Within a club, a team is found by its name, ignoring case and surrounding spaces. Two teams in the same club cannot share a name. Two clubs may each have a team with the same name.
- A team never moves to a different club. Its club is fixed when it is created.
- A team is entered in a tournament separately, once, in one division of that tournament.
- A team's name belongs to the team itself. Renaming it renames it in every tournament it is entered in.
- Teams have no team code.

**Teams page**
- Teams is an item in the navigation drawer, alongside Tournaments. The page has a tournament picker at the top. It starts on the tournament the director last opened or picked. With none, nothing is selected and the page asks the director to select a tournament.
- If the selected tournament has no divisions, the page says to add a division on the tournament first. Add and Import CSV are unavailable.
- The page shows one table with Club, Team, Division, and Edit and Delete actions on each row. Rows are ordered by division, in the Divisions list order, then by team name. A division filter ("All divisions" by default) and a search over team and club sit above it, with a count such as "50 of 157 teams." With no teams, the page says to use Add or Import CSV.

**Add**
- Add opens a modal with Club, Team, and Division.
- Club is a search box over the organization's clubs. Typing a name that does not exist creates that club.
- Team is enabled once a club is chosen. It lists that club's teams and creates a new team when the typed name does not exist. A team already entered in this tournament cannot be picked again.
- Division lists this tournament's divisions with their gender, such as "14U Open (Female)."
- "Save and add another" saves, keeps the club and division, clears the team, and is ready for the next team. Save saves and closes. Cancel closes without saving the current form. Earlier saves stay.

**Edit and delete**
- Edit opens a modal. The team name and the division can change. The club is shown and cannot change.
- To change a team's club, the director deletes it and adds the right team.
- Delete asks the director to confirm, then removes that team from this tournament. The team and its club stay for other tournaments. Withdrawal as a recorded repair is a later slice.

**CSV import**
- The file's header row must contain Club, Team, Division, and Gender. Headers match ignoring case. Columns may be in any order. Extra columns are ignored. A missing required column rejects the file and names the column.
- Values are trimmed. Blank lines are skipped. Standard CSV quoting is supported. The file is UTF-8, including Excel's "CSV UTF-8."
- Division and Gender together find the division in the selected tournament, because two divisions may share a name across genders. Division names match ignoring case and surrounding spaces. Gender accepts M, F, Male, or Female in any case.
- A row whose division does not exist in the tournament is rejected. The message names the division and tells the director to add it to the tournament before importing.
- Import CSV opens a modal. After the director picks a file, it shows a preview before anything is written. Each row shows its outcome: new club, new team, existing team, already entered, moved to another division, or rejected with the reason.
- Import is disabled while any row is rejected. The director fixes the file and uploads it again. Import writes the whole file in one transaction.
- A team already entered in the same division is left as is. A team entered in a different division is moved to the row's division. Teams entered in the tournament but missing from the file are kept. An import never removes a team. Two rows naming the same team reject both rows.
- The modal offers a template file with just the header row.

**Divisions and tournaments**
- A division that has teams entered cannot be deleted. The director is told how many teams it has and to move or remove them on the Teams page first.
- Deleting a tournament removes its team entries along with its divisions, venues, and courts. Clubs and teams stay with the organization.

**Out of scope**
- Team codes, the AES import layout, and any import layout other than the one above.
- Players and rosters, rankings and seeding, waves, pools, brackets, schedule generation, withdrawals as repairs, and the public site.
- A club management page, a team history page, and export.
- One fixed local organization owns the data. There is no sign-in and no organization picker.

## Open questions
None.
