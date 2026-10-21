# Spec: Engineering practices

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack: .NET API, Blazor WebAssembly with MudBlazor, Entity Framework Core and Postgres, .NET Aspire for local startup, WorkOS for auth, and Google OR-Tools for scheduling. This slice does not replace any of those. Domain rules stay in the domain. Use cases stay in the application layer. The API, the Blazor app, and persistence stay outside that model.

It adds shared build checks for the product tree under `src/`, brings the code already there up to those checks, and leaves a rule and a skill so later branch changes stay there.

Startup behavior stays the one in [simple local startup](../simple-local-startup/spec.md). Screen behavior stays the one in [look and feel](../look-and-feel/spec.md) and the accepted tournament specs. Sign-in stays the one in [sign-in and access](../sign-in-and-access/spec.md). Calendar dates stay the one in [US date format](../us-date-format/spec.md). Package versions stay the one in [current packages](../current-packages/spec.md).

## Shared build settings

One file, `src/Directory.Build.props`, holds the checks for every project under `src/`. No project file sets them again, and none sets a weaker value.

The file sets:

- `AnalysisLevel` to `latest`
- `AnalysisMode` to `Recommended`
- `TreatWarningsAsErrors` to `true`
- `EnforceCodeStyleInBuild` to `true`
- `Nullable` to `enable`
- `ImplicitUsings` to `enable`

`AnalysisMode` is not `All`. A rule the SDK template marks as a suggestion or silent stays at that severity. Suggestion and info diagnostics do not fail the build.

A project may repeat `Nullable` as `enable` and `ImplicitUsings` as `enable`. A project that sets either to `disable`, or sets `TreatWarningsAsErrors` to `false`, fails the build check described below.

`src/Directory.Packages.props` is untouched. This slice does not add, remove, or change a package version.

## Editor config and generated migrations

A repository-root `.editorconfig` carries the .NET SDK editorconfig template. `EnforceCodeStyleInBuild` makes the template's warning and error style rules fail the build. The template's suggestion and silent rules are not raised to warning.

`src/CourtFlow.Infrastructure/Migrations/` is generated code. The editorconfig marks that folder `generated_code = true`. Analyzer and style rules skip it. The bring-up does not edit those files, including their existing `#nullable disable` and the `#pragma warning disable 612, 618` the generator writes.

If `dotnet build` still reports a compiler warning only inside that folder, the plan suppresses those codes for `CourtFlow.Infrastructure` with a comment that names the Migrations folder, and only the codes the build actually reports. That suppression is not copied to any other project, and it is not a repo-wide `NoWarn`.

## Suppressions

A hand-written suppression outside Migrations is a `#pragma warning disable`, a `SuppressMessage`, or a `NoWarn`. It has a reason beside it: a comment on the same line as the pragma, a `Justification` on `SuppressMessage`, or an XML comment on `NoWarn`.

The skill does not add a suppression to make the build pass. The compiler does not check that a reason is present. The rule and the skill do.

The one hand-written suppression already in the tree is `CS0618` in `src/CourtFlow.AppHost.Tests/AppHostModelTests.cs`. The bring-up removes it by updating the call, or keeps it and adds a reason when updating the call would change what the test asserts. The assertions on ports, proxying, and environment names stay as [simple local startup](../simple-local-startup/spec.md) and the AppHost tests already have them.

## The rule

A project rule at `.cursor/rules/engineering-practices.mdc` is always on. Agents follow it whenever they change CourtFlow. It says:

- Write product code under `src/` to current Microsoft guidance for .NET, ASP.NET Core, Blazor, Entity Framework Core, Aspire, and xUnit.
- Keep the accepted differences below. When Microsoft guidance would change one of them, keep the accepted behavior.
- **Styling.** Theme and layout live in typed C# and MudBlazor parameters, as [look and feel](../look-and-feel/spec.md) specifies. Do not add `*.razor.css`. Hand-written CSS is only the loading screen and the unhandled-error bar in `wwwroot/css/app.css`.
- **Aspire.** Do not add a service-defaults project. The API stays on `5009` and `7011`, the Blazor app on `5186` and `7211`, and Postgres on `5432`. Aspire does not pick a free port.
- **Sign-in.** The session stays in the `HttpOnly` cookie `courtflow-session`. Page code does not read a WorkOS token. The Blazor app still learns who is signed in from `GET /api/me`.
- **Dates.** A calendar date a director sees uses `MM/dd/yyyy`. The API keeps `yyyy-MM-dd`.
- Do not change director-facing behavior, an API contract, or behavior an accepted intent specifies.
- The shared build settings above are required. Generated migration files stay generated. A suppression needs a reason beside it. Do not add one to make the build pass.
- Google OR-Tools, hosted Postgres, and `tools/logo/` are outside this rule. Package versions belong to the current-packages rule.
- Checking a branch against this rule is the skill below.

## The skill

A project skill at `.cursor/skills/verify-engineering-practices/SKILL.md` can be called to verify the changes on the current branch. This slice also brings the whole product tree up to the rule once, as the next section describes. Later calls are branch-scoped.

The skill:

