# Intent: CourtFlow
Author: product owner. Status: draft.

## What this is
CourtFlow is a volleyball tournament application. A director sets up a tournament, runs it, repairs it when the day changes, and publishes what coaches and spectators may see, without emailing a vendor.

## Who it is for
Directors and organizers are the only writers. Coaches, players, and spectators read a landing page and a published event and have no account. Each organization's tournaments, clubs, and teams stay separate from every other organization's.

## Tech stack
- .NET API
- Blazor WebAssembly with MudBlazor
- Google OR-Tools for scheduling
- WorkOS for auth
- Supabase (Postgres) for data

## Pillars
- Repairs (withdrawals, outages, reassignments, reflow) are recorded. History is not silently overwritten.
- Conflict checks consider the full schedule, not the current screen.
- Generating, previewing, and publishing a schedule are explicit steps. A reflow does not destroy earlier versions.
- Court status shows, at a glance, which courts are behind or ahead.
- Public visitors can read what the organizer has published. They cannot change anything.
