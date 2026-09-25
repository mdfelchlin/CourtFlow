# Local Development

How to run CourtFlow on a developer machine, what the env vars in `launchSettings.json` mean, and how to point the WASM client at the API you want to hit.

## Prerequisites

- .NET 10 SDK
- The ASP.NET Core dev certificate trusted (`dotnet dev-certs https --trust`)
- SQLite (the dev DB file `courtflow-dev.db` is created automatically on first run)

**Non-local deployments** use **Turso/libSQL** as the default database provider (`Database:Provider = LibSql`). Local Aspire and standalone dev runs keep SQLite; see [`azure-plan.md`](azure-plan.md) for connection string wiring.

## Running with Aspire (recommended)

The orchestrated entry point is `CourtFlow.AppHost`. It launches the API, the Web (Blazor Web App + WASM client), and the Aspire dashboard together.

```bash
dotnet run --project src/CourtFlow.AppHost --launch-profile CourtFlow.AppHost
```

The dashboard prints a login URL like `https://localhost:17200/login?t=...`. From there you can open the Web project, browse logs/traces, and inspect resource state.

### What the `CourtFlow.AppHost` env vars do

These live in `src/CourtFlow.AppHost/Properties/launchSettings.json` and are required for the Aspire dashboard to start. If you remove them, Aspire fails at startup with `OptionsValidationException: Failed to configure dashboard resource because ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL and ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL environment variables are not set.`

| Variable | What it controls | Default value |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT` | Standard ASP.NET Core hosting environment. Must be `Development` for the dashboard + dev DB seeding to engage. | `Development` |
| `ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL` | gRPC OTLP listener the dashboard exposes for child resources (API, Web) to push traces/metrics/logs to. The Aspire dashboard requires **at least one** OTLP listener to be configured. | `https://localhost:21030` |
| `ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL` | HTTP/protobuf OTLP listener — same purpose as the gRPC endpoint, used by clients that don't support gRPC. Setting both is safe and lets Aspire route either flavour. | `https://localhost:21031` |
| `ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL` | gRPC endpoint the dashboard uses to query the AppHost for live resource state (running, healthy, exposed endpoints). | `https://localhost:22057` |

The port numbers themselves are arbitrary — any unused ports work. The values are pinned so the dashboard URL stays stable across runs.

The dashboard itself listens on the URL in `applicationUrl` (`https://localhost:17200;http://localhost:17201`).

### How API URLs are wired

Every project's HTTP/HTTPS endpoints are **pinned** in `src/CourtFlow.AppHost/Program.cs` to match the standalone `launchSettings.json` values, so the URLs never change between runs:

| Project | HTTP | HTTPS |
| --- | --- | --- |
| `CourtFlow.Api` | `http://localhost:5033` | `https://localhost:7028` |
| `CourtFlow.Web` | `http://localhost:5111` | `https://localhost:7247` |

The AppHost injects the API's HTTPS endpoint into the Web project as `Api__BaseUrl`, so the server-side Refit client always knows where to call.

On startup, the AppHost runs **`CourtFlow.DbMigrator`** first, then starts the API (which waits for migrations). Fresh clones get an up-to-date schema without a separate migrator step.

## Pointing the WASM client at an API

The Blazor WASM client runs in the browser and fetches its config from its own `wwwroot/appsettings.json`, independently of the server. Edit that file to choose which API the client calls:

`src/CourtFlow.Web.Client/wwwroot/appsettings.json`

```json
{
  "Api": { "BaseUrl": "<one of the URLs below>" },
  "Clerk": { ... }
}
```

| Scenario | `Api.BaseUrl` |
| --- | --- |
| Running via Aspire or API standalone (HTTPS) | `https://localhost:7028/` |
| Running the API standalone with `--launch-profile http` | `http://localhost:5033/` |
| Hitting the deployed Azure API | `https://courtflow-dev-api.thankfulocean-c1b15ccc.westus2.azurecontainerapps.io/` |

When you change this file, do a hard refresh (Ctrl+Shift+R) in the browser so the WASM runtime picks up the new value.

