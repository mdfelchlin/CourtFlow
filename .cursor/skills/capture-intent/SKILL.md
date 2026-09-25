---
name: capture-intent
description: >-
  Writes a CourtFlow intent.md for a new slice from intent/_template.md.
  Use when the user describes a problem, asks to capture an idea, start a
  slice, or write an intent. Does not write a spec or code.
---

# Capture intent

Write `intent/<slice>/intent.md`. Stop there.

1. Read [`intent/_template.md`](../../../intent/_template.md) and [`intent/product/intent.md`](../../../intent/product/intent.md). The slice must fit the accepted product direction.
2. Name `<slice>` in kebab-case from the outcome, not the implementation (`schedule-reflow`, not `blazor-grid`).
3. If an `intent.md` already exists for that slice, edit it. Do not create a second copy.
4. Fill the template in the originator's terms. Status stays `draft` until the user accepts it.
5. Put technology choices in **Open questions**, not **Constraints**, unless the user called one non-negotiable.

Do not write `spec.md`, `plan.md`, or application code. Do not change [`poc/`](../../../poc/).
