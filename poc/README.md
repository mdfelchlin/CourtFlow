# CourtFlow

Volleyball tournament management platform. V1 is admin-only: an ASP.NET Core Web API plus a Blazor Web App (Server) for tournament directors to configure events, generate schedules, manage brackets, monitor courts, and record scores.

All product, architecture, data-model, deployment, UI, and per-feature docs live in [`docs/`](docs/README.md). Start with [`docs/product-overview.md`](docs/product-overview.md).

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

Database configuration for API:

- Local default: set `ConnectionStrings:Default` (current default is `Data Source=courtflow.db`).
- Turso/libSQL: either set `ConnectionStrings:Default` to your libSQL URL, or set:
  - `Turso:Url`
  - `Turso:AuthToken` (optional if URL already contains token/query auth)

`ConnectionStrings:Default` takes precedence when both are present.

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

The **architecture, Azure/Turso/Clerk setup, GitHub secrets, CI/CD YAML/Dockerfile templates, libSQL migration notes, and go-live checklist** are all in a single plan: [`docs/azure-plan.md`](docs/azure-plan.md) (and the Cursor plan `azure_swa_hardening` mirrors it). **§3.1** tracks legacy database decommission guidance.

> **Pipeline files:** add `.github/workflows/*.yml`, `deploy/Dockerfile.api`, and `.dockerignore` from **§10** of that plan, or use Agent mode to materialize them as real files in the repo.