The Web *server* reads its own `Api:BaseUrl` from `appsettings.Development.json` (or the `Api__BaseUrl` env var injected by Aspire). Keep those two in sync when you're not running via Aspire.

## Google Maps (venue address autocomplete)

Venue address fields use the **Google Places Autocomplete** widget when an API key is configured. Without a key, the address field behaves as a plain text input.

1. In [Google Cloud Console](https://console.cloud.google.com/), enable **Maps JavaScript API** and **Places API** for your project.
2. Create a **browser-restricted** API key (HTTP referrer restrictions for `https://localhost:*` and your deployed Web origin).
3. Set the key in either or both of:
   - `src/CourtFlow.Web/appsettings.Development.json` → `GoogleMaps:ApiKey` (injected into the host page as `window.__courtflowGoogleMapsConfig`)
   - `src/CourtFlow.Web.Client/wwwroot/appsettings.json` → `GoogleMaps:ApiKey` (fallback when the WASM client loads config directly)

The key is client-side by design; restrict it by referrer in Google Cloud — do not treat it as a server secret.

## Authentication & organizations

Production is **invite-only** — see [`adr/0008-personal-organizations-and-team-upgrade.md`](adr/0008-personal-organizations-and-team-upgrade.md). Prospective customers use **Request access**; operators provision orgs and send the first invite. **Disable public sign-up** in the Clerk Dashboard (Restricted mode).

For local development without admin tooling:

- Set **`Dev:ClerkUserId`** in user-secrets to your Clerk `sub` (see below) so the demo org membership attaches to your login.
- Optional: **`Dev:AllowPersonalWorkspace=true`** keeps legacy auto-provision in Development only (until removed).

The seeded demo tournament lives in the `courtflow-demo` org.

### Seeing the demo tournament on your local login

The `DemoSeeder` populates a `CourtFlow Demo Club` org with a `Spring Classic` tournament, courts, divisions, etc. To make that data visible to **your** Clerk login, drop your Clerk `sub` into user-secrets:

```bash
dotnet user-secrets --project src/CourtFlow.Api set "Dev:ClerkUserId" "user_3CihXXXXXXXXXXXXXXXXX"
```

Restart the AppHost. The seeder will add an `Admin` `OrganizationMembership` row linking your `sub` to the demo org.

If you leave `Dev:ClerkUserId` unset, the demo tournament is still seeded but no real user has access to it — you'll need a membership row (via provision tooling once AUTH-02 ships, or manual DB seed) to sign in successfully in production-like mode.

You can read your `sub` off any Clerk-issued JWT (decode the middle segment), or from the Clerk dashboard → Users → click yourself → "User ID".

## Running the API alone

If you want the API without the rest of the orchestration:

```bash
dotnet run --project src/CourtFlow.Api --launch-profile https
```

That binds `https://localhost:7028` and `http://localhost:5033`. Swagger is mounted at `/swagger` in Development.

## Running the Web alone

```bash
dotnet run --project src/CourtFlow.Web --launch-profile https
```

Binds `https://localhost:7247` and `http://localhost:5111`. Requires an API to be reachable at whatever `Api:BaseUrl` resolves to.

## Port reference

All ports below are pinned (`WithHttpEndpoint`/`WithHttpsEndpoint` in `CourtFlow.AppHost/Program.cs` and matching values in each project's `Properties/launchSettings.json`).

| Concern | Port | Owner |
| --- | --- | --- |
| Aspire dashboard | 17200 (https) / 17201 (http) | `CourtFlow.AppHost` |
| OTLP gRPC ingest | 21030 | Aspire dashboard |
| OTLP HTTP ingest | 21031 | Aspire dashboard |
| Aspire resource service | 22057 | Aspire dashboard |
| API | 7028 (https) / 5033 (http) | `CourtFlow.Api` |
| Web | 7247 (https) / 5111 (http) | `CourtFlow.Web` |

The API's CORS allow-list (`src/CourtFlow.Api/appsettings.json` → `Cors:Origins`) is wired to the Web project's pinned ports, so the WASM client can call the API directly from the browser.
