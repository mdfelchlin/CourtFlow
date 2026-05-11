# Design System

The visual and interaction baseline used by every CourtFlow screen. Anything not specified here defaults to MudBlazor.

Per-feature PRDs in [`../features/`](../features/) and the shell spec in [`shell.md`](shell.md) reference this document.

> **Styling stack:** Plain CSS (`wwwroot/css/app.css`) + Blazor scoped CSS (`*.razor.css`) + MudBlazor's runtime palette variables + a thin `--cf-*` custom-property layer for derived semantic tints. **No SASS / PostCSS / Tailwind.** Rationale recorded in [`../adr/0002-styling-strategy.md`](../adr/0002-styling-strategy.md). Components never inline `rgba(...)` or `color-mix(...)` literals — they reference tokens from §2 below.

---

## 1. Component framework

The UI is built on **MudBlazor**. All custom screens compose MudBlazor primitives (`MudAppBar`, `MudDrawer`, `MudPaper`, `MudDataGrid`, `MudTable`, `MudDialog`, `MudSnackbar`, etc.). No second component library is used.

---

## 2. Color palette

### Light theme

| Role | Hex |
| --- | --- |
| Primary | `#245F73` (deep teal) |
| Primary darken | `#1E4F60` |
| Primary lighten | `#3D778C` |
| Secondary | `#733E24` (warm rust) |
| Background | `#F2F0EF` (warm off-white) |
| Surface | `#FFFFFF` |
| App bar background | `#245F73` |
| App bar text | `#F2F0EF` |
| Text primary | `#1F2A30` |
| Text secondary | `#5D6670` |
| Success | `#2E7D32` |
| Info | `#245F73` |
| Warning | `#F59E0B` |
| Error | `#D32F2F` |

### Dark theme

| Role | Hex |
| --- | --- |
| Primary | `#1E4F60` |
| Secondary | `#A56747` |
| Background | `#1D2327` |
| Surface | `#2A3237` |
| App bar / drawer background | `#1E4F60` / `#232B30` |
| Text primary | `#F2F0EF` |
| Text secondary | `#D6D8D7` |
| Lines / dividers | `#3A444A` |

### Semantic surface tints

The Light / Dark palettes above are MudBlazor's `Palette` properties — set in C# (`ThemeState.BuildTheme()`) and emitted at runtime as CSS custom properties (`--mud-palette-primary`, `--mud-palette-warning`, etc.). Anywhere a screen needs **a tinted surface, a striped pattern, or a saturation-bumped variant** of a palette color, it goes through a `--cf-*` token defined once in `wwwroot/css/app.css`. Components never inline `rgba(...)` or `color-mix(...)` literals — they reference tokens.

