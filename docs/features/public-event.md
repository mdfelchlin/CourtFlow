# Public Event Detail

## Purpose

Let unauthenticated visitors drill into a specific tournament and see its team roster, published schedule, brackets, and standings — in whatever order the organizer chooses to make those surfaces ready.

## Visibility model

This page only exists for **`Tournament`s** whose `status` is `published` or `archived`. The two visibility layers compose:

| Layer | What it gates | Source of truth |
| --- | --- | --- |
| **Tournament status** | Whether the page exists at all. `draft` → 404. `published` / `archived` → page renders. | `tournaments.status` — see [`../data-model.md`](../data-model.md#tournament-status-lifecycle), [`../../CONTEXT.md`](../../CONTEXT.md) **Tournament status**. |
| **Per-tab readiness gates** | Inside a visible page, whether each tab shows real content or the documented empty-state copy. | The materialization state of the underlying data (Teams placed, ScheduleVersion published, bracket slots resolved, matches scored). See the [Readiness gates](#per-tab-readiness-gates) table below. Implement as the **PublicEventReadiness** read model ([`../adr/0006-read-models-tiebreaker-delay-public-division-readiness.md`](../adr/0006-read-models-tiebreaker-delay-public-division-readiness.md)). |

The composition lets an organizer publish a tournament with its team roster visible **while still keeping the schedule hidden** — they publish the tournament, but defer publishing any `ScheduleVersion`. The Teams tab then shows real content; the Schedule tab shows "Schedule hasn't been published yet."

## Routes

| Route | Page | Layout |
| --- | --- | --- |
| `/events/{Id:guid}` | Public event detail with four lazy-loaded tabs | `PublicLayout` |

The route returns **HTTP 404** when the tournament's `status` is `draft` (generic — does not leak existence). `published` and `archived` both render normally; an `archived` event additionally shows an "Archived event" badge in the hero card.

## Page header / toolbar

- **Loading state:** indeterminate `MudProgressLinear`.
- **Not found state:** centered outlined paper with `SearchOff` icon, "Event not found" title, and "Back to events" link. Used for both real 404s and `status = 'draft'` responses (the API does not distinguish; the page does not need to).
- **Found state:**
  - **Breadcrumbs** (`MudBreadcrumbs`): `Events` → event name (disabled).
  - **Hero card** (outlined `MudPaper`, padding 6, no elevation):
    - Row 1: `Typo.h4` event name + display-label chip ("Upcoming" / "Live" / "Completed", computed from dates) + optional **Archived** chip when `tournaments.status = 'archived'`.
    - Row 2: organization name in `Typo.subtitle1`, secondary color.
    - Row 3: `DateRange` icon + formatted dates.
    - Optional description below the dates.
    - Stats row: three `Typo.caption`/`Typo.h6` pairs for **Venues**, **Divisions**, **Teams** counts.

## My-team filter (mobile-first lookup)

Above the tabs, a single full-width **My-team strip** lets a visitor pin a TournamentTeam (or a club) and re-scope every tab to that team's matches, pools, brackets, and standings. This is the public surface's answer to "I'm coaching 14U Black, just tell me where I'm playing next" — the single most-asked question of every public schedule page.

### Strip layout (collapsed default)

A `MudPaper` outlined, padding 2, with:

- Left: `Person` icon + caption "Filter to my team".
- Center: an `MudAutocomplete<TournamentTeamLite>` (clearable, max 50 items, placeholder "Type your team or club name"). Search matches on team name + club name (case-insensitive contains). Selecting a team writes the choice to `localStorage` under `courtflow.publicMyTeam.{tournamentId}` and re-scopes the tabs below.
- Right: **Clear** text button (`Close` icon) — hidden when no team is selected; resets the strip and tabs to "all teams".

### Strip layout (expanded — when a team is pinned)

The strip swaps to a richer summary:

- Left: team avatar (`Groups` icon) + team name in `subtitle1` + club in `caption`.
- Center column 1: **Next match** mini-card — division, scheduled start, court, "Home vs Away" (the team's own side highlighted with `font-weight: 700`), and a `+N min behind` / `Live now` / `Final` chip computed from `matches.started_at`. Tap navigates to the Schedule tab pre-scrolled to that match.
- Center column 2: **Working next** mini-card — when the team's next role is **WorkTeam** rather than playing, this card shows "Reffing on Court 3 at 10:45 AM". Surfaces a frequently-missed obligation.
- Right: **Change team** outlined button (text "Change"). Reopens the autocomplete picker.

The strip is **sticky** on scroll on viewports ≥ `sm` so a parent walking the gym always sees their team's next obligation without scrolling back up. On `xs` viewports it scrolls with the page (limited vertical real estate).

### How the filter scopes each tab

| Tab | Behavior when a team is pinned |
| --- | --- |
| **Teams** | The pinned team's division is moved to the top and visually highlighted. Other divisions remain accessible below. |
| **Schedule** | The table is pre-filtered to **only the pinned team's matches** (playing or reffing). A small `MudChip` above the table reads "Showing 6 of 247 matches for Spike Crush 14U Black" with a **Show all** affordance to clear the filter without unpinning the team. |
| **Brackets** | The bracket containing the team is moved to the top; the team's path through the tree is highlighted (a different chip color on slots the team has played or is scheduled to play). |
| **Standings** | The pool containing the team is moved to the top; the team's row is highlighted. |

When no team is pinned, every tab behaves as documented in [Page body](#page-body) below.

### Persistence

The selection persists per-tournament in `localStorage` under `courtflow.publicMyTeam.{tournamentId}`. Visitors who close the tab and reopen the event page later see their team already pinned. The selection is cleared automatically when:

- The tournament status moves to `archived` (the strip hides itself in archived view).
- The pinned TournamentTeam has `tournament_team_status` starting with `withdrawn_*` (a small `MudAlert Warning` reads "This team has withdrawn from the event" until the visitor clears or changes the team).

### Mobile-first sizing

Strip text scales: team name + next-match line use 14pt minimum to be readable at arm's length on a phone. Buttons are minimum 40×40 px touch targets. No hover-only affordances; every action works on tap.

## Page body

`MudTabs` (no elevation, outlined, rounded, padded panels) — **four** panels with **lazy loading** (each tab loads its data on first activation). Tab order is Teams → Schedule → Brackets → Standings; Teams is first because it is the most-evergreen surface (becomes available the moment the tournament has teams and publishes).

### Tab 1 — Teams (`Groups` icon)

The roster tab. Becomes available as soon as the tournament has any `TournamentTeam`s and `tournaments.status = 'published'` (or `archived`).

- Loading state: indeterminate progress bar.
- Empty state: `MudAlert Info` "Teams haven't been posted for this event yet." (Shown when the tournament has zero TournamentTeams. Rare for a published tournament.)
- Otherwise one outlined `MudPaper` per division, ordered by `tournament_divisions.name`:
  - **Header:** `Groups` icon, division name (`h6`), team-count caption ("N teams"), optional wave chip (`AM` / `PM`).
  - **Body:**
    - If the division's first pool-type Stage has reached `seeded` (i.e. `pool_entries` exist), render a per-pool sub-card: pool name + dense `MudList` of teams in `seed_in_pool` order. This is the "publish the tournament with pools visible but schedule still private" intermediate state.
    - Otherwise (no pool placements yet) render a flat dense `MudList` of teams alphabetical by name, with each row showing `TeamName · Club`.
  - Withdrawn TournamentTeams (`tournament_team_status` starts with `withdrawn_*`) are rendered with a strikethrough and a `caption` "Withdrawn" suffix.

### Tab 2 — Schedule (`CalendarViewWeek` icon)

- Loading state: indeterminate progress bar.
- Empty state: `MudAlert Info` "Schedule hasn't been published yet." Shown when there is no `ScheduleVersion` with `status = 'published'` for the tournament.
- Otherwise dense `MudTable` (`Breakpoint.Sm`) populated from the current `published` `ScheduleVersion` only with columns:
  - **Time** — local `MMM d · HH:mm`.
  - **Court** — court name + venue caption.
  - **Division** — division name + pool caption ("Pool A").
  - **Match** — `Home vs Away` with TBD fallback.
  - **Status** — chip color reflects match status (Info / Success / Default / Error).
  - **Score** — joined set scores (`H-A, H-A, ...`) or "-".

### Tab 3 — Brackets (`AccountTree` icon)

- Loading state: indeterminate progress bar.
- Empty state: `MudAlert Info` "No brackets have been generated for this event yet." Shown when no bracket-type Stage has `bracket_seed_slots` materialized in this tournament.
- Otherwise one outlined `MudPaper` per bracket containing:
  - Header: bracket icon, division name (`h6`), type chip, capacity chip ("N teams").
  - Dense `MudTable` of nodes ordered by Round/Slot with columns: **Round**, **Slot**, **Branch**, **Matchup**. Each side renders the resolved team name when `bracket_seed_slots.resolved_tournament_team_id` is set; otherwise the placeholder label from the upstream rule (`Pool A · 1st`, `Gold-3 ← manual`, `Seed N`, or `TBD`).

### Tab 4 — Standings (`Leaderboard` icon)

- Loading state: indeterminate progress bar.
- Empty state: `MudAlert Info` "Standings appear once pool matches are scored." Shown when no `matches` in any pool-type Stage have `match_status = 'final'`.
- Otherwise one outlined `MudPaper` per pool:
  - Header: leaderboard icon, division name (`h6`), pool chip ("Pool A").
  - Dense table with columns: `#`, **Team**, **MP**, **W**, **L**, **Sets** (`SW-SL`), **Set %**, **Point %**. Row order is the resolved order from the division's **TiebreakerPreset** chain — see [`standings.md`](standings.md) for admin-side semantics; the public view does not surface the preset name.

### Per-tab readiness gates

| Tab | Visible content when | Empty-state copy | Data source |
| --- | --- | --- | --- |
| **Teams** | The tournament has `≥ 1` `tournament_teams` row. (Effectively always for a useful published tournament.) | "Teams haven't been posted for this event yet." | `tournament_teams`, optionally enriched with `pool_entries` per division. |
| **Schedule** | At least one `ScheduleVersion` for the tournament has `status = 'published'`. The public reads only that version. | "Schedule hasn't been published yet." | `schedule_versions` (where `status = 'published'`) + its `match_assignments`. |
| **Brackets** | At least one bracket-type Stage has its `bracket_seed_slots` row set materialized (regardless of whether `resolved_tournament_team_id` is filled — the public can see the *shape* with TBD slots before pool play completes, which is intentional). | "No brackets have been generated for this event yet." | `brackets`, `bracket_seed_slots`. |
| **Standings** | At least one match in any pool-type Stage has `match_status = 'final'`. | "Standings appear once pool matches are scored." | Computed live from `matches` + `match_sets` per [`../data-model.md`](../data-model.md#standings-tally-per-pool-per-tournamentteam). |

Organizers control public visibility per surface by **controlling the upstream data, not by toggling visibility flags**: don't publish a `ScheduleVersion` if you want the Schedule tab hidden; don't run **Generate brackets** if you want the Brackets tab hidden; etc. The single explicit toggle is `tournaments.status` (the master gate). Per-tab explicit toggles are deferred to V2.

## Interactions

- Initial load: `GET /public/events/{id}` populates the hero card and breadcrumbs. Returns 404 when `tournaments.status = 'draft'` — the page shows the not-found card.
- Activating a tab triggers a one-time fetch:
  - Teams: `GET /public/events/{id}/teams` — returns divisions, teams (per division), and optional pool placements when materialized.
  - Schedule: `GET /public/events/{id}/schedule` — returns the assignments from the current `published` `ScheduleVersion`, or `204 No Content` when none is published yet.
  - Brackets: `GET /public/events/{id}/brackets` — returns brackets and slot maps for all bracket Stages that have `bracket_seed_slots` materialized.
  - Standings: `GET /public/events/{id}/standings` — returns the live standings for every pool whose stage has at least one `final` match.
- **My-team filter:** the autocomplete is sourced from the Teams tab's payload (reused — no extra fetch). The "Next match" / "Working next" mini-cards are computed client-side from the Schedule tab's payload once both are loaded; until the Schedule tab is activated, the expanded strip shows "Schedule not posted yet" in place of the mini-cards.
- "Back to events" link returns to `/`.

## Dialogs

None.

## References

- Sibling page: [`public-landing.md`](public-landing.md).
- Admin sibling that controls visibility: [`tournaments.md`](tournaments.md) (publish status strip), [`schedule.md`](schedule.md) (which `ScheduleVersion` is `published`), [`division-detail.md`](division-detail.md) (whether bracket slots have been materialized).
- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md) (PublicLayout).
- Data: [`../data-model.md`](../data-model.md#tournament-status-lifecycle) (visibility gate), [`../data-model.md`](../data-model.md#9-scheduling-and-assignment) (ScheduleVersion lifecycle), [`../data-model.md`](../data-model.md#7-bracket-configuration-and-advancement-inside-a-bracket-stage) (bracket slot materialization), [`../data-model.md`](../data-model.md#10-score-reporting-and-winner-resolution) (standings).
- Glossary: [`../../CONTEXT.md`](../../CONTEXT.md) — **Tournament status**, **Event**, **TournamentTeam**, **BracketSeed**.
