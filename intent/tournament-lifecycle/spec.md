# Spec: Tournament lifecycle

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack. This slice uses the .NET API, Blazor WebAssembly with MudBlazor, Entity Framework Core, and local Postgres. It does not call WorkOS, OR-Tools, or Supabase. The lifecycle rules live on the **Tournament** in the domain model. The application layer holds the use cases that load a tournament, call one transition, and save.

The three states are **Unpublished**, **Published**, and **Archived**. The first state is **Unpublished**, as the intent says.

Tournament details, venues, and delete otherwise follow [../tournament-setup/spec.md](../tournament-setup/spec.md). Divisions follow [../tournament-divisions/spec.md](../tournament-divisions/spec.md). Entries and import follow [../tournament-teams/spec.md](../tournament-teams/spec.md). The shared dropdown follows [../current-tournament/spec.md](../current-tournament/spec.md). This spec adds to each of them only what is written here.

## States and transitions

A new tournament is Unpublished. These are the only allowed changes:

| From | Action | To | Warning first |
| --- | --- | --- | --- |
| Unpublished | Publish | Published | No |
| Unpublished | Delete | (gone) | Yes |
| Published | Unpublish | Unpublished | Yes |
| Published | Archive | Archived | Yes |
| Published | Automatic archive | Archived | (no one is asked) |
| Archived | Restore | Unpublished | No |
| Archived | Delete | (gone) | Yes |

Anything else is rejected, and the tournament does not change:

- Archiving an Unpublished tournament: "Only a published tournament can be archived."
- Deleting a Published tournament: "A published tournament cannot be deleted. Unpublish or archive it first."
- Publishing a tournament that is not Unpublished: "Only an unpublished tournament can be published." An Archived tournament is restored first, then published.
- Unpublishing a tournament that is not Published: "Only a published tournament can be unpublished."
- Restoring a tournament that is not Archived: "Only an archived tournament can be restored."

Repeating an action does nothing more. Publishing a tournament that is already Published is rejected. It does not restart the clock.

## The 270-day clock

Each tournament remembers the moment it was last published, as an instant in time, not a calendar date. Every Publish sets that moment to now. Nothing else changes it.

A Published tournament becomes eligible for archiving 270 days after that moment. A day is 24 hours. Time zones and daylight saving do not change the count. A tournament published at 9:00 a.m. Eastern on March 1 is eligible 270 × 24 hours later.

CourtFlow archives each eligible Published tournament within one day of its becoming eligible. It does this on its own, on a schedule, with no director present. It checks every organization's tournaments. It emails no one and shows no message.

- Only Published tournaments are archived automatically. An Unpublished or Archived tournament is never touched.
- The check reads the tournament's state and last-published moment at the time it archives, not earlier. If a director unpublished or republished the tournament in the meantime, the check acts on the current values. A tournament republished yesterday is not archived because an older publish has expired.
- Running the check twice archives nothing twice. An already Archived tournament is left alone.
- If CourtFlow was not running when a tournament became eligible, it archives that tournament the next time the check runs. "Within one day" holds only while CourtFlow is running.

Unpublishing does not reset or remove the clock. It no longer matters, because an Unpublished tournament is never archived automatically. The next Publish sets a new moment.

Restoring does not set the clock. A restored tournament is Unpublished. The clock starts again at its next Publish.

## Tournament list

The list at `/tournaments` keeps its columns, order (start date, then name), and actions. It adds a **Status** column after Name. Every row shows a badge: "Unpublished", "Published", or "Archived". Archived tournaments stay in the list. They are not hidden, filtered out, or moved to the end.

Delete stays on every row. For a Published row it is disabled, and its tooltip reads "Unpublish or archive this tournament before deleting it." For Unpublished and Archived rows it works as before, with the stronger warning below.

## Tournament editor: status

An existing tournament's editor shows a status area directly under the page title. A new, unsaved tournament has none, because it has no state until saved. It is saved Unpublished.

The status area shows the same badge as the list and the actions for the current state:

- **Unpublished.** Text: "Only your organization can see this tournament." Action: **Publish**.
- **Published.** Text: "Published on {date}. It will be archived automatically on {date}." Actions: **Unpublish** and **Archive**.
- **Archived.** Text: "This tournament is archived. Restore it to make changes." Action: **Restore**.

Dates are shown in the tournament's own time zone, in the app's existing US date format. The archive date is the date on which the tournament becomes eligible. Archiving may happen up to one day later.

After any action, the editor reloads the tournament and shows a short confirmation: "Published.", "Unpublished.", "Archived.", or "Restored."

## Warnings

Each warning uses the app's existing confirmation dialog. Cancel, Escape, and clicking outside change nothing.

- **Unpublish.** Title: the tournament's name. Body: "Coaches, players, and spectators will no longer see this tournament. Links you have shared will stop working." Buttons: "Unpublish" and "Cancel."
- **Archive.** Title: the tournament's name. Body: "Archived tournaments are hidden from the public and cannot be changed. You can restore it later." Buttons: "Archive" and "Cancel."
- **Delete.** The existing delete confirmation, with its body changed to "Its venues, courts, divisions, and teams are also deleted. This cannot be undone." Buttons: "Delete" and "Cancel."

Publish and Restore have no warning.

## Archived tournaments cannot be changed

While a tournament is Archived, nothing that belongs to it can be added, changed, or removed. The API rejects each of these with "This tournament is archived. Restore it to make changes.":