- Reads `.cursor/rules/engineering-practices.mdc`.
- Diffs the current branch against the repository's default branch, and includes uncommitted changes under `src/`.
- Checks those changed files. It leaves files the branch does not touch, except a warning that its own edit caused, which it fixes and reports.
- Edits a violation when the edit is structure, project settings, or checks. `dotnet format` on `src/CourtFlow.slnx` applies the style rules the build enforces.
- Does not change director-facing behavior, an API contract, or behavior an accepted spec specifies. When the only fix would do that, it does not edit, and it tells the developer the file, the diagnostic, and why it stopped.
- Does not move a type from one project to another. A layering miss is reported. The developer decides.
- Restores a branch change that breaks an accepted difference: a new `*.razor.css`, a service-defaults project, a WorkOS token readable by page code, or a director-facing calendar date that does not use `MM/dd/yyyy`.
- Keeps the shared build settings present. It does not set `AnalysisMode` to `All` or raise suggestion rules to warning.
- Does not hand-edit `src/CourtFlow.Infrastructure/Migrations/`.
- Does not add a suppression to make the build pass.
- Does not edit `tools/logo/`. Does not add Google OR-Tools or hosted Postgres. Does not change package versions. That is the `update-packages` skill.

The report names each file the skill changed and what about it changed. When the branch already follows the rule, the report says so and lists no edits.

## The tree this slice leaves

After this slice, `dotnet build src/CourtFlow.slnx` succeeds with warnings treated as errors, and the existing tests pass.

Every hand-written file under `src/` is clean under the Recommended analyzers and the enforced style rules. Info and suggestion diagnostics may remain. Generated migration files are unchanged.

`src/Directory.Build.props` and the root `.editorconfig` are in place. The rule and the skill match this spec. The `CS0618` suppression in the AppHost test is either gone or has a reason beside it.

No project file under `src/` sets `AnalysisLevel`, `AnalysisMode`, `TreatWarningsAsErrors`, or `EnforceCodeStyleInBuild`. None sets `Nullable` or `ImplicitUsings` to `disable`. None sets `NoWarn` except the Migrations case above, if the build required it.

## What stays the same

Directors, coaches, players, and spectators see no new product behavior. Tournament screens, local startup, the session cookie, and US dates stay as their specs describe.

The target framework stays `net10.0`. `src/global.json` is unchanged. The solution file stays `src/CourtFlow.slnx`.

Offline sign-in is a later intent. This slice does not store a sign-in token where page code can read it.

## How this is checked

- `dotnet build src/CourtFlow.slnx` succeeds, and the existing test projects pass.
- A test reads the product tree the way the package-rules test does. `src/Directory.Build.props` is the only file that sets `AnalysisLevel`, `AnalysisMode`, `TreatWarningsAsErrors`, and `EnforceCodeStyleInBuild`, at the values in this spec. No project sets `Nullable` or `ImplicitUsings` to `disable`.
- The root `.editorconfig` marks `src/CourtFlow.Infrastructure/Migrations/` as generated code. The files in that folder are unchanged by this slice.
- A hand-written `#pragma warning disable` outside that folder has a reason on the same line, or it is gone.
- The rule file is always applied and names the four accepted differences. The skill file tells an agent to edit a branch violation that is structure, project settings, or checks, to report every edit, and to stop without editing when the fix would change accepted behavior.

## Decisions this spec makes that the intent did not

The intent left these to the spec. Change any of them before a plan is written.

- **One props file.** The four analysis properties, plus nullable and implicit usings, live in `src/Directory.Build.props`. Project files do not repeat the analysis properties.
- **SDK editorconfig.** The root `.editorconfig` is the .NET SDK template. Suggestion and silent rules are not promoted.
- **Migrations.** The folder is generated. Analyzer and style rules skip it. Compiler warnings that remain there are suppressed only for `CourtFlow.Infrastructure`, only for the codes the build reports, and the migration files are not edited.
- **Reasons are not a compiler check.** The build cannot see a comment. The rule and the skill require the reason.
- **Bring-up versus later calls.** This slice cleans the whole product tree once. Later skill calls are the branch diff plus uncommitted changes under `src/`.
- **Format.** `dotnet format` on `src/CourtFlow.slnx` applies enforced style. Remaining warnings are edited by hand.
- **No project moves.** The skill reports a type in the wrong project and does not move it.
- **Restore accepted differences.** A branch change that adds scoped CSS, a service-defaults project, a page-readable WorkOS token, or a non-US director date is edited back.
- **Own-edit fallout.** A warning caused by the skill's edit, in a file the branch did not otherwise touch, is fixed and reported.

## Deliberately not in this slice

- A root continuous-integration workflow. The intent's problem names the missing workflow. The accepted outcome does not add one.
- `AnalysisMode` `All`, and raising suggestion or info diagnostics to warnings.
- Google OR-Tools, hosted Postgres, and `tools/logo/`.
- Package version changes, including `src/Directory.Packages.props`.
- A readable sign-in token for offline use.
- Editing generated migration files.
- Changing director-facing behavior, API contracts, ports, or the target framework.
