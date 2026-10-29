# Intent: Club registration
Author: product owner. Status: draft.

## Problem
A club cannot create an account, cannot register its own teams for a tournament, and cannot manage the roster it wants to submit with that registration. Today a director types each team in (club, team name, and division) or imports a spreadsheet. Coaches have no account. There is no roster of players to submit.

A director cannot set when registration for a tournament opens or closes, and a division has no cap on teams. Nothing keeps a team from registering after the window has shut, or into a division that is already full.

## Proposed outcome
Someone acting for a club creates an account, registers that club's teams for a tournament, and manages the roster they want to submit to that tournament.

The director sets, on the tournament, when registration opens and when it closes. Each division has a cap of teams. A team can register for a division only when a spot is available: registration for that tournament is open, and the division is still under its cap.

Registration follows the patterns of SportsWrench, the service now called OTTO Event. On that service the event has an open and a close, each division has a maximum number of teams, and a club director enters the event and assigns the club's teams to divisions.

## Affected users and systems
Club contacts, who cannot do this today. Directors, whose tournaments currently receive teams only when the director adds or imports them, and who have no registration window or division cap to set. The accepted product intent, which says directors and organizers are the only writers and coaches, players, and spectators have no account. The existing club and team records, which a director creates by name and which persist across tournaments inside one organization. Sign-in, which is invite-only for directors and organizers. The tournament, which already has a name, dates, and a time zone, and has no registration open or close. Divisions, which were left without a max-teams count for a later slice.

## Constraints
- A club is not an organization. An organization is the tenant that runs tournaments. A club is an affiliation that owns teams.
- A team is the long-lived record owned by a club. Entering that team in one division of one tournament is separate from the team itself.
- The registration open and the registration close are set on the tournament. The team cap is set on the division.
- A team registers only when registration is open and the division still has a spot under its cap.

## Open questions
- One sign-in for the same person, who may run tournaments, act for a club, or both, versus two separate sign-ins.
- How a club account is created, and how it relates to the existing invite-only director sign-in.
- Whether the director accepts an entry after the club registers. SportsWrench keeps Registered and Accepted as separate counts, and entering a team does not by itself accept it.
- What "roster" is: players kept on the team, or the players submitted for one tournament, and what the club records about each player. SportsWrench publishes a roster deadline separate from the registration close.
- How a club's teams relate to teams a director has already entered by typing a club name.
- Whether teams the director types in or imports count toward the division cap, and whether the director can still add a team after registration has closed or the division is full.
- SportsWrench places further teams on a waitlist once a division reaches its max, and the number registered can pass that max. This request is that a team registers only when a spot is available. Whether that waitlist replaces the hard stop is still open.
- Whether the open and the close are a calendar date or a date and time. SportsWrench publishes both a date and a time. The tournament already has a time zone.
- Which further SportsWrench registration patterns belong in this slice: an entry fee and payment before a team counts, paying for every team from the club in one step, a public note that registration is open, and a limit on how many teams from one club a division will take.
