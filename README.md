# CourtFlow

CourtFlow is a volleyball tournament application. A director sets up a tournament, runs it, repairs it when the day changes, and publishes what coaches and spectators may see, without emailing a vendor.

Directors and organizers are the only writers. Coaches, players, and spectators read a landing page and, as that surface is built, a published event. They have no account. Each organization's tournaments, clubs, and teams stay separate from every other organization's.

The accepted product direction is [`intent/product/intent.md`](intent/product/intent.md). That file is the north star: who the application is for, the tech stack, and the pillars of value. New work starts in [`intent/`](intent/README.md).

## Tech stack

- .NET 10 API
- Blazor WebAssembly with MudBlazor
- Google OR-Tools for scheduling (named in the product direction; not in the tree yet)
- WorkOS for auth
- Entity Framework Core and Postgres
- .NET Aspire to start the local API, Blazor app, Postgres, and Mailpit together

The domain model holds the rules. An application layer holds the use cases. The API, the Blazor app, and persistence stay outside that model.

Supabase is the hosted Postgres when deployment is in scope. Until then, the app uses local Postgres.

## What a director can do today

These slices are accepted and implemented. Each folder under `intent/` is the record of the decision.

| Slice | What it covers |
| --- | --- |
| [Tournament setup](intent/tournament-setup/intent.md) | Create, edit, and delete a tournament. Name, dates, US time zone, default match duration, description, venues, and numbered courts. |
| [Tournament lifecycle](intent/tournament-lifecycle/intent.md) | Unpublished, published, and archived. A published tournament archives itself 270 days after it was last published. Publishing a tournament does not publish a schedule. |
| [Tournament divisions](intent/tournament-divisions/intent.md) | Divisions on the tournament editor, each with a name and a gender. |
| [Tournament teams](intent/tournament-teams/intent.md) | Clubs and teams for the organization, team entry per tournament, and a CSV import with a preview. |
| [Current tournament](intent/current-tournament/intent.md) | One tournament dropdown, shared by the pages that work inside a tournament. |
| [Division ranking](intent/division-ranking/intent.md) | Rank a division's teams by dragging, typing a rank, or randomizing. Pools and brackets are placeholders on the same Format editor. |
| [Sign-in and access](intent/sign-in-and-access/intent.md) | WorkOS sign-in, Request Access, operator review, and owner, editor, and viewer roles. One organization per person. |
| [Public landing page](intent/public-landing-page/intent.md) | The site root is a public page. Login is separate from the director frame. |
| [Look and feel](intent/look-and-feel/intent.md) | Shared director frame, brand, light and dark themes, and form and feedback conventions. |
| [US date format](intent/us-date-format/intent.md) | A director sees calendar dates as `MM/dd/yyyy`. The API keeps `yyyy-MM-dd`. |

The director drawer lists Tournaments, Teams, and Format. Operators also get Requests and Organizations.

The public site today is the landing page (`Welcome` / `Coming soon`), Request Access, and the no-access page. Visitors cannot read a published tournament yet. Inside an organization, a viewer can read published tournaments and cannot change them.

## What is next

Draft slices, not yet specified or built:

- [Division pools](intent/division-pools/intent.md). Fill a division's pools from its ranking, under constraints the director sets. Pool sizes can differ. Google OR-Tools is the candidate solver.
- [Division brackets](intent/division-brackets/intent.md). Set the elimination shape and how pool finishes feed bracket slots. Scoring and advancing winners wait for the tournament day.
- [Logo refresh](intent/logo-refresh/intent.md). A redrawn mark that stays sharp and readable on light and dark surfaces. `tools/logo/` generates the current assets.

The product intent still ahead of any slice:

- **Schedule.** Generating, previewing, and publishing a schedule are explicit steps. A reflow keeps earlier versions. Conflict checks consider the full schedule. The drawer name reserved for this is Schedule, after Format.
- **Tournament day.** Court status shows, at a glance, which courts are behind or ahead. Repairs (withdrawals, outages, reassignments, reflow) are recorded. History is not silently overwritten.
- **Public event.** Coaches, players, and spectators read what the organizer has published. They cannot change anything.
- **Deployment.** Hosted Postgres, a real sending domain for email, and WorkOS email on CourtFlow's own domain.

Navigation order for the work still to come: Teams, Format, Schedule, Tournament day.

## How a change is written down

Each change is a folder under `intent/`, named in kebab-case for the outcome (`division-pools`, not a screen or a library). The folder fills in this order:

