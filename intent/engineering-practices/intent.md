# Intent: Engineering practices
Author: developer. Status: accepted.

## Problem
Nothing in the product tree says how CourtFlow code should be written, and nothing checks it. Each project turns on nullable reference types and implicit usings. There is no shared editor config, no solution-wide build settings, no analyzer set, and warnings are not errors. The repository root has no continuous integration. Developers and agents writing the next slice can each invent a different way of working.

The accepted product direction already names the stack and the layering. It does not say what "written well" means for that stack.

## Proposed outcome
A rule at `.cursor/rules/engineering-practices.mdc` states how product code under `src/` is written. It follows current Microsoft guidance for the technologies that tree uses, except where an accepted intent already differs.

A project skill at `.cursor/skills/verify-engineering-practices/SKILL.md` can be called to verify that the changes on the branch follow that rule. When a change does not follow, the skill edits it and reports to the developer what changed.

The product code already under `src/` is brought up to that rule. Later slices inherit it.

## Affected users and systems
Developers and agents writing CourtFlow. Directors, coaches, players, and spectators see no new product behavior from the standard itself.

The product tree under `src/` today uses C# on .NET 10, an ASP.NET Core API, Blazor WebAssembly and Razor, MudBlazor, Entity Framework Core with Npgsql, ASP.NET Data Protection, WorkOS over HTTP, .NET Aspire with MailPit, and tests on xUnit v3, bunit, Testcontainers, coverlet, and Microsoft.Testing.Platform. Google OR-Tools and hosted Postgres are named in the product direction and are not in this slice. `tools/logo/` is not in this slice.

## Constraints
- Stay inside the accepted product stack and layering. This slice does not reopen Blazor, MudBlazor, Entity Framework Core, Postgres, WorkOS, Aspire, or domain-driven design.
- Screen conventions already accepted in the look-and-feel intent stay as they are. This slice does not reopen theme, layout, or component wrappers.
- NuGet version currency stays with the current-packages intent: latest stable, central package management, vulnerability handling, and that intent's update skill.
- This repo has no proof of concept. This slice does not recreate one.
- The written standard follows current Microsoft guidance except where an accepted intent already differs: Blazor styling, Aspire's service defaults and fixed ports, the session cookie in the accepted sign-in spec, and fixed US dates.
- Offline sign-in is a later intent. This slice does not put a sign-in token where page code can read it, and it does not change the session cookie.
- Bringing the code up to the rule, and the edits the verify skill makes, may change structure, project settings, and checks. Director-facing behavior, API contracts, and behavior an accepted intent already specifies stay as they are. Where a Microsoft practice would change one of those, the rule keeps the accepted behavior and the skill leaves it.
- The rule is `.cursor/rules/engineering-practices.mdc`.
- The skill is `.cursor/skills/verify-engineering-practices/SKILL.md`. It is called to verify the changes on the branch. When those changes do not follow the rule, the skill edits them and reports to the developer what changed.
- Google OR-Tools, hosted Postgres, and `tools/logo/` are out of scope.
- Every project under `src/` sets `AnalysisLevel` to latest, `AnalysisMode` to Recommended, `TreatWarningsAsErrors` to true, and `EnforceCodeStyleInBuild` to true. Info-level suggestions stay off. Generated Entity Framework migration files stay generated. A suppression needs a reason beside it. The skill does not add one to make the build pass.

## Open questions
None.
