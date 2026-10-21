# Intent: Current packages
Author: developer. Status: accepted.

## Problem
Nothing keeps CourtFlow on the latest stable NuGet packages, and nothing checks that a package in use is free of known vulnerabilities. Each product project pins its own versions. Developers and agents can add a prerelease, leave an older stable version in place, pull in a library nobody asked for, or keep a package after a vulnerability is published, including one that arrives only through a transitive package. There is no rule for adding packages, and no skill brings the product tree up to date.

## Proposed outcome
A rule at `.cursor/rules/current-packages.mdc` covers adding packages and the limits that go with them. A package version is declared once in `src/Directory.Packages.props` through NuGet Central Package Management. Every NuGet package the product tree names is on the latest stable version. Any stable release counts. Prerelease packages are not used. A library is added only when specifically instructed, and that addition uses the latest stable version. A known vulnerability counts for a package the product tree names and for a transitive package. When the latest stable release still has a known vulnerability, a developer decides how to handle it.

A project skill at `.cursor/skills/update-packages/SKILL.md` brings every package up to date. Each time a latest version is a breaking change, the skill asks the developer about that change and how to proceed.

The product tree already under `src/` is brought up to date on that practice.

## Affected users and systems
Developers and agents changing CourtFlow. Directors, coaches, players, and spectators see no new product behavior from the practice itself.

The product tree under `src/` pins NuGet versions in each project file: the API, the Blazor WebAssembly app, the domain, application, and infrastructure libraries, the Aspire app host, and their tests. There is no Central Package Management, no rule for adding packages, and no skill that brings those packages up to date. This slice updates that tree.

## Constraints
- The rule is `.cursor/rules/current-packages.mdc`. It covers adding a package and the limits that go with it. A library is not added unless specifically instructed. When one is added, it is the latest stable. Prerelease is not used.
- NuGet Central Package Management in `src/Directory.Packages.props`. A package version is declared once, not pinned again in each project.
- Latest stable release of each NuGet package the product tree names. Any stable release counts.
- A known vulnerability counts whether the package is named in a project file or pulled in transitively.
- When the latest stable release still has a known vulnerability, a developer decides how to handle it.
- The skill is `.cursor/skills/update-packages/SKILL.md`. It brings the packages up to date by updating their versions. Each time a latest version is a breaking change, the skill asks the developer about that change and how to proceed.
- The product tree already under `src/` is brought up to date.
- Stay inside the accepted product stack. Updating a package does not replace Blazor, MudBlazor, Entity Framework Core, Postgres, WorkOS, Aspire, or domain-driven design.

## Open questions
None.