1. **`intent.md`** from [`intent/_template.md`](intent/_template.md). Status stays `draft` until the person who asked accepts it. Technology choices stay in Open questions unless that person called one non-negotiable.
2. **`spec.md`** in the same folder, after the intent is accepted. Behavior, rules, and edge cases a plan can be written against. The product intent locks the tech stack.
3. **`plan.md`** beside that spec. Files and tests under `src/`, in the order to change them.
4. **Code** under [`src/`](src/).

[`intent/README.md`](intent/README.md) is the short version of that path. [`AGENTS.md`](AGENTS.md) points agents at the same place. Domain language lives in the accepted product intent and the accepted specs.

## Skills

Project skills live in [`.cursor/skills/`](.cursor/skills/). Cursor loads them when the request matches. Each skill stops at its own artifact.

| Skill | When to use it | What it writes |
| --- | --- | --- |
| `capture-intent` | A problem, an idea, or "start a slice." | `intent/<slice>/intent.md` only. |
| `write-spec` | A spec, requirements, or design from an accepted intent. | `spec.md` beside that intent. Refuses a draft intent. |
| `write-plan` | Plan the implementation of a spec. | `plan.md`. Names files and tests. Does not edit application code. |
| `verify-engineering-practices` | Check the current branch against the engineering-practices rule. | Edits structure, project settings, and checks that miss the rule. Leaves accepted behavior alone. |
| `update-packages` | Bring NuGet packages under `src/` up to date. | Versions in `src/Directory.Packages.props` only. Asks before a breaking upgrade or a package that still has an advisory. |

Ask for the step you want. "Write an intent" does not also produce a spec or code.

## Rules

Two rules in [`.cursor/rules/`](.cursor/rules/) apply to every change under `src/`.

### Engineering practices

[`.cursor/rules/engineering-practices.mdc`](.cursor/rules/engineering-practices.mdc)

Product code follows current Microsoft guidance for .NET, ASP.NET Core, Blazor, Entity Framework Core, Aspire, and xUnit. Where an accepted intent already differs, the accepted behavior stays:

