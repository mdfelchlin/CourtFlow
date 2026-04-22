# CourtFlow

Volleyball tournament management platform. V1 is admin-only: an ASP.NET Core Web API plus a Blazor Web App (Server) for tournament directors to configure events, generate schedules, manage brackets, monitor courts, and record scores.

See the PRD: [Volleyball Tournament Management API + Blazor Web App PRD (V1, Admin-Only).md](Volleyball%20Tournament%20Management%20API%20+%20Blazor%20Web%20App%20PRD%20%28V1%2C%20Admin-Only%29.md).

## Solution Layout

- `src/CourtFlow.Domain` - entities, value objects, enums, domain events.
- `src/CourtFlow.Application` - CQRS handlers (MediatR), DTOs, validators (FluentValidation), abstractions.
- `src/CourtFlow.Infrastructure` - EF Core (SQLite/libSQL target), migrations, Identity, engines, seed.
- `src/CourtFlow.Api` - ASP.NET Core Web API host, JWT auth, OpenAPI.
- `src/CourtFlow.ApiClient` - typed HTTP client used by the Blazor app.
- `src/CourtFlow.Web.Shared` - Razor Class Library with reusable UI components.
- `src/CourtFlow.Web` - Blazor Web App host (Server interactive).

## Prerequisites

- .NET 10 SDK
- Docker (optional, not required for local DB by default)

## Running locally

1. Apply migrations and run the API:
   ```powershell
   dotnet run --project src/CourtFlow.Api
   ```
2. Run the Blazor web app:
   ```powershell
   dotnet run --project src/CourtFlow.Web
   ```

## Configuration

Connection string: `ConnectionStrings:Default` in API and Web `appsettings.Development.json` (or user secrets). Default API config now uses a local SQLite file (`Data Source=courtflow.db`).

Clerk is the only auth provider:

- API settings (`src/CourtFlow.Api/appsettings*.json`):
  - `Clerk:Enabled` = `true`
  - `Clerk:Authority` = your Clerk issuer URL (for JWT validation)
  - `Clerk:Audience` = audience claim expected by API tokens
- Web client settings (`src/CourtFlow.Web.Client/wwwroot/appsettings.json`):
  - `Clerk:PublishableKey` = your Clerk publishable key
  - `Clerk:FrontendApi` = optional Clerk frontend API host

Legacy password sign-in is intentionally disabled. `POST /api/v1/auth/sign-in` now returns `410 Gone`.

## Deployment (Azure SWA + Container Apps + Turso + Clerk)

The **architecture, Azure/Turso/Clerk setup, GitHub secrets, CI/CD YAML/Dockerfile templates, Postgres → Turso cleanup, and go-live checklist** are all in a single plan: [`docs/CourtFlow-Azure-Plan.md`](docs/CourtFlow-Azure-Plan.md) (and the Cursor plan `azure_swa_hardening` mirrors it). **§3.1** is the detailed Postgres removal checklist.

> **Pipeline files:** add `.github/workflows/*.yml`, `deploy/Dockerfile.api`, and `.dockerignore` from **§10** of that plan, or use Agent mode to materialize them as real files in the repo.
