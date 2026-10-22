---
name: start-intent
description: >-
  Creates a git worktree for one CourtFlow intent. Asks the developer for a
  summary, stubs a draft intent.md from it, pulls the latest main, and
  branches feat/{intent-name} from that main. Then asks if they want that
  worktree added to the current workspace. Use when the user asks to start
  an intent, create a worktree for a slice, or branch feat/{intent-name}.
  Does not write a spec, a plan, or code, and does not commit.
---

# Start intent

Create one worktree for one intent. Pull the latest `main`, branch `feat/{intent-name}` from it, and stub `intent/{intent-name}/intent.md` from the developer's summary. Then ask if they want that worktree added to the current workspace.

## Ask first

1. `{intent-name}` is the slice folder: kebab-case, named for the outcome (`schedule-reflow`, not `blazor-grid`). If the user did not give one, ask. If they gave a phrase, derive the kebab-case name.
2. `git fetch origin`. This does not move any local branch.
3. If `intent/{intent-name}/intent.md` exists on `origin/main`, skip the summary. Leave that file as it is.
4. Otherwise ask the developer for a summary of the slice and wait. If this conversation already contains that summary, use it and do not ask again.
5. State the branch `feat/{intent-name}` and the worktree path, then create them.

## Worktree

The main worktree is the checkout `git worktree list` shows on `main`. The new worktree is a sibling of that checkout, named `CourtFlow-{intent-name}`.

1. In the main worktree, run `git pull --ff-only origin main`.
2. If that pull does not fast-forward, stop and say why. Do not stash, reset, or force.
3. If branch `feat/{intent-name}` already exists, stop and say so.
4. If the sibling path already exists, or another worktree is already on this intent, stop and say so.
5. Run `git worktree add -b feat/{intent-name} <sibling>/CourtFlow-{intent-name} main`.

## Stub

In the new worktree, when `intent/{intent-name}/intent.md` is missing, write it from [`intent/_template.md`](../../../intent/_template.md).

- Read [`intent/product/intent.md`](../../../intent/product/intent.md). The slice must fit the accepted product direction.
- Status stays `draft`. Author is who asked. When this conversation does not name them, use `git config user.name`.
- Use the summary for **Problem** and **Proposed outcome**. Put only limits the summary already stated in **Constraints**. Put anything still undecided, including technology choices, in **Open questions**.
- Leave a section empty of invented detail when the summary did not cover it.

Do not write `spec.md`, `plan.md`, or application code. Do not commit.

## Workspace

Ask the developer if they want the new worktree added to the current workspace. Wait for the answer. If this conversation already answers that, use it and do not ask again.

When they say yes, run `cursor -a <worktree-path>`. That adds the folder to the last active Cursor window. When they say no, leave the workspace unchanged.

If that path is already a workspace folder, do not ask.

## Report

Tell the developer the worktree path, the branch `feat/{intent-name}`, whether `intent.md` was stubbed or was already on `main`, and whether the worktree was added to the current workspace.
