# Intent: Tournament lifecycle
Author: product owner. Status: accepted.

## Problem
A tournament has no lifecycle today. A director cannot say whether a tournament is still being prepared, is open to the public, or is over. So there is no way to keep a tournament hidden while it is being set up, to show it to coaches and spectators once it is ready, or to retire it once it is no longer current. Old tournaments would stay in front of the public indefinitely.

## Proposed outcome
Every tournament is in one of three states:

1. **Unpublished.** The tournament is being prepared. Only directors and organizers can see it.
2. **Published.** The director has published the tournament to the public. Coaches, players, and spectators can read it.
3. **Archived.** The tournament is no longer current. In practice it belongs to a previous season. The public sees nothing of it.

A new tournament starts unpublished. A director publishes it when it is ready for the public. A director can unpublish it again, after a warning. Only a published tournament can be archived. A director can archive a published tournament by hand at any time, and can restore an archived tournament. Restoring returns it to unpublished, so the director can check it before publishing it again.

A director can delete an unpublished or archived tournament. A published tournament cannot be deleted. Deleting shows a warning first and is permanent. An unpublished tournament that is no longer wanted is deleted rather than archived.

CourtFlow archives a published tournament automatically 270 days after it was last published to the public. No one has to remember to do it, and no one is notified when it happens. Every publish restarts the 270-day clock.

Archived tournaments stay in the director's tournament list, marked with an "Archived" badge. A director cannot edit an archived tournament.

Publishing a tournament does not publish its schedule. Schedule publishing stays its own step.

## Affected users and systems
Directors and organizers publish, unpublish, archive, restore, and delete tournaments and see each tournament's state. Coaches, players, and spectators see only published tournaments. This touches the tournament in the domain model, the use cases and API that change its state, the director's tournament list and tournament pages in the Blazor app, and the database. Automatic archiving needs something that runs on a schedule without a director present. The public site will rely on these states once it shows tournaments. The database is wiped for this change, so existing tournaments need no migration.

## Constraints
- A tournament has exactly three states: unpublished, published, and archived.
- A new tournament starts unpublished.
- A director can publish, unpublish, archive by hand, restore, and delete.
- Unpublishing shows a warning first.
- Only a published tournament can be archived. An unpublished tournament cannot be archived.
- Restoring an archived tournament returns it to unpublished.
- Only an unpublished or archived tournament can be deleted. A published tournament cannot.
- Deleting shows a warning first and is permanent.
- A published tournament is archived automatically 270 days after it was last published.
- The automatic archive happens within one day after the 270 days are up.
- Every publish restarts the 270-day clock.
- The 270 days is the same for every organization.
- No one is notified when a tournament is archived automatically.
- The public sees nothing of an archived tournament.
- Archived tournaments appear in the director's tournament list with an "Archived" badge. They are not hidden.
- A director cannot edit an archived tournament.
- Publishing a tournament does not publish its schedule.
- Only directors and organizers change a tournament's state. Public visitors cannot.
- Each organization's tournaments stay separate. Publishing, archiving, or deleting one organization's tournament never affects another's.
- The database is wiped for this change. Existing tournaments are not migrated.

## Open questions
None.
