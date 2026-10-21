# Spec: Tournament setup

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack. This slice uses the .NET API, Blazor WebAssembly with MudBlazor, Entity Framework Core, and local Postgres. It does not call WorkOS, OR-Tools, or Supabase. Supabase is the hosted Postgres when deployment is in scope.

**Tournament** is one competitive event. **Organization** is the boundary that owns tournaments. A **venue** is a place where that tournament is played. A **court** is one numbered playing surface at that venue. This slice does not use Event, Club, Team, Division, or Wave.

## What the director can do

The director is not signed in and never chooses an organization. The app already has one organization, named Test Organization. Every tournament belongs to it. Its name is not shown.

The app opens on a list of that organization's tournaments. The list shows each tournament's name, start date, end date, time zone, and default match duration. From the list the director can add a tournament, open one to edit it, or delete one.

An empty list tells the director to add a tournament.

The editor is the same screen for create and edit.

- Before the tournament has been saved, the director can enter details only. Venues are unavailable, with a note that details must be saved first.
- After the tournament exists, the director can edit details and add, edit, and delete venues.

Saving details on create opens the editor for the new tournament so venues can be added. Saving details on edit keeps the director on that tournament.

If the director opens a tournament that does not exist, the editor shows that it was not found.

## Tournament details

The director saves:

- **Name.** Required. Blank or whitespace-only is rejected.
- **Description.** Optional. Empty is allowed.
- **Start date** and **end date.** Both required. They are calendar dates, not times of day. The end date must be after the start date. A same-day tournament is rejected. When the inclusive span is more than five calendar days, the editor warns the director and still saves. September 1 through September 5 is five days and does not warn. September 1 through September 6 does.
- **Time zone.** Required. The picker offers only these US time zones, including Arizona because it does not follow the rest of Mountain time:

  - Eastern (`America/New_York`)
  - Central (`America/Chicago`)
  - Mountain (`America/Denver`)
  - Arizona (`America/Phoenix`)
  - Pacific (`America/Los_Angeles`)
  - Alaska (`America/Anchorage`)
  - Hawaii (`Pacific/Honolulu`)

- **Default match duration.** Required whole minutes, from 15 through 240.

Two tournaments may share a name. The name is not required to be unique. When the director saves a name that matches another tournament, ignoring case and surrounding whitespace, the editor shows a warning and still saves. Editing a tournament does not warn about its own name.

## Venues and courts

A venue belongs to one tournament. It is not a gym catalog the next tournament can reuse. Deleting the tournament deletes its venues and their courts.

Each venue has:

- **Name.** Required. Blank or whitespace-only is rejected. Two venues in the same tournament may share a name.
- **Address.** Optional single line of text. Empty is allowed. This slice does not look up addresses.
- **Court count.** Required whole number from 0 through 99. After save, that venue has courts numbered `1` through `N` and no others. The number is the court's name and its order.

Changing the count rewrites that set:

- Raising the count adds the missing higher numbers.
- Lowering the count deletes courts whose numbers are above the new count.
- The numbers that remain are the same courts as before.

A count of `0` leaves the venue with no courts. A count above `99` is rejected.

## Delete

Deleting a tournament asks the director to confirm, then deletes that tournament, its venues, and its courts.

Deleting a venue asks the director to confirm, then deletes that venue and its courts. The tournament remains.

Canceling a confirmation deletes nothing.

## Rules the API enforces

The Blazor app talks only to the .NET API. The API uses Entity Framework Core to write local Postgres. The UI and the API reject the same invalid saves.

- A tournament or venue with a blank name is rejected.
- A tournament name that matches another tournament is not rejected. The warning is only in the editor.
- A tournament missing a start date, end date, time zone, or default match duration is rejected.
- An end date on or before the start date is rejected.
- A span of more than five calendar days is not rejected. The warning is only in the editor.
- A default match duration outside 15 through 240 whole minutes is rejected.
- A time zone outside the list above is rejected.
- A venue missing a court count, or with a count outside the whole numbers 0 through 99, is rejected.
- A venue cannot be added, edited, or deleted for a tournament that does not exist.
- A tournament, venue, or court that belongs to a different organization is not returned and cannot be changed. With one organization, this means the API never reads or writes another organization's rows.

This slice does not detect simultaneous edits. The last save wins.

## Deliberately not in this slice

- Sign-in, Clerk, and choosing an organization.
- Publish, unpublish, archive, un-archive, and a public event link.
- Clone, and venues reused across tournaments.
- Waves, session mode, and match start times.
- Divisions, clubs, teams, rankings, and CSV import.
- Google Places address lookup.
- Blocking a court-count reduction because a court has matches. This slice has no matches.
- A concurrency token. Simultaneous edits are not detected.
