# Intent home

This folder is where CourtFlow work starts. Each change is a directory with an `intent.md` written from [`_template.md`](_template.md). The north star lives in [`product/intent.md`](product/intent.md): what the application is, who it is for, the tech stack, and the pillars of value. Later slices get their own folder, for example `intent/schedule/intent.md`.

An accepted `intent.md` is followed by a `spec.md` in the same folder (requirements and design), then a `plan.md` (how to change the codebase), then the code. Project skills for those steps are `capture-intent`, `write-spec`, and `write-plan` under [`.cursor/skills/`](../.cursor/skills/). New application code belongs under [`src/`](../src/).
