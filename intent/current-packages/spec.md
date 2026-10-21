# Spec: Current packages

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack: .NET API, Blazor WebAssembly with MudBlazor, Entity Framework Core and Postgres, .NET Aspire for local startup, WorkOS for auth, and Google OR-Tools for scheduling. This slice does not replace any of those. It changes how the product tree under `src/` declares NuGet packages, brings the packages already there up to date, and leaves a rule and a skill so later work stays there.

Startup behavior stays the one in [simple local startup](../simple-local-startup/spec.md), including the Mailpit exception in [sign-in and access](../sign-in-and-access/spec.md). Screen behavior stays the one in [look and feel](../look-and-feel/spec.md) and the accepted tournament specs. A package update that would change those behaviors is a breaking change, handled below.

## Where versions live

NuGet Central Package Management covers every project under `src/` and nowhere else. The central file is `src/Directory.Packages.props`.

`ManagePackageVersionsCentrally` is on. Each package id has one exact stable version in that file. A `PackageReference` under `src/` names the package and keeps any assets metadata it already has (`PrivateAssets`, `IncludeAssets`, and the like). It does not set `Version`. A project that sets its own version fails the build.

`CourtFlow.Domain` and `CourtFlow.Application` reference no NuGet packages today. They stay that way unless a later task specifically adds one. They still build with the central file in place.

Versions are exact numbers, not ranges and not floating versions. The latest stable version is the highest nuget.org version that is not a prerelease. A prerelease is any version NuGet marks as prerelease, including preview, rc, alpha, and beta. Prerelease is never selected.

## The rule for adding packages

A project rule at `.cursor/rules/current-packages.mdc` is always on. Agents follow it whenever they change CourtFlow. It says:

- Do not add a NuGet package unless the user specifically instructed this task to add that package. If the task needs a library the user did not name, stop and ask. Do not pick one and add it.
- When the user has instructed an add, use the latest stable version. Write that version only in `src/Directory.Packages.props`. If that package id is already declared, move the single declared version to the latest stable. Do not add a second version for one id.
- Do not add a prerelease, even when a newer prerelease exists. A request to add a prerelease stops on this rule.
- Do not put `Version` on a `PackageReference` under `src/`.
- Do not replace a stack package with a different product. Updating MudBlazor, Entity Framework Core, Npgsql, Aspire, or the test packages to a newer stable version is allowed. Swapping one of them for something else is not.
- If the latest stable version has a known vulnerability, stop and let the developer decide. Do not choose another package, an older version, or a suppression instead.
- A full bring-up of packages already in the tree is the skill below, not an ad-hoc edit.

## The skill that brings packages up to date

A project skill at `.cursor/skills/update-packages/SKILL.md` brings every NuGet package the product tree names up to date. This slice runs that skill once against the current tree. Later, an agent uses the same skill when asked to update packages.

The skill:

- Reads the versions in `src/Directory.Packages.props` and the projects under `src/`.
- Looks up the latest stable version of each declared package on nuget.org.
- Applies each newer stable version that is not a breaking change.
- Asks the developer about each breaking change, one package at a time, before changing that package. The ask names the package, the declared version, and the latest stable version. The skill waits for how to proceed. It does not apply that package's update, open a pull request, or skip the package until the developer answers. It then does what the developer said for that package.
- Does not remember a previous answer as a permanent exception. The next run asks again about any declared version that is still behind a breaking latest stable.
- Does not add a package id that is not already declared.
- Does not edit `tools/logo/` or `TargetFramework`.
- Does not take a prerelease.

A same-major update that fails to build or fails the existing tests is treated as a breaking change. The skill stops and asks how to proceed. It does not rewrite product behavior to make the new version fit unless the developer tells it to.

When every declared package is already on its latest stable version, the skill says so and still reports vulnerabilities, as below.

## Vulnerabilities

A known vulnerability is an advisory the .NET SDK reports for the product tree, including transitive packages. Every severity the SDK reports counts. The check includes transitive packages. The skill does not pin transitive packages, and it does not add a direct package just to force a transitive version.

When the latest stable version still has a known vulnerability, or a transitive package still has one after direct packages are current, the skill tells the developer the package, whether it is direct or transitive, and the advisory, then waits. The developer decides how to handle it. The skill does not downgrade, swap a library, turn on transitive pinning, or add a suppression on its own.

## The tree this slice leaves

After this slice:

- `src/Directory.Packages.props` is the only place a product package version is declared.
- Every package the product tree already names is on the latest stable version, except a package the developer chose not to move when the skill asked.
- No `PackageReference` under `src/` carries a `Version`.
- The rule and the skill are in the repo.
- The product tree builds. Existing tests pass, except where the developer accepted an update that changes that and said how to proceed.

`Microsoft.EntityFrameworkCore.Design` keeps its `IncludeAssets` and `PrivateAssets`. `Microsoft.AspNetCore.Components.WebAssembly.DevServer` keeps `PrivateAssets="all"`. Those stay on the project reference.

## What stays the same

Directors, coaches, players, and spectators see no new product behavior from the practice itself. Tournament setup, local startup, Mailpit, and the look and feel stay as their specs describe, unless the developer accepts a breaking package update and says to change that behavior.

The target framework stays `net10.0`. If the latest stable of a package does not support `net10.0`, that update is a breaking change and the skill asks.

This slice does not add Google OR-Tools, a WorkOS client, or any other library the tree does not already use. The product intent names OR-Tools and WorkOS. Adding a package for either waits until a task specifically instructs it.

## How this is checked

- Restoring and building the product tree succeeds with central package management on.
- A `PackageReference` under `src/` that sets `Version` fails the build.
- Each package id appears once in `src/Directory.Packages.props`, at an exact stable version.
- The skill's vulnerability report includes transitive packages. Any advisory still present was handed to the developer during the run.
- The rule file is always applied, and the skill file tells an agent to ask on each breaking change before updating that package.

## Decisions this spec makes that the intent did not

The intent left these to the spec. Change any of them before a plan is written.

- **One version per id.** Two projects cannot pin different versions of the same package. A clash stops for the developer.
- **Breaking change.** A higher major version, an update that does not support `net10.0`, or a same-major update that fails the build or the existing tests. Release notes alone do not mark a same-major update as breaking when the build and tests pass.
- **Non-breaking updates.** The skill applies them without asking.
- **Each time.** A skipped breaking update is asked again on the next run.
- **No required pull request.** The developer says how to proceed for that package. The skill does not assume a separate pull request.
- **Advisories.** The .NET SDK list, all severities, direct and transitive. This slice does not turn advisories into build errors.
- **Rule is always on.** `.cursor/rules/current-packages.mdc` applies on every agent turn.
- **Deprecated packages.** A deprecation notice is not a reason to replace a library. Replacement waits for a specific instruction.

## Deliberately not in this slice

- The Node tool under `tools/logo/`.
- A packages lock file, Dependabot, or another updater.
- Pinning transitive package versions.
- Changing `TargetFramework` or the .NET SDK.
- Adding a library the user did not specifically instruct.
- Hiding a vulnerability warning with `NoWarn` unless the developer, when asked, says to.