This gives us one source of truth for the brand palette (the C# theme) and one place to override behavior for **TV mode**, **print**, or future skins (one `:root` block flip, not a component rewrite).

#### Token catalog

| Token | Resolves to | Used by |
| --- | --- | --- |
| `--cf-surface-0` | `var(--mud-palette-background-grey)` | Court Status Grid em-dash cells; empty-state surfaces |
| `--cf-surface-1` | `var(--mud-palette-surface)` | Court Status Grid future-on-schedule cells |
| `--cf-tint-success-pale` | `color-mix(in srgb, var(--mud-palette-success) 12%, transparent)` | "Live · on time" cell background |
| `--cf-tint-info-pale` | `color-mix(in srgb, var(--mud-palette-info) 14%, transparent)` | "Live · ahead" cell background |
| `--cf-tint-warning-pale` | `color-mix(in srgb, var(--mud-palette-warning) 14%, transparent)` | "Future · likely to slip" / "Live · 2–10 min behind" |
| `--cf-tint-warning-solid` | `color-mix(in srgb, var(--mud-palette-warning) 55%, transparent)` | "Live · 11–20 min behind" |
| `--cf-tint-error-solid` | `color-mix(in srgb, var(--mud-palette-error) 65%, transparent)` | "Live · > 20 min behind" |
| `--cf-tint-success-solid` | `color-mix(in srgb, var(--mud-palette-success) 70%, transparent)` | `final` cells (slightly de-emphasized via opacity 90%) |
| `--cf-wave-band-am` | `color-mix(in srgb, var(--mud-palette-info) 4%, transparent)` | AM wave row background in Grid view |
| `--cf-wave-band-pm` | `color-mix(in srgb, var(--mud-palette-secondary) 4%, transparent)` | PM wave row background |
| `--cf-wave-band-numeric` | `color-mix(in srgb, currentColor 3%, transparent)` | Waves `1` / `2` row background |
| `--cf-stripe-outage` | `repeating-linear-gradient(45deg, transparent 0 6px, color-mix(in srgb, var(--mud-palette-text-disabled) 25%, transparent) 6px 12px)` | Court outage cell pattern |
| `--cf-stripe-canceled` | `repeating-linear-gradient(45deg, transparent 0 6px, color-mix(in srgb, var(--mud-palette-error) 20%, transparent) 6px 12px)` | Canceled / forfeited cell pattern |
| `--cf-division-1` … `--cf-division-8` | Fixed hex (see [Division color derivation](#division-color-derivation) below) | Division chips and accents — selected by `divisionColor(divisionId)` helper |

#### TV mode override

Court Status's [TV mode](../features/court-status.md#tv-mode) sets `data-cf-tv-mode="true"` on the document root, which redefines two tokens with higher saturation to compensate for projector contrast loss:

```css
[data-cf-tv-mode="true"] {
  --cf-tint-warning-solid: color-mix(in srgb, var(--mud-palette-warning) 65%, transparent);
  --cf-tint-error-solid:   color-mix(in srgb, var(--mud-palette-error)   75%, transparent);
}
```

No other tokens change in TV mode — the rest are already adequately readable from across a gym.

#### Print override

All tint tokens collapse to `transparent` under `@media print` so artifacts print without ink-wasting fills. Wave bands and stripes likewise disappear. The text content (clock chip, WorkTeam caption, team names) remains intact because it's never expressed as a tint.

```css
@media print {
  :root {
    --cf-tint-success-pale: transparent;
    --cf-tint-info-pale: transparent;
    --cf-tint-warning-pale: transparent;
    --cf-tint-warning-solid: transparent;
    --cf-tint-error-solid: transparent;
    --cf-tint-success-solid: transparent;
    --cf-wave-band-am: transparent;
    --cf-wave-band-pm: transparent;
    --cf-wave-band-numeric: transparent;
    --cf-stripe-outage: none;
    --cf-stripe-canceled: none;
  }
}
```

See [`../features/print-outputs.md`](../features/print-outputs.md) for the rest of the print stylesheet contract.

#### Why CSS custom properties and not SASS

The brand palette already lives in C# (`ThemeState.BuildTheme()`) and is the single source of truth. Adding SASS would create a second source — duplicated `$primary: #245F73`-style variables in `.scss` partials that drift the moment someone tweaks the C# theme. Modern CSS (`color-mix()`, nesting, custom properties, `@layer`) covers everything we'd reach for a preprocessor for; the Blazor toolchain stays single-tool (`dotnet`) with no Node, no `webcompiler`, no watch process. See [`../adr/0002-styling-strategy.md`](../adr/0002-styling-strategy.md) for the full decision record.

### Division color derivation

CourtFlow does **not** store a color column on `tournament_divisions` (or any other entity). Per-division colors used in chips, the Court Status Grid view, brackets, and standings are **derived deterministically from `tournament_divisions.id`** at render time. The data model stays free of UI concerns; the UI stays consistent across sessions and devices because the same id always maps to the same swatch.

- **Palette** (8-color rotation, chosen for accessible contrast against both light and surface backgrounds, distinct from the Success / Warning / Error chip colors so status reading is unaffected):
  1. `#3D778C` (slate teal)
  2. `#A56747` (rust)
  3. `#7E6BC4` (lavender)
  4. `#4F8A52` (sage)
  5. `#C97E3C` (amber)
  6. `#B85C7A` (rose)
  7. `#5B7F9B` (steel blue)
  8. `#6E8E76` (moss)
- **Selection function:** `paletteIndex = hash(divisionId) mod 8` where `hash` is any stable, fast string hash (FNV-1a is the default reference implementation). The mapping is exposed by a single client-side helper `divisionColor(divisionId)` that every consumer must use; no component computes the color inline.
- **Contrast pairing:** each palette entry pairs with a text color in the helper output (`{ background, foreground }`). Foregrounds are picked to maintain WCAG AA against the background in both themes.
- **No override path in V1.** If a customer eventually demands brand-matched division colors, the right move is to add an optional `tournament_divisions.color_token` then — and only then — wire it as an override before the derivation. Until that need is concrete, we don't carry the column.

This rule is the canonical answer to "what color is this division?" everywhere in the app — Court Status Grid wave bands and cells, Schedule cell accent, Brackets header, Standings table row stripe, public Teams list chip.

---

## 3. Typography

- Font stack: `Inter, "Segoe UI", Roboto, Helvetica, Arial, sans-serif`.
- Base size: `0.95rem`, weight `400`, line height `1.5`.
- `H4` / `H6`: weight `600`.
- Buttons: weight `600`, `text-transform: none` (sentence case, never uppercase).

---

## 4. Layout properties

- Default border radius: `10px` (cards, dialogs, paper, buttons).
- Standard density: **dense** for tables, selects, and numeric fields. Forms use `Margin.Dense` and `Variant.Outlined`.
- App container max-width on admin screens: `MaxWidth.ExtraExtraLarge` with `py-4` vertical padding.
- App container max-width on public screens: `MaxWidth.Large` with `py-6` vertical padding.

---

## 5. Iconography

All icons come from MudBlazor's Material Icons (`Icons.Material.Filled.*`).

| Concept | Icon |
| --- | --- |
| Tournament | `EmojiEvents` |
| Division | `Category` |
| Team | `Groups` |
| Import | `UploadFile` |
| Schedule | `CalendarViewWeek` |
| Bracket | `AccountTree` |
| Match / scoring | `SportsScore`, `Scoreboard` |
| Standings | `Leaderboard` |
| Court | `SportsVolleyball` |
| Move match | `SwapHoriz` |
| Add | `Add` |
| Edit | `Edit` |
| Delete | `Delete` |
| Refresh | `Refresh` |
| Light/dark | `LightMode` / `DarkMode` |
| Auth | `Login` / `Logout` |
| Manage division | `Dashboard` |
| Generate | `AutoAwesome` |
| Conflict check | `VerifiedUser` |
| Save | `Save` |
| Back | `ArrowBack` |
| Forward | `ArrowForward` |
| Date | `DateRange` |
| Empty events | `EventBusy` |
| Not found | `SearchOff` |
| Info | `Info` |

---

## 6. Theming

- Light is the default theme. Dark mode is toggled via the icon button in the app bar.
- Preference is persisted to a cookie named `courtflow-theme` (values `light` / `dark`) so the very first SSR paint already matches the user's choice.
- `ThemeState` is a scoped service injected into every layout; toggling it raises `OnChanged`, which causes the layout to re-render.

---

## 7. Cross-cutting interaction patterns

### 7.1 Snackbar

All success, warning, and error feedback uses MudBlazor's snackbar (`ISnackbar.Add`).

- Saves: "Saved" — Success.
- Deletes: "Deleted" — Success.
- Validation issues: short imperative messages — Warning.
- API exceptions: `ex.Message` — Error.

### 7.2 Confirmation

Destructive actions (delete tournament, delete venue, delete court, delete division, delete team), generation actions (generate schedule, generate bracket), and import application all open a confirmation message box before mutating server state. See [`dialogs.md`](dialogs.md#confirmation-message-boxes).

### 7.3 Loading indicators

- Tables / data grids: built-in loading row driven by a `_loading` flag.
- Pages with deferred content: `MudProgressLinear Indeterminate="true"` at the top of the section.
- Auth resolution: same indeterminate bar plus the message "Preparing sign-in…".

### 7.4 Empty states

- Use `MudAlert Severity="Severity.Info"` with concise copy that tells the user what to do next ("Use Generate to create pool-play matches.", "Enter scores to populate.", "Generate one above.").
- The public landing and event-not-found pages use card-style empty states with an icon + heading + helper text + optional action.

### 7.5 Forms

- Standard density: `Margin.Dense`, `Variant.Outlined`.
- Required fields are marked via `Required="true"` (MudBlazor surfaces a leading asterisk).
- Numeric fields use `Min` / `Max` to constrain server-acceptable ranges.
- Selects with constrained sizing use the project utility classes `cf-select--sm`, `cf-select--md`, `cf-select--lg`.

### 7.6 Tables and grids

- `MudDataGrid` for admin lists with rich actions; `MudTable` for read-only tabular displays (standings, public schedule).
- Standard density: `Dense="true"`, `Hover="true"`. Striping is off by default.
- Action columns are right-aligned and use icon buttons (`Size.Small`) with `aria-label` text.

### 7.7 Buttons

- The primary toolbar Add button across the entire app is `CfToolbarAddButton`: small, primary filled, leading `Add` icon, label "Add", with an `AriaLabel` parameter for context (e.g. "Add tournament", "Add team").
- Page-level secondary actions (Generate, Check conflicts, Refresh, Help) are sized `Size.Small` with leading icons. Outlined for non-destructive secondary actions; filled primary for the dominant action of the page.
- Destructive icon buttons use `Color.Error`.

### 7.8 Breadcrumbs

- Public Event page uses `MudBreadcrumbs` (`Events` → event name disabled).
- Admin pages do not use breadcrumbs in V1 — back navigation relies on a leading `ArrowBack` icon button (e.g. Tournament editor) or the side drawer.

---

## 8. Responsive behavior

- App container maxes out at `ExtraExtraLarge` for admin and `Large` for public; on smaller viewports it occupies full width with the standard MudBlazor breakpoints.
- Drawer: mini variant (icon-only collapsed) opens on hover / click; usable down to mobile widths.
- Public landing grid: xs=12, sm=6, md=4 columns.
- Teams filter strip: xs=12, sm=6 (division and team), md=4 columns each.
- Schedule grid: the table itself is wrapped in a horizontal scroller; the leftmost "Court" column and the header row are CSS-sticky so they stay visible while scrolling.

---

## 9. Accessibility

- Every icon-only button supplies `aria-label` (or wraps in a `MudTooltip`) so screen readers announce its purpose. Examples: drawer toggle, theme toggle, refresh, help, delete, edit, move, score, add (per context).
- Keyboard:
  - The drawer toggle, theme toggle, all toolbar buttons, and dialog actions are reachable via Tab.
  - The Venue dialog and Tournament editor's "New court name" inputs commit on **Enter** and refocus the input after add.
  - `FocusOnNavigate` automatically focuses the page `<h1>` after every route change.
- Color: status chips use color *and* text labels; never color alone.
- Forms: required fields are programmatically marked; labels are always rendered (no placeholder-only inputs).
- Breakpoints / typography are defined in rem so they respect the user's browser zoom and font preferences.

---

## 10. Error and edge states (cross-cutting)

| Surface | State | UI |
| --- | --- | --- |
| Any admin page | API exception during load | Snackbar Error with the exception message; the page renders whatever data it already had. |
| Any admin page | API exception during mutation | Snackbar Error; the dialog stays open so the user can retry. |
| Auth | Unauthenticated load of protected route | `RedirectToLogin` → `/login?returnUrl=...`. |
| Auth | Clerk publishable key missing | Inline `MudAlert Error` on the login page. |

Per-feature error states live in each [`features/*.md`](../features/) doc.
