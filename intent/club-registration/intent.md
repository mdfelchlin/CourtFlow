# Intent: Club registration
Author: product owner. Status: draft.

## Problem
A club cannot create an account, cannot register its own teams for a tournament, and cannot manage the roster it wants to submit with that registration. Today a director types each team in (club, team name, and division) or imports a spreadsheet. Coaches have no account. There is no roster of players to submit.

## Proposed outcome
Someone acting for a club creates an account, registers that club's teams for a tournament, and manages the roster they want to submit to that tournament.

## Affected users and systems
Club contacts, who cannot do this today. Directors, whose tournaments currently receive teams only when the director adds or imports them. The accepted product intent, which says directors and organizers are the only writers and coaches, players, and spectators have no account. The existing club and team records, which a director creates by name and which persist across tournaments inside one organization. Sign-in, which is invite-only for directors and organizers.

## Constraints
- A club is not an organization. An organization is the tenant that runs tournaments. A club is an affiliation that owns teams.
- A team is the long-lived record owned by a club. Entering that team in one division of one tournament is separate from the team itself.

## Open questions
- One sign-in for the same person, who may run tournaments, act for a club, or both, versus two separate sign-ins.
- How a club account is created, and how it relates to the existing invite-only director sign-in.
- Which tournaments a club may register for, and whether the director accepts the entry.
- What "roster" is: players kept on the team, or the players submitted for one tournament, and what the club records about each player.
- How a club's teams relate to teams a director has already entered by typing a club name.
