# Spec: Build and test before merge

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack: .NET API, Blazor WebAssembly with MudBlazor, Entity Framework Core and Postgres, .NET Aspire for local startup, WorkOS for auth, and Google OR-Tools for scheduling. This slice does not replace any of those. It adds the continuous-integration workflow that [engineering practices](../engineering-practices/spec.md) named and left out.

Startup behavior stays the one in [simple local startup](../simple-local-startup/spec.md), including the Mailpit exception in [sign-in and access](../sign-in-and-access/spec.md). Screen behavior stays the one in [look and feel](../look-and-feel/spec.md) and the accepted tournament specs. Sign-in stays the one in [sign-in and access](../sign-in-and-access/spec.md). Calendar dates stay the one in [US date format](../us-date-format/spec.md). Package versions stay the one in [current packages](../current-packages/spec.md). Shared build checks stay the ones in [engineering practices](../engineering-practices/spec.md).

## When a pull request runs

A pull request whose base branch is `main` runs one GitHub Actions workflow. The workflow file is `.github/workflows/build-and-test.yml`. The event is `pull_request`. The base branch filter is `main`. There is no path filter. A pull request that changes only docs, intent, or the workflow itself still runs.

The workflow that runs is the one on the pull request. A pull request that edits the workflow runs that edited workflow. The event is not `pull_request_target`.

Draft and ready pull requests both run. A new commit on the pull request starts a new run and cancels the in-progress run for the older commit. The result that gates the merge is the run for the commit being merged. A cancelled or superseded run is not a success.

Pull requests into any other branch do not run this workflow.

## What the job does

The workflow has one job. The workflow name is `Build and test`. The job name is `build-and-test`. The status check GitHub reports is `Build and test / build-and-test`.

The job runs on a GitHub-hosted Ubuntu runner (`ubuntu-latest`) and uses that runner's Docker daemon. It does not use a self-hosted runner. It does not run the tests inside a container that cannot see Docker.

The job checks out the pull request and installs the .NET 10 SDK, feature band `10.0.x`. It does not edit `src/global.json`. That file still selects Microsoft.Testing.Platform, and it still does not pin an SDK version.

From the repository root the job runs this command and no other build or test command:

```
dotnet test src/CourtFlow.slnx
```

The command has no configuration switch, no filter, no `--no-build`, and no `--no-restore`. It restores, builds, and runs every test project in the solution. Today those projects are `CourtFlow.Domain.Tests`, `CourtFlow.Application.Tests`, `CourtFlow.Api.Tests`, `CourtFlow.Web.Tests`, and `CourtFlow.AppHost.Tests`. A test project that is not in `src/CourtFlow.slnx` is outside this run. The job does not split the suite into unit and integration, and it does not add a second job.

`TreatWarningsAsErrors` is already on, so a warning fails the build and fails this command. A failed restore, a failed build, a failed test, a Docker pull that fails, or the job timeout fails the job. The job timeout is 30 minutes. `continue-on-error` is not set.

API tests start Postgres themselves through Testcontainers, using the image they already use, `postgres:16-alpine`. The workflow does not define a Postgres service container. It does not start the Aspire AppHost, Mailpit, or the local Postgres on port `5432`. The AppHost tests keep setting `COURTFLOW_SKIP_VOLUME_ADOPTION` themselves. The workflow does not set it.

The workflow's token permissions are `contents: read`. It declares no secrets and no GitHub environment. It does not read WorkOS, Resend, or Turnstile credentials. NuGet restore uses the public nuget.org feed. The tests pull `postgres:16-alpine` from the public registry.

`tools/logo/` is not built. The pipeline does not run `dotnet format` as its own step. Style rules the build already enforces still fail the build.

A failure that also fails on a developer machine with the .NET 10 SDK and a container runtime is a broken tree. This slice does not edit `src/` to make that failure green. A failure that is only a missing SDK or a missing Docker daemon on the runner is fixed in the workflow.

## What blocks the merge

`main` has a repository ruleset with both of these rules:

