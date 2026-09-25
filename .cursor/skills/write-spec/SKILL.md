---
name: write-spec
description: >-
  Writes a CourtFlow spec.md of requirements and design from an accepted
  intent.md. Use when the user asks for a spec, requirements, or design
  from an intent. Does not write a plan or code.
---

# Write spec

Write `spec.md` beside an accepted `intent.md`. Stop there.

1. Read the slice `intent.md` and [`intent/product/intent.md`](../../../intent/product/intent.md). If the slice status is not `accepted`, stop and say so. Do not treat a draft as accepted.
2. Read [`poc/CONTEXT.md`](../../../poc/CONTEXT.md). Read only the [`poc/docs/`](../../../poc/docs/) pages that describe this slice. The proof of concept is evidence of past behavior, not a requirement.
3. Write `spec.md` in the same folder. Cover behavior, rules, and edge cases a plan can be written against. Flag contradictions and anything the intent left open. Resolve an open question only when the user answered it in this session; otherwise leave it flagged.
4. The product intent locks the tech stack. Do not reopen it. Gaps that belong to this slice stay in that slice's intent. Flag them; do not fill them from the proof of concept.

Do not write `plan.md` or application code. Do not change [`poc/`](../../../poc/).
