# Intent: Club registration
Author: product owner. Status: draft.

## Problem
A club cannot create an account, cannot register its own teams for a tournament, and cannot manage the roster it wants to submit with that registration. Today a director types each team in (club, team name, and division) or imports a spreadsheet. Coaches have no account. There is no roster of players to submit.

A director cannot set when registration for a tournament opens or closes, and a division has no cap on teams. Nothing keeps a team from registering after the window has shut, or from taking a spot in a division that is already full. Registering a team is the same as the team being in the tournament. The director cannot approve it separately, and a full division has no waitlist. The tournament does not list a roster deadline, an entry fee, or how much the tournament costs.

## Proposed outcome
Someone acting for a club creates an account, registers that club's teams for a tournament, and manages the roster they want to submit to that tournament.

The director sets, on the tournament, when registration opens and when it closes, a roster deadline, an entry fee, and how much the tournament costs. For now the tournament lists the roster deadline, the entry fee, and the cost. Each division has a cap of teams. While registration is open, a club can register a team for a division. While the division is under its cap, that registration holds a spot and waits for the director. Once the division is at its cap, further registrations go on a waitlist, in the order they registered. A waitlisted team does not hold a spot, so the number of teams registered can be higher than the cap.

Registered and accepted stay separate, as they do on SportsWrench. The director approves a team before it is accepted into the tournament. A waitlisted team stays registered until the director approves it into an open spot.

Registration follows the patterns of SportsWrench, the service now called OTTO Event. On that service the event has an open and a close, a roster deadline, and a listed registration fee, each division has a maximum number of teams, registered and accepted are separate counts, a full division waitlists later teams, and a club director enters the event and assigns the club's teams to divisions.

## Affected users and systems
Club contacts, who cannot do this today. Directors, whose tournaments currently receive teams only when the director adds or imports them, and who have no registration window or division cap to set. The accepted product intent, which says directors and organizers are the only writers and coaches, players, and spectators have no account. The existing club and team records, which a director creates by name and which persist across tournaments inside one organization. Sign-in, which is invite-only for directors and organizers. The tournament, which already has a name, dates, and a time zone, and has no registration open or close, roster deadline, entry fee, or listed cost. Divisions, which were left without a max-teams count for a later slice.

## Constraints
- A club is not an organization. An organization is the tenant that runs tournaments. A club is an affiliation that owns teams.
- A team is the long-lived record owned by a club. Entering that team in one division of one tournament is separate from the team itself.
- The registration open and the registration close are set on the tournament. The team cap is set on the division.
- The tournament lists a roster deadline, an entry fee, and how much the tournament costs. For now those are listed. The fee is not collected.
- A team can register while registration is open.
- A registration holds a spot until the division reaches its cap. Registrations after that go on the waitlist, in the order they arrived, and do not hold a spot.
- Registered and accepted are separate. The director approves a team before it is accepted into the tournament. Approval of a waitlisted team takes an open spot.

## Open questions
- One sign-in for the same person, who may run tournaments, act for a club, or both, versus two separate sign-ins.
- How a club account is created, and how it relates to the existing invite-only director sign-in.
- What "roster" is: players kept on the team, or the players submitted for one tournament, and what the club records about each player. The roster deadline is listed. What happens when that deadline passes is still open.
- How a club's teams relate to teams a director has already entered by typing a club name.
- Whether teams the director types in or imports count toward the division cap, and whether the director can still add a team after registration has closed or the division is full.
- What frees a spot so the director can approve a waitlisted team: declining a registration, removing an accepted team, raising the cap, or some of these.
- Whether the open, the close, and the roster deadline are a calendar date or a date and time. SportsWrench publishes both a date and a time. The tournament already has a time zone.
- Whether the entry fee and how much the tournament costs are the same amount or two amounts. SportsWrench lists a registration fee per team.
- Which further SportsWrench registration patterns belong in this slice: collecting the entry fee, payment before a team counts, paying for every team from the club in one step, a public note that registration is open, and a limit on how many teams from one club a division will take.
