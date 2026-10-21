# Spec: Simple local startup

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack. This slice adds .NET Aspire only as the way a developer starts local CourtFlow. The running applications stay the .NET API, the Blazor WebAssembly app, and local Postgres. It does not call WorkOS, OR-Tools, or Supabase.

## What the developer can do

A developer starts local CourtFlow with one command. That command runs the Aspire app host in the current solution.

The command starts only these three:

- The .NET API.
- The Blazor WebAssembly app.
- Postgres.

The same command opens the Aspire dashboard, or prints a URL the developer can open. The dashboard shows each of the three resources, whether it is starting, running, or failed, its logs, and its endpoints.

Stopping the command stops all three. Starting it again brings the same three up on the same endpoints.

The developer does not start Postgres with Docker Compose, and does not start the API or the Blazor app as their own commands. Test runs are separate and are not part of this startup.

## Endpoints that stay put

Aspire does not assign new ports. These are the endpoints, and a second start does not move them.

| Resource | Endpoint |
| --- | --- |
| API | `http://localhost:5009` and `https://localhost:7011` |
| Blazor app | `http://localhost:5186` and `https://localhost:7211` |
| Postgres | `localhost:5432`, database `courtflow`, username `courtflow`, password `courtflow` |

The Blazor app keeps calling the API at `http://localhost:5009/`. The API keeps allowing browser calls from `http://localhost:5186` and `https://localhost:7211`.

The API keeps using this connection string:

`Host=localhost;Port=5432;Database=courtflow;Username=courtflow;Password=courtflow`

If `5009`, `7011`, `5186`, `7211`, or `5432` is already taken, startup fails and the failure is visible in the command output or on the dashboard. Aspire does not pick a free port instead.

## Startup order and data

Postgres accepts connections before the API starts its own work. The API then does what it already does on startup: apply its migrations and seed Test Organization. The Blazor app can come up in the same command before the API is ready. Until the API is running, the dashboard shows the API as starting or failed, and the tournament screens cannot load data.

Postgres keeps data that already exists from Docker Compose. The first Aspire start does not replace that database with an empty one. Tournaments already saved are still there.

Data created after that, including those preexisting tournaments, survives stopping the command and starting it again.

A machine that has never started CourtFlow Postgres is a normal start. Aspire creates Postgres with the database, username, and password above. The API migrates that empty database, and the Blazor app then shows the empty tournament list described in [tournament setup](../tournament-setup/spec.md).

## What stays the same for a director

Tournament setup behavior does not change. A director still opens the Blazor app, works with Test Organization's tournaments, and never sees startup, Aspire, or Postgres.

## Deliberately not in this slice

- SQLite, Turso, or a hosted database.
- A separate migrator project. The API applies its own migrations.
- A service-defaults project, or ports other than the ones in this spec.
- Clerk, user secrets, or a demo login.
- Google Maps keys.
- Launching test projects, or any project other than the API, the Blazor app, and Postgres.
- WorkOS, OR-Tools, and Supabase.
