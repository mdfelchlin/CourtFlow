---
name: write-plan
description: >-
  Writes a CourtFlow plan.md for implementing an approved spec.md. Use when
  the user asks to plan implementation of a spec. Names files and tests
  under src/. Does not write application code.
---

# Write plan

Write `plan.md` beside an existing `spec.md`. Stop there.

1. Read that `spec.md` and its `intent.md`. If `spec.md` is missing, stop and use the write-spec skill instead of inventing the spec inside the plan.
2. List the files to add or change under `src/`. Name the order of changes and the tests that prove each behavior in the spec.
3. Call out spec items with no test.

Do not edit application code.
