---
name: verify-engineering-practices
description: >-
  Verifies that the changes on the current branch follow the CourtFlow
  engineering-practices rule. Use when the user asks to verify the branch,
  check engineering practices, or run verify-engineering-practices.
---

# Verify engineering practices

Check the changes on the current branch against `.cursor/rules/engineering-practices.mdc`. When a change does not follow the rule, edit it so it does, then tell the developer what changed.

## Steps

1. Read `.cursor/rules/engineering-practices.mdc`.
2. Diff the current branch against the repository's default branch, and include uncommitted changes under `src/`.
3. Check those changed files against the rule. Leave files the branch does not touch.
4. Edit a violation when the edit is structure, project settings, or checks. Leave director-facing behavior, API contracts, and behavior an accepted intent specifies. When Microsoft guidance would change one of those, do not edit it, and tell the developer why.
5. Keep `AnalysisLevel` latest, `AnalysisMode` Recommended, `TreatWarningsAsErrors` true, and `EnforceCodeStyleInBuild` true on projects under `src/`. Fix warnings from the compiler, the Recommended analyzers, and enforced C# style. Leave info-level suggestions. Do not hand-edit generated Entity Framework migration files. Do not add a suppression to make the build pass.
6. Do not edit `tools/logo/`. Do not add Google OR-Tools or hosted Postgres. Do not change package versions; that is the `update-packages` skill.

## Report

Tell the developer what changed. Name each file and what about it changed. When the branch already follows the rule, say that and list no edits.
