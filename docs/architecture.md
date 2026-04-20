# Architecture Notes

CourtFlow V1 follows a Clean Architecture layout:

- `Domain` - pure entities, value objects, enums, domain events. No framework dependencies.
- `Application` - use-case orchestration via MediatR command/query handlers, DTOs, FluentValidation rules, and abstractions (e.g. `IAppDbContext`, `ITenantContext`, `IClock`, scheduling/bracket/standings interfaces).
- `Infrastructure` - EF Core DbContext, migrations, Identity, and concrete engines (`IScheduleGenerator`, `IConstraintChecker`, `IBracketBuilder`, `IBracketProgressionService`, `IStandingsCalculator`, `ICourtStatusService`).
- `Api` - ASP.NET Core host, JWT bearer auth, OpenAPI, controllers wiring MediatR.
- `ApiClient` - typed HTTP client (generated via NSwag) used by Blazor.
- `Web.Shared` - RCL with all reusable Razor components, layouts, and view models. Host-agnostic so a future MAUI Blazor Hybrid app can reuse it.
- `Web` - Blazor Web App (Server interactive) host; cookie auth; exchanges the user's login for a JWT and forwards it to the API.

## Multi-tenancy

Every aggregate root carries `OrganizationId`. An `ITenantContext` is resolved per-request from the JWT `org_id` claim. EF Core global query filters apply the tenant filter everywhere so cross-tenant reads are impossible by default.

## Conflict & scheduling correctness

Ref/team/court conflict checks run as SQL queries against the full schedule (not in-memory), so they cannot miss conflicts outside the currently loaded view (PRD 5.4).

## Naming

Tables and columns use snake_case via `EFCore.NamingConventions` (e.g. `tournaments.scheduled_start_utc`).
