# Intent: Simple local startup
Author: developer. Status: accepted.

## Problem
A developer cannot start the projects needed for local work in one place. Postgres starts from Docker Compose, and the API and the Blazor app each start on their own. Nothing shows those running pieces together, so startup is more steps than it needs to be and status is hard to see at a glance.

## Proposed outcome
A developer starts local CourtFlow with one .NET Aspire command. That start brings up only the API, the Blazor app, and Postgres. The Aspire dashboard shows those resources, their status, logs, and endpoints. The API and the Blazor app keep their current local URLs.

## Affected users and systems
Developers running CourtFlow locally. This touches how the .NET API, the Blazor WebAssembly app, and local Postgres are started together. Directors, coaches, players, and spectators see no change.

## Constraints
- .NET Aspire is the only way these projects start. Aspire owns startup for the API, the Blazor app, and Postgres.
- The API and the Blazor app keep their current local URLs.
- Local data stays on Postgres. Hosted Postgres is out of scope.
- Tournament setup behavior does not change.

## Open questions
None.
