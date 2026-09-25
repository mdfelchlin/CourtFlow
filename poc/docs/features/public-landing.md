# Public Landing

## Purpose

Invite the public to find the volleyball tournament they care about. Read-only; no auth required.

## Routes

| Route | Page | Layout |
| --- | --- | --- |
| `/` | Public events landing | `PublicLayout` |

## Page header / toolbar

1. **Hero panel.** Full-width `MudPaper` (no border, 16 px radius, custom gradient background `#245F73 → #1E4F60`, light text `#F2F0EF`).
   - Title: "Volleyball tournaments, live." (`Typo.h3`, weight 700).
   - Subtitle: "Browse current and upcoming events, follow schedules, and check standings in real time." (max-width 640 px, slightly transparent).
2. **Section header row.** "Current & upcoming events" (`Typo.h5`) on the left; **Refresh** icon button on the right.

## Visibility model

This page is the public-visibility surface. The list reflects **only `Tournament`s** whose `status` is `published` or `archived`. `draft` tournaments are not returned by the API and cannot be reached via deep link. See [`../data-model.md`](../data-model.md#tournament-status-lifecycle) and [`../../CONTEXT.md`](../../CONTEXT.md) **Tournament status**.

- `GET /public/events` returns rows where `tournaments.status IN ('published', 'archived')`. The response carries `status` so the client can split the rendering.
- The hero card display label ("Upcoming" / "Live" / "Completed") is computed from `start_date` / `end_date` vs. today — it is a **display label**, not a status. A `published` tournament can be Upcoming, Live, or Completed; an `archived` tournament is shown in a separate Past Events section regardless of its date computation.

## Page body

- **Loading state:** indeterminate `MudProgressLinear` while events load.
- **Empty state (no current/upcoming events):** centered card containing `EventBusy` icon (large, secondary color), title "No events scheduled right now", and copy "Check back soon — new tournaments are added regularly." Shown when there are no `published` rows; Past Events may still render below if there are archived rows.
- **Events grid (`MudGrid`):** responsive cards, 1 column on xs, 2 on sm, 3 on md+, populated from `published` rows. Each `MudCard` (outlined, full height, flex column, cursor pointer):
  - **Header:**
    - Title: event name (`Typo.h6`).
    - Subtitle: organization name (`caption`, secondary color).
    - Header actions: display label `MudChip` — "Upcoming" (Info), "Live" (Success), or "Completed" (Default) computed from today vs. start/end dates.
  - **Content (flex grow):**
    - Date row: `DateRange` icon + formatted date range. Same-day events show one date; same-month ranges collapse to "MMM d-d, yyyy"; multi-month ranges spell out both dates.
    - Optional description (truncated to 120 chars + "...").
    - Stat row at bottom with three pairs of (caption, value): **Venues**, **Divisions**, **Teams**.
  - **Actions:** text button "View event" with forward arrow.
- **Past events section** (visible only when at least one `archived` row exists): a divider, section header "Past events" (`Typo.h5`), and the same card grid populated from `archived` rows. The status chip on each card is replaced with a muted "Archived" chip. Organizations that prefer to hide archived events entirely (per organization settings) cause the API to omit `archived` rows; the section then doesn't render.

## Interactions

- Refresh icon button reloads the list from `GET /public/events`.
- Clicking anywhere on a card or its action button navigates to `/events/{Id}` (works for both `published` and `archived`).
- API failure during load: silently shows the empty state; no error chrome.

## Dialogs

None.

## References

- Sibling page: [`public-event.md`](public-event.md).
- Admin sibling: [`tournaments.md`](tournaments.md) (where organizers control publish status).
- Cross-cutting: [`../ui/design-system.md`](../ui/design-system.md), [`../ui/shell.md`](../ui/shell.md) (PublicLayout).
- Data: [`../data-model.md`](../data-model.md#2-tournament-creation), [`../data-model.md`](../data-model.md#tournament-status-lifecycle) (visibility gate).
- Glossary: [`../../CONTEXT.md`](../../CONTEXT.md) — **Tournament status**, **Event**.
- Product: [`../product-overview.md`](../product-overview.md) §1.5 (public visitor persona).