- Changes reach `main` through a pull request. A direct push to `main` is refused.
- The check `Build and test / build-and-test` has succeeded for the commit being merged. A missing, pending, cancelled, or failing check blocks the merge.

The ruleset's bypass list is empty. Repository admins are not exempt.

The workflow file alone does not block a merge. The ruleset is part of this slice. It is stored in the GitHub repository settings, not in a file in the tree.

The ruleset does not require a review, a signed commit, a linear history, or the pull request branch to be current with `main`. A pull request whose own check succeeded can merge while `main` has moved on.

## What stays the same

Directors, coaches, players, and spectators see no change. Local startup, ports, the session cookie, US dates, and package versions stay as their specs describe.

The product tree under `src/` stays as it is. `src/global.json`, `src/Directory.Build.props`, `src/Directory.Packages.props`, and `src/CourtFlow.slnx` are unchanged. The target framework stays `net10.0`. The command a developer already runs stays `dotnet test src/CourtFlow.slnx`.

## How this is checked

- `.github/workflows/build-and-test.yml` is the workflow this slice adds. Its trigger is `pull_request` to `main`, with no path filter and no `pull_request_target`.
- The job is named `build-and-test`, runs on `ubuntu-latest`, times out at 30 minutes, sets permissions to `contents: read`, and declares no secrets.
- The job's test command is `dotnet test src/CourtFlow.slnx` from the repository root, with no extra arguments.
- A pull request into `main` shows the check `Build and test / build-and-test`. A failing test fails that check.
- The ruleset on `main` requires a pull request and that check, and its bypass list is empty.
- The same `dotnet test src/CourtFlow.slnx` command still passes locally when a container runtime is available.

## Decisions this spec makes that the intent did not

The intent left these to the spec. Change any of them before a plan is written.

- **GitHub Actions.** The pipeline is a workflow in this repository. The file is `.github/workflows/build-and-test.yml`.
- **One check.** The workflow name is `Build and test` and the job name is `build-and-test`. The required status check is `Build and test / build-and-test`.
- **Ubuntu and Docker on the runner.** GitHub-hosted `ubuntu-latest` supplies the container runtime. Testcontainers pulls `postgres:16-alpine`. The workflow does not start its own Postgres service.
- **.NET 10 SDK band.** The job installs SDK `10.0.x`. `src/global.json` stays as it is and still selects Microsoft.Testing.Platform.
- **The local command, unchanged.** One `dotnet test src/CourtFlow.slnx` from the repository root. No configuration, filter, or `--no-build`.
- **Thirty-minute timeout.** An overrun fails the check.
- **Cancel the older run.** A new commit on the same pull request cancels the in-progress run for the previous commit.
- **Pull request only.** The event is `pull_request`, so the pull request's own workflow runs. `pull_request_target` is not used.
- **Ruleset outside the tree.** Blocking the merge is a GitHub ruleset on `main`: pull requests required, this check required, bypass list empty. Direct pushes are refused.
- **No up-to-date requirement.** The branch does not have to be current with `main` for the ruleset to allow the merge.

## Deliberately not in this slice

- A deploy, a release, or a run of the Aspire AppHost.
- Required reviews, code owners, signed commits, or a merge queue.
- Requiring the pull request to be up to date with `main` before merge.
- A separate `dotnet format` job, a coverage gate, or a split between unit and integration tests.
- Building `tools/logo/`, or calling WorkOS, Resend, Turnstile, or Supabase.
- Package version changes, shared build setting changes, or edits under `src/` so the pipeline will pass.
- Changing director-facing behavior, API contracts, ports, or the target framework.
- A workflow on pushes to `main` after the merge. The pull request run is the gate. The ruleset is what stops a push that skipped it.
- Path filters that skip the suite when a pull request does not touch `src/`.

## Flagged

No open question from the intent remains.

The ruleset cannot be stored in this repository. Saving it on the GitHub repository is part of the slice. A workflow file with the right check name does not, by itself, stop a merge.