- Saving tournament details.
- Adding, editing, or deleting a venue, which includes changing its court count.
- Adding, editing, or deleting a division.
- Adding, editing, or deleting a team entry, including moving an entry to another division.
- Previewing or running a CSV team import into that tournament.

Only Restore and Delete work on an Archived tournament.

The screens match:

- **Editor.** Detail fields are read-only and **Save details** is hidden. Venues and divisions are listed, but their Add, Edit, and Delete buttons are hidden.
- **Teams page.** With an Archived tournament current, the teams table is shown. Add and Import CSV are disabled. The Edit and Delete buttons on each row are hidden. A note above the table reads "This tournament is archived. Restore it to make changes."

Clubs and teams belong to the organization, not to the tournament. Renaming a team through another tournament's Teams page still renames it everywhere, including where it appears in an Archived tournament. That is not a change to the Archived tournament.

Published and Unpublished tournaments stay editable exactly as they are today.

## Shared tournament dropdown

The dropdown keeps listing every tournament in list order, including Archived ones. An Archived tournament's option reads "{name} (Archived)". Choosing one makes it current, as with any other tournament.

## Public visibility

Only Published tournaments are for the public. Unpublished and Archived tournaments are never shown to the public, and a link to one must behave as if the tournament does not exist.

No public tournament page exists yet. The landing page still shows only "Welcome" and "Coming soon." So in this slice the rule is a property of each tournament that the future public site must honor. This slice adds no public page and no public API.

Publishing a tournament does not publish a schedule. No schedules exist yet. When they do, a Published tournament's schedule stays private until it is published on its own.

## Rules the API enforces

- The Blazor app and the API reject the same invalid actions. The API is the authority. A screen that is out of date, for example because the tournament was archived automatically while the editor was open, gets the API's rejection message and reloads the tournament.
- Every tournament the API returns, in the list and on its own, includes its state. A Published tournament also includes its last-published moment.
- Publish, Unpublish, Archive, and Restore are separate actions. Saving tournament details never changes the state.
- Creating a tournament always makes it Unpublished. A create request cannot choose a state.
- A tournament that belongs to another organization is not returned and cannot be changed. The automatic archive works across organizations, but each archive changes only that tournament.

## Data

The database is wiped for this change. No existing tournaments are carried over, so the new state and last-published moment need no backfill.

## How this is checked

- A new tournament is Unpublished, and the list shows its "Unpublished" badge.
- Publish moves it to Published and records the moment. The editor shows the published date and the archive date 270 days later.
- Unpublish asks first. Cancel leaves it Published. Confirming makes it Unpublished.
- Archive asks first. Confirming makes it Archived. Archiving an Unpublished tournament is rejected by the API.
- Restore makes an Archived tournament Unpublished. Publishing it again records a new moment.
- Delete is disabled for a Published row, and the API rejects deleting a Published tournament. Deleting an Unpublished or Archived tournament asks first, says it cannot be undone, and removes it.
- With an Archived tournament, the API rejects every change listed above, the editor is read-only, and the Teams page shows the note and disables Add and Import CSV.
- The automatic archive, run with a clock set 270 days past a Published tournament's last publish, archives it. Run at 269 days, it does not. It leaves Unpublished and Archived tournaments alone. It uses the latest publish when the tournament was republished. Running it twice does no harm.
- The dropdown labels an Archived tournament "{name} (Archived)".

## Decisions this spec makes that the intent did not

The intent left these open, or did not mention them. Change any of them before a plan is written.

- **Badges for every state.** The intent asked for an "Archived" badge. The list shows a badge for all three states.
- **No warning on Publish or Restore.** The intent asked for warnings on Unpublish and Delete only. Archive by hand also warns, because it locks the tournament.
- **Archive date shown.** The editor shows when a Published tournament will be archived. It is on screen, not a notification, so it does not conflict with "no one is notified."
- **Re-publishing is rejected.** A Published tournament cannot be published again to push the clock out. To extend it, the director unpublishes and publishes.
- **Archived tournaments keep their place.** They stay in normal list order and in the dropdown, and are not grouped at the end.
- **Read-only screens.** Archived tournaments hide edit buttons instead of showing them disabled, except Add and Import CSV on Teams, which stay disabled as they already are when nothing is selected.
- **Import preview is blocked too.** Previewing an import into an Archived tournament is rejected, even though preview writes nothing.
- **Wording.** All on-screen copy above is this spec's choice.

## Flagged for the owner

- **Viewers and archived tournaments.** The accepted [sign-in and access intent](../sign-in-and-access/intent.md) says viewers see published tournaments and not unpublished ones. It does not say whether viewers see archived tournaments. That slice's spec should decide.
- **Deactivated organizations.** That same intent removes a deactivated organization's published tournaments from the public site and puts them back on reactivation. This spec keeps archiving them automatically on schedule while the organization is deactivated, so a tournament may come back already Archived. That slice's spec should confirm or change this.
- **"Within one day" while down.** CourtFlow can keep the one-day promise only while it is running. After an outage, overdue tournaments are archived on the next check.

## Deliberately not in this slice

- A public tournament page, a public link, or a public API.
- Schedules and schedule publishing.
- Emailing or notifying anyone about publishing or archiving.
- A per-organization archive period. It is 270 days for everyone.
- Restoring straight to Published.
- Recovering a deleted tournament.
- Sign-in, roles, and who may publish. Until sign-in exists, anyone who can reach the director app can use every action.
