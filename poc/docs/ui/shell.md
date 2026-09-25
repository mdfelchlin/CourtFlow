# App Shell

Information architecture, layouts, navigation, and the cross-cutting chrome that wraps every page. Visual baseline lives in [`design-system.md`](design-system.md). Modal dialogs are catalogued in [`dialogs.md`](dialogs.md).

---

## 1. Information architecture

### 1.1 Public routes (no authentication required)

| Route | Page | Layout |
| --- | --- | --- |
| `/` | Public landing — current & upcoming events grid | `PublicLayout` |
| `/events/{Id:guid}` | Public event detail — Teams / Schedule / Brackets / Standings tabs | `PublicLayout` |
| `/login` | Sign-in redirect (Clerk) | `PublicLayout` |
| `/logout` | Sign-out then redirect to `/` | (no chrome) |

### 1.2 Admin routes (authenticated)

| Route | Page | Notes |
| --- | --- | --- |
| `/admin` | Redirects to `/tournaments` | Convenience entry point |
| `/tournaments` | Tournaments table | Default landing after sign-in |
| `/tournaments/new` | Tournament editor (create) | Single page used for both new and existing |
| `/tournaments/{Id:guid}` | Tournament editor (edit) | Includes Venues & Courts panel |
| `/divisions` | Divisions table | See [`../features/divisions.md`](../features/divisions.md) |
| `/divisions/{Id:guid}` | Division detail — stages, pools, brackets, rankings | See [`../features/division-detail.md`](../features/division-detail.md) |
| `/teams` | Teams table with filters | |
| `/imports` | CSV import workflow | |
| `/schedule` | Court × time-slot grid | |
| `/brackets` | Tabbed bracket viewer per division | |
| `/matches` | Flat matches list with scoring | |
| `/standings` | Pool standings per division | |
| `/repairs` *(planned)* | Repair Job history | See [`../features/repairs.md`](../features/repairs.md) |
| `/court-status` *(planned)* | Court Status & Delay Dashboard | See [`../features/court-status.md`](../features/court-status.md) |

WorkTeam editing is **not** a top-level route — it lives inside `/divisions/{Id}` (Work rotation panel), the Schedule cells, and the Matches rows. See [`../features/work-teams.md`](../features/work-teams.md).

Print outputs are **not** routes — they are alternate stylesheets activated by **Print** buttons on existing pages. See [`../features/print-outputs.md`](../features/print-outputs.md).

### 1.3 Navigation rules

- All admin routes carry `[Authorize]`. Unauthenticated requests render `RedirectToLogin`, which sends the user to `/login?returnUrl={current path}`.
- The public landing (`/`) is the default route for unauthenticated traffic.
- The `not-found` route renders inside the admin shell with a simple "Page not found" message.

---

## 2. PublicLayout (unauthenticated)

- **App bar** (`Dense`, primary color):
  - Logo image (`/CourtFlow.png`, 44 × 44, 4 px radius) and "CourtFlow" title link to `/`.
  - Right-aligned: theme toggle, then either:
    - **Authorized:** "Admin" button (links to `/tournaments`), user display name, "Logout" button.
    - **Not authorized (and not on `/login`):** filled "Sign in" button (secondary color) linking to `/login?returnUrl=/tournaments`.
- **Main content:** centered container with `MaxWidth.Large`, `py-6` padding.
- No drawer.

---

## 3. AppShell / MainLayout (authenticated admin)

The Web App project uses `MainLayout` (which composes the same chrome as the shared `AppShell`) so the tournament selector and route remount logic are available.

- **App bar** (full size, primary color, elevation 1):
  - **Left:** menu icon button (toggles drawer), then logo image + "CourtFlow" title link to `/`.
  - **Right (in order):**
    1. **Header tournament selector** (`HeaderTournamentSelect`) — see §5. Hidden when the active route is `/tournaments` or `/tournaments/...` to avoid duplicating that screen's primary navigation.
    2. Theme toggle icon button.
    3. User display name (`MudText body2`, 12 px horizontal margin).
    4. "Logout" text button with logout icon, links to `/logout`.
  - For unauthenticated users: same chrome but with a "Login" button instead.