- Theme and layout live in typed C# and MudBlazor parameters. Hand-written CSS is only the loading screen and the unhandled-error bar in `wwwroot/css/app.css`.
- There is no Aspire service-defaults project. Ports stay fixed (see [Local addresses](#local-addresses)).
- The session stays in the `HttpOnly` cookie `courtflow-session`. Page code does not read a WorkOS token. Who is signed in comes from `GET /api/me`.
- A calendar date a director sees uses `MM/dd/yyyy`. The API keeps `yyyy-MM-dd`.

`src/Directory.Build.props` holds `AnalysisLevel` `latest`, `AnalysisMode` `Recommended`, `TreatWarningsAsErrors` `true`, and `EnforceCodeStyleInBuild` `true`. Info-level suggestions stay off. Generated migration files stay generated. A suppression needs a reason beside it.

Google OR-Tools, hosted Postgres, and `tools/logo/` sit outside this rule. Package versions belong to the current-packages rule. Checking a branch is the `verify-engineering-practices` skill.

### Current packages

[`.cursor/rules/current-packages.mdc`](.cursor/rules/current-packages.mdc)

Add a NuGet package only when the task specifically says to add that package. The version is the latest stable, written once in `src/Directory.Packages.props`. A `PackageReference` under `src/` does not set `Version`. Prerelease packages are refused. Updating MudBlazor, Entity Framework Core, Npgsql, Aspire, or the test packages to a newer stable version is allowed. Replacing one of them with a different product is not.

When the latest stable version has a known vulnerability, stop and let the developer decide. A full bring-up of packages already declared is the `update-packages` skill.

## Get started

You need:

- The [.NET 10 SDK](https://dotnet.microsoft.com/download). Every project targets `net10.0`.
- A container runtime Aspire can use, typically Docker. The AppHost runs Postgres 16 and Mailpit as containers. API tests use Testcontainers and need the same runtime.
- A WorkOS **test** environment API key, so Login can reach WorkOS. The test client id is already in [`src/CourtFlow.Api/appsettings.Development.json`](src/CourtFlow.Api/appsettings.Development.json).

From the repository root:

```powershell
dotnet user-secrets set Parameters:workos-api-key "<WorkOS test API key>" --project src/CourtFlow.AppHost
dotnet user-secrets set Parameters:resend-api-key "local" --project src/CourtFlow.AppHost
dotnet run --project src/CourtFlow.AppHost
```

The Resend value can be any placeholder on a local run. Aspire passes it through, and local mail goes to Mailpit because the AppHost sets `Email:Smtp`. If either secret is missing, the AppHost asks for it at startup.

That command starts the API, the Blazor app, Postgres, and Mailpit, and opens the Aspire dashboard. Stopping it stops all four. Postgres data stays in the Docker volume `courtflow-postgres`. The first start migrates the database and seeds Test Organization.

Open the app at <https://localhost:7211>. Login sends the browser to WorkOS and back to the API at `https://localhost:7011/auth/callback`. An email listed in `Development:TestOrganizationOwners` becomes an owner of Test Organization the first time that person signs in locally and has no membership. That list is in `appsettings.Development.json`.

Request Access uses Cloudflare Turnstile's always-pass test keys, already in configuration, so the form works without a Turnstile account. CourtFlow's own emails (invitations, request decisions, organization changes) show up in the Mailpit inbox linked from the Aspire dashboard. WorkOS still sends its own password-reset and email-confirmation codes.

If `5009`, `7011`, `5186`, `7211`, or `5432` is already taken, startup fails. Aspire does not pick another port.

### Tests

```powershell
dotnet test src/CourtFlow.slnx
```

Tests are xUnit v3 on Microsoft.Testing.Platform. They do not call WorkOS, Resend, or Turnstile. API tests start Postgres through Testcontainers.

`dotnet format src/CourtFlow.slnx` applies the style rules the build enforces. Warnings are errors.

## Local addresses

| Resource | Address |
| --- | --- |
| Blazor app | <http://localhost:5186> and <https://localhost:7211> |
| API | <http://localhost:5009> and <https://localhost:7011> |
| Postgres | `localhost:5432`, database `courtflow`, user `courtflow`, password `courtflow` |
| Aspire dashboard | The URL printed when the AppHost starts (launch profile `https://localhost:17150`) |
| Mailpit | The HTTP endpoint on the Mailpit resource in the dashboard |

The Blazor app calls the API at `https://localhost:7011/`. The API allows credentialed browser calls from `http://localhost:5186` and `https://localhost:7211`.

## Configuration

Committed settings are safe to share. Secrets stay in user secrets or the Aspire parameters above.

| Setting | Where | Local value |
| --- | --- | --- |
| `WorkOS:ApiKey` | AppHost parameter `workos-api-key` | WorkOS test secret key |
| `WorkOS:ClientId` | API `appsettings.Development.json` | Test environment client id |
| `WorkOS:RedirectUri` | API `appsettings.json` | `https://localhost:7011/auth/callback` |
| `Development:TestOrganizationOwners` | API development settings | Emails that become Test Organization owners on first local sign-in |
| `Operators:Emails` | API `appsettings.json` | Who can open the operator screens |
| `Turnstile:SiteKey` / `Turnstile:SecretKey` | Web and API settings | Cloudflare always-pass test keys |
| `Email:Smtp` | Set by the AppHost | Mailpit |
| `Resend:ApiKey` | AppHost parameter `resend-api-key` | Unused locally while Mailpit is set |
| `Email:Domain` | API settings | Empty until a sending domain exists. Deployed mail then uses Resend. |

The WorkOS test environment this app expects: email and password plus Google, Waitlist on, Waitlist confirmation email off, invitation emails off (CourtFlow sends its own), sessions 30 days with no inactivity timeout, redirect `https://localhost:7011/auth/callback`, sign-out `https://localhost:7211/`. Details are in the [sign-in plan](intent/sign-in-and-access/plan.md).

## Repository layout

```
intent/                 One folder per slice: intent.md, then spec.md, then plan.md
src/CourtFlow.Domain/           Rules
src/CourtFlow.Application/      Use cases
src/CourtFlow.Infrastructure/   EF Core, WorkOS, email, Turnstile
src/CourtFlow.Api/              HTTP API
src/CourtFlow.Web/              Blazor WebAssembly app
src/CourtFlow.AppHost/          Aspire startup
src/*.Tests/                    Domain, application, API, web, and AppHost tests
src/CourtFlow.slnx              Solution
src/Directory.Packages.props    NuGet versions
src/Directory.Build.props       Analyzer and style settings
.cursor/rules/          Rules that apply to every src/ change
.cursor/skills/         capture-intent, write-spec, write-plan, verify-engineering-practices, update-packages
tools/logo/             Logo, favicon, and app-icon generator (`npm run generate`)
```

Director routes in the Blazor app:

| Route | Page |
| --- | --- |
| `/` | Public landing page |
| `/request-access` | Request Access |
| `/no-access` | Signed in, with no usable organization |
| `/tournaments` | Tournament list |
| `/tournaments/new`, `/tournaments/{id}` | Tournament editor |
| `/teams` | Teams for the current tournament |
| `/format` | Divisions and ranking status |
| `/format/{divisionId}` | Format editor (Ranking; Pools and Brackets are placeholders) |
| `/organization` | Members and invitations (owners) |
| `/operator/requests` | Access requests (operators) |
| `/operator/organizations` | Organizations (operators) |
