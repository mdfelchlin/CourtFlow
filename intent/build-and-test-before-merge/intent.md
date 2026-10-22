# Intent: Build and test before merge
Author: developer. Status: accepted.

## Problem
A developer can build CourtFlow and run its tests only on their own machine. Nothing in the repository builds the project and runs the tests before a change merges to main. The engineering-practices slice named the missing continuous integration and left the workflow out. There is still no pipeline.

## Proposed outcome
A pull request into main runs a CI pipeline that builds the project and runs all of the tests. A failing pipeline blocks the merge.

## Affected users and systems
Developers merging work to main. This touches how the product tree under `src/` is built and tested. Directors, coaches, players, and spectators see no change.

There is no workflow in the repository today. Tests live in `CourtFlow.Domain.Tests`, `CourtFlow.Application.Tests`, `CourtFlow.Api.Tests`, `CourtFlow.Web.Tests`, and `CourtFlow.AppHost.Tests`. A developer runs them with `dotnet test src/CourtFlow.slnx`. API tests start Postgres 16 through Testcontainers and need a container runtime. The tests do not call WorkOS, Resend, or Turnstile.

## Constraints
- Director-facing behavior, API contracts, and behavior an accepted intent already specifies stay as they are.
- The pipeline builds and tests the existing product tree. It does not change how the app starts or runs.
- A pull request into main owns the pipeline. A failing result blocks the merge to main.
- The pipeline runs every test project. It does not split tests into unit and integration.
- The pipeline provides a container runtime so the API tests can start Postgres. No secrets are required.

## Open questions
None.
