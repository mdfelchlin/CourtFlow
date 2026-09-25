# Intent: CourtFlow
Author: product owner. Status: accepted.

Implementation status: not started. The previous app under [`poc/`](../../poc/) is evidence of behavior, not the build target.

## Problem
Volleyball tournament directors need fast, safe, self-service control over schedules, brackets, and courts. Existing tools are rigid at the moment a director most needs flexibility:

- Moving a match, delaying a court, or fixing a bracket pairing takes a vendor or a destructive edit with no audit trail.
- Team, ref, and court collisions stay silent because checks run against whatever is loaded on screen, not the full schedule.
- Withdrawals, court outages, ref reassignments, and schedule reflow happen as off-record edits that corrupt history.
- Directors cannot see which courts are behind, or by how much, without walking the gym.
- Long-lived club and team identity gets mixed up with a team's entry in one tournament, which makes repairs dangerous.

## Proposed outcome
A director can set up a tournament, run it, repair it when reality changes, and publish what coaches and spectators are allowed to see, without emailing a vendor.

- Repairs (withdrawals, outages, reassignments, reflow) are recorded. History is not silently overwritten.
- Conflict checks consider the full schedule, not the current screen.
- Generating, previewing, and publishing a schedule are explicit steps. A reflow does not destroy earlier versions.
- Court status shows, at a glance, which courts are behind or ahead: on the floor, at the scorer's table, and on a wall monitor.
- Public visitors can read a landing page and a per-event view (teams, schedule, brackets, standings). The organizer controls what is visible. Visitors cannot change anything.

## Affected users and systems
- Tournament directors and organizers. They are the only writers. They set up tournaments, divisions, pools, and brackets; import and rank teams; assign waves; generate and edit schedules; and watch which courts are behind.
- Coaches, players, and spectators. They browse current and upcoming events and open a published event to see the schedule, brackets, and standings. They have no account.
- Organizations. Each organization's tournaments, clubs, and teams stay separate from every other organization's.
- The proof of concept in `poc/`, including [`poc/docs/`](../../poc/docs/) and [`poc/CONTEXT.md`](../../poc/CONTEXT.md). Use it to see how the product behaved. Do not treat its stack or schema as a requirement.

## Constraints
In scope for the first version:

- Volleyball only.
- Many organizations, each with its own tournaments.
- Venues and courts linked to a tournament.
- Divisions, pools, and AM/PM waves as a scheduling constraint.
- Team CSV import in the predefined-pools and ranked-teams shapes.
- Schedule generation, conflict checking, and per-match move or delay.
- Bracket generation: single and double elimination, with an optional if-necessary final.
- Set-level match scoring, standings, and bracket progression.
- Admin-only writes. Public reads are anonymous.
- Public landing page and per-event read-only views.
- Core admin pages (tournament dashboard, schedule, bracket, court status) load in under 2 seconds for a typical event, up to about 150 teams and 30 courts.
- Several tournaments can run at once inside one organization, and one deployment can hold dozens of organizations.
- Availability target is 99.9% for the API and the web app, excluding planned maintenance.
- Each organization's data is isolated on the server. Traffic is HTTPS.
- Schedule generation, bracket changes, conflict-check failures, and delay calculations are logged in a way an operator can search. Request, error, and slow-query metrics exist.
- Idle time should stay cheap. The proof of concept did this with a scale-to-zero API, static UI hosting, and a low-baseline database. That hosting shape is evidence, not a requirement.

Out of scope for the first version:

- Team registration and payments.
- External roster or league integrations.
- Per-court billing, a marketplace, or payouts.
- Sports other than volleyball.
- A full AES or SportsWrench import, or a two-way integration with those tools.
- Realtime push. Refresh is enough.

A director should be able to:

- Set up a typical tournament (80 teams, 12 courts, AM/PM waves) in under 60 minutes without vendor help, or in under 10 minutes by cloning a similar prior tournament.
- Move a match to a new court or time, or adjust a bracket pairing, in under 30 seconds.
- Reflow a day when one court runs long (for example Court 3 is 20 minutes over) in under 60 seconds, including a preview of the diff before it is applied.
- Trust that built-in tools never silently allow a scheduling or ref conflict. Conflicts show up as warnings or hard blocks.
- See behind-schedule courts on a court-status view and intervene before the delay spreads.
- Let a coach or parent answer "where am I playing next?" from the public event page with a my-team filter, without scrolling the full draw.

Later, and not part of this intent's build:

- Public schedule, pool, and bracket views beyond the read-only event tabs.
- Native scoring clients, including offline scoring and per-court device login.
- Team registration and payment.
- Deep roster or league integrations.
- Analytics such as court-utilization and revenue dashboards.

## Open questions
The first spec decides these. This intent does not.

- Which parts of the proof-of-concept stack to keep: Blazor, ASP.NET, Clerk, Turso, and the current UI kit.
- Whether print outputs (pool sheets, score sheets, brackets, court runs, work rotations, standings pages) are in the first rebuild.
- Whether saving a tournament as a template and creating a new tournament from that template are in the first rebuild.
- How much of the repair-job experience is a recorded history versus a polished list and detail workflow.
- Whether the domain language in `poc/CONTEXT.md` carries forward as written or is revised in the spec.
