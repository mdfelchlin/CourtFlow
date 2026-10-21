---
name: verify-engineering-practices
description: >-
  Verifies that the changes on the current branch follow the CourtFlow
  engineering-practices rule. Use when the user asks to verify the branch,
  check engineering practices, or run verify-engineering-practices.
---

# Verify engineering practices

Check the changes on the current branch against `.cursor/rules/engineering-practices.mdc`. When a change does not follow the rule, edit it so it does, then report each changed file.

## Steps

1. Read `.cursor/rules/engineering-practices.mdc`.
2. Diff the current branch against the repository's default branch, and include uncommitted changes under `src/`.
3. Check those changed files against the rule. Leave files the branch does not touch, except a warning this edit caused, which you fix and report.
4. Edit a violation when the edit is structure, project settings, or checks. `dotnet format src/CourtFlow.slnx` applies the style rules the build enforces. Do not change director-facing behavior, an API contract, or behavior an accepted spec specifies. Stop without editing when the fix would change accepted behavior, and tell the developer the file, the diagnostic, and why.
5. This skill does not move a type from one project to another. Report a layering miss and let the developer decide.
6. Restore a branch change that adds `*.razor.css`, a service-defaults project, a WorkOS token page code can read, or a director-facing calendar date that does not use `MM/dd/yyyy`.
7. Keep `src/Directory.Build.props` with `AnalysisLevel` latest, `AnalysisMode` Recommended, `TreatWarningsAsErrors` true, and `EnforceCodeStyleInBuild` true. Do not set `AnalysisMode` to `All` or raise suggestion rules to warning.
8. Do not hand-edit `src/CourtFlow.Infrastructure/Migrations/`. Do not add a suppression to make the build pass.
9. Do not edit `tools/logo/`. Do not add Google OR-Tools or hosted Postgres. Do not change package versions. Package versions are the `update-packages` skill.

## Report

Report each changed file and what about it changed. When the branch already follows the rule, say that and list no edits.
