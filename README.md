# CourtFlow

Volleyball tournament management platform. V1 is admin-only: an ASP.NET Core Web API plus a Blazor Web App (Server) for tournament directors to configure events, generate schedules, manage brackets, monitor courts, and record scores.

See the PRD: [Volleyball Tournament Management API + Blazor Web App PRD (V1, Admin-Only).md](Volleyball%20Tournament%20Management%20API%20+%20Blazor%20Web%20App%20PRD%20%28V1%2C%20Admin-Only%29.md).

## Solution Layout

- `src/CourtFlow.Domain` - entities, value objects, enums, domain events.
- `src/CourtFlow.Application` - CQRS handlers (MediatR), DTOs, validators (FluentValidation), abstractions.
- `src/CourtFlow.Infrastructure` - EF Core (Npgsql), migrations, Identity, engines, seed.
- `src/CourtFlow.Api` - ASP.NET Core Web API host, JWT auth, OpenAPI.
- `src/CourtFlow.ApiClient` - typed HTTP client used by the Blazor app.
- `src/CourtFlow.Web.Shared` - Razor Class Library with reusable UI components.
- `src/CourtFlow.Web` - Blazor Web App host (Server interactive).

## Prerequisites

- .NET 10 SDK
- Docker (optional, for local Postgres)

## Running locally

1. Start Postgres:
   ```powershell
   docker compose -f deploy/docker-compose.dev.yml up -d
   ```
2. Apply migrations and run the API:
   ```powershell
   dotnet run --project src/CourtFlow.Api
   ```
3. Run the Blazor web app:
   ```powershell
   dotnet run --project src/CourtFlow.Web
   ```

Default dev admin (seeded in Development only): `admin@courtflow.local` / `Admin!234`.

## Configuration

Connection string: `ConnectionStrings:Default` in API and Web `appsettings.Development.json` (or user secrets).

JWT keys: `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` in the API config.