- **Drawer** (`Variant.Mini`, `OpenMiniOnHover=true`, clip mode `Always`, elevation 2): renders `NavMenu` (see §4).
- **Main content:** centered container with `MaxWidth.ExtraExtraLarge`, `py-4` padding. The router's body is wrapped in a `<div>` keyed on the active tournament id, so changing the tournament fully remounts the route and reloads its data.
- **Error UI:** static `<div id="blazor-error-ui">` with reload link and dismiss button, hidden by Blazor framework CSS unless an unhandled error occurs.
- **Snackbar provider, Dialog provider, Popover provider, Theme provider** are mounted at the layout root.

---

## 4. NavMenu (drawer contents)

Visible only when authorized. List of links, each with an icon:

1. **Tournaments** — `EmojiEvents` — `/tournaments` (`NavLinkMatch.All`)
2. **Divisions** — `Category` — `/divisions`
3. **Teams** — `Groups` — `/teams`
4. **Imports** — `UploadFile` — `/imports`
5. **Schedule** — `CalendarViewWeek` — `/schedule`
6. **Brackets** — `AccountTree` — `/brackets`
7. **Matches** — `SportsScore` — `/matches`
8. **Standings** — `Leaderboard` — `/standings`

Planned (post-V1): **Repairs** — `Handyman` — `/repairs`; **Court Status** — `Dashboard` — `/court-status`.

---

## 5. Header tournament selector (`HeaderTournamentSelect`)

- Compact `MudSelect` rendered in the app bar with leading "Tournament" label.
- Bound to `SelectedTournamentStore.SelectedTournamentId`.
- Lists all tournaments returned by the API. Sorted as the API returns them; the default selection is the most recent tournament by `StartDate, EndDate, Name`.
- Persists the selection to `localStorage` under `courtflow.selectedTournamentId`. The store rehydrates the selection on first interactive render and re-applies the default on every `EnsureInitializedAsync()` call when no valid selection is set.
- Hidden on tournament list/edit routes to avoid redundancy with the page's own tournament context.
- Changing the selection raises `Changed` on the store, which causes `MainLayout` to remount the routed content and forces a reload of any tournament-scoped data.

---

## 6. Tournament context propagation

- `SelectedTournamentStore` is a scoped service shared by every admin page that needs a tournament context.
- Pages call `await TournamentStore.EnsureInitializedAsync()` in `OnInitializedAsync`, then load tournament-scoped data using `TournamentStore.SelectedTournamentId`.
- The admin shell remounts the routed content on tournament change so each page can run its own load logic without bespoke change subscriptions.
- Persistence: the selection lives in `localStorage` under `courtflow.selectedTournamentId`; on reload or tab navigation the most recent tournament is selected if no value is stored, the stored value is no longer valid, or the selection was cleared.

---

## 7. Cross-cutting providers

Every layout mounts:

- `MudThemeProvider` (binds to `ThemeState`).
- `MudPopoverProvider`.
- `MudDialogProvider`.
- `MudSnackbarProvider`.

`SelectedTournamentStore` is cascaded from the layout via `<CascadingValue>` so any page can pull it as `[CascadingParameter]`.

---

## 8. Auth flow chrome

- `AuthorizeRouteView` shows the `Authorizing` slot (indeterminate progress + "Preparing sign-in…") while auth state is resolving.
- If unauthorized, renders `RedirectToLogin`, which navigates to `/login?returnUrl={current path}`.

---

## 9. Route-aware app bar

The header tournament selector is hidden on `/tournaments` and `/tournaments/...` routes to avoid duplicating the page's primary axis of navigation.
