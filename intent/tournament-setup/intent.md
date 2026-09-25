# Intent: Tournament setup
Author: product owner. Status: accepted.

## Problem
A director cannot record a tournament and the courts it will use without a vendor or the proof of concept.

## Proposed outcome
A director opens a list of tournaments for one local organization and can create, edit, and delete a tournament.

They save a required name, start date, end date, time zone, default match duration, and an optional description. The end date must be after the start date. If another tournament already has that name, or the tournament covers more than five calendar days, the editor warns them and still saves. Default match duration is 15 to 240 minutes. After the tournament exists, they add, edit, and delete venues. Each venue has a name, an optional address, and a court count from 0 to 99. Courts are numbered 1 through N and belong to that tournament.

Deleting a tournament deletes its venues and courts. Changing a court count rewrites that venue's courts to 1 through N.

## Affected users and systems
Directors use the Blazor WebAssembly app. The .NET API uses Entity Framework Core to persist tournaments, venues, and courts to local Postgres. The proof of concept is reference only.

## Constraints
- No sign-in. One fixed local organization, named Test Organization, owns every tournament. The director never picks an organization and never sees that name.
- Venues and courts belong to the tournament, not to a reusable gym catalog.
- No divisions, clubs, teams, CSV import, or rankings.
- No waves or match start times.
- No pools, brackets, schedule generation, or OR-Tools.
- No publish, archive, clone, or public site.
- No deployment work. Supabase is a later deployment choice. This slice uses local Postgres.
- The time zone picker lists US time zones.
- Simultaneous edits are not detected. The last save wins.

## Open questions
None.
