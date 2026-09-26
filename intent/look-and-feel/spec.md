# Spec: Look and feel

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack. This slice changes only the Blazor WebAssembly app and its MudBlazor theme. The API, the domain, and the database do not change. The behavior in [../tournament-setup/spec.md](../tournament-setup/spec.md) stays the same. Only its presentation changes.

The proof of concept's [design system](../../poc/docs/ui/design-system.md), [shell](../../poc/docs/ui/shell.md), and [dialogs](../../poc/docs/ui/dialogs.md) docs are evidence of past behavior. Where they disagree with this spec, this spec wins. In particular, this spec replaces the proof of concept's `--cf-*` CSS token layer and its [styling decision record](../../poc/docs/adr/0002-styling-strategy.md) with typed C#.

## The frame

Every director page renders inside one layout.

**App bar.** It uses the primary color and a slight elevation. From left to right:

- A menu button that expands and collapses the drawer. Accessible label: "Toggle navigation".
- The CourtFlow logo at 44 px and the word CourtFlow. Both link to the tournament list at `/`.
- On the right, the theme toggle. In light mode it shows the `DarkMode` icon with the label "Switch to dark theme". In dark mode it shows the `LightMode` icon with the label "Switch to light theme".

The app bar shows no user name, sign-in, sign-out, or tournament selector.

**Drawer.** It uses the mini variant at every screen width. Collapsed, it shows only icons. It expands to show labels when the director hovers over it or presses the menu button. On a phone-width screen, the expanded drawer covers the content instead of pushing it aside. The drawer sits below the app bar.

The drawer lists one link: **Tournaments**, with the `EmojiEvents` icon, going to `/`. It is marked active on the tournament list and in the tournament editor. Later slices add their own links. The drawer never lists a page that does not exist.

**Content.** Page content sits in a centered container that is at most MudBlazor's extra-extra-large width, with vertical padding. On narrower screens it takes the full width.

**Page heading.** Each page's main title is an `h1` element styled like the current `h4` titles. The router already moves focus to the `h1` after navigation. Today's pages render `h4`, so focus lands nowhere.

**Not found.** An unknown route renders inside the frame. It shows the `SearchOff` icon, the heading "Page not found", and a link back to the tournaments. The editor's "This tournament was not found" state also stays inside the frame and gains the same link.

**Unhandled errors.** The Blazor error bar ("An unhandled error has occurred" with Reload) stays. It renders outside the Blazor layout and keeps its plain, light-only styling.

## Brand

The brand values below carry over from the proof of concept. They are defined once, in C#.

**Light palette**

| Role | Value |
| --- | --- |
| Primary | `#245F73` |
| Secondary | `#733E24` |
| Background | `#F2F0EF` |
| Surface | `#FFFFFF` |
| App bar background | `#245F73` |
| App bar text | `#F2F0EF` |
| Text primary | `#1F2A30` |
| Text secondary | `#5D6670` |
| Success | `#2E7D32` |
| Info | `#245F73` |
| Warning | `#F59E0B` |
| Error | `#D32F2F` |

**Dark palette**

| Role | Value |
| --- | --- |
| Primary | `#1E4F60` |
| Secondary | `#A56747` |
| Background | `#1D2327` |
| Surface | `#2A3237` |
| App bar background | `#1E4F60` |
| Drawer background | `#232B30` |
| Text primary | `#F2F0EF` |
| Text secondary | `#D6D8D7` |
| Lines and dividers | `#3A444A` |

Success, info, warning, and error keep their light values in dark mode unless contrast fails. Text on any colored surface meets WCAG AA in both modes. Warning in particular pairs with dark text, not white.

**Typography.** The font stack is Inter, then "Segoe UI", Roboto, Helvetica, Arial, sans-serif. The base size is 0.95rem, weight 400, line height 1.5. Headings styled as `h4` and `h6` use weight 600. Buttons use weight 600 and sentence case, never uppercase. Sizes are in rem so browser zoom and font settings apply.

**Shape.** The default corner radius is 10 px for cards, dialogs, paper, and buttons.

**Logo and icons.** The logo is the proof of concept's `CourtFlow.png`. The favicon and the 192 px app icon are made from that logo and replace Blazor's defaults. The same logo is used in both modes. Icons come from MudBlazor's filled Material icons, using the proof of concept's choices: `EmojiEvents` for tournaments, `SportsVolleyball` for courts, `Add`, `Edit`, `Delete`, `Save`, `ArrowBack`, `LightMode`, `DarkMode`, `SearchOff`, and `Info`.

## Light and dark

- On startup, the app reads a cookie named `courtflow-theme`. A value of `light` or `dark` selects that mode.
- If the cookie is missing, or holds any other value, the app uses the operating system's light or dark setting at startup. It does not follow later operating system changes while it is open.
- The cookie is written only when the director presses the toggle. It holds the new mode, applies to the whole site, and lasts one year. Following the operating system never writes the cookie.
- If the browser refuses the cookie, the toggle still switches the mode for the rest of the visit.
- The loading screen shown before the app starts is always light. After the app loads, the frame appears in the resolved mode. A dark-mode director may see the light loading screen briefly before the app appears.

## Typed styling

Colors, typography, spacing, radii, component defaults, and derived tints are defined in strongly typed C#.

- One C# theme definition holds both palettes, the typography, and the layout values above. No hex color, font name, or radius appears anywhere else, except the one exception below.
- The current light or dark mode lives in one injectable C# service. The layout binds MudBlazor's theme provider to it.
- Visual choices in markup use MudBlazor's typed parameters, such as `Color`, `Variant`, `Margin`, `Typo`, `Size`, `Elevation`, and `MaxWidth`. They do not use hand-written class names or inline `style` values.
- This slice needs no derived tints. When a later slice does, it adds them as typed C# values computed from the palette.
- Blazor scoped CSS files (`*.razor.css`) are not used.

Hand-written CSS is allowed only in these cases:

1. **The loading screen and the unhandled-error bar**, in `wwwroot/css/app.css`. Both render before or outside Blazor, so no C# can style them. The loading screen uses the brand's light background and primary color. These are the only brand values repeated outside C#, and a test fails if they drift from the C# light palette.

MudBlazor's own stylesheet is a library file, not hand-written CSS. The font is loaded by a stylesheet link in `index.html`.

The Bootstrap leftovers from the Blazor template are removed. That means the Bootstrap rules in `app.css`, `Layout/MainLayout.razor.css`, and `wwwroot/lib/bootstrap`. The Roboto font link is replaced by Inter.

## Wrapper components

Pages and dialogs use CourtFlow wrapper components around MudBlazor controls. A wrapper carries the shared look and behavior, so pages set only what differs. A control gets a wrapper when a page first needs it. This slice creates wrappers only for controls the tournament list, the tournament editor, and the venue dialog use.

**Form fields.** Each field wrapper renders its label above the control as plain text, not as MudBlazor's floating label. The control itself is outlined and dense. A required field shows an asterisk after its label and is marked required for screen readers. The label is always present and is tied to the control for screen readers. Placeholder text never replaces a label.

The existing screens need these field wrappers:

- **Text field.** Used for tournament name, description (two lines), venue name, and address.
- **Numeric field.** Used for default match duration (15 through 240) and court count (0 through 99). It passes its minimum and maximum through.
- **Date field.** Used for start date and end date. It wraps MudBlazor's date picker and binds a calendar date, with a calendar icon. It replaces today's text inputs of type date.
- **Select.** Used for the time zone. It replaces today's raw HTML `<select>`. The options are the US time zones from the tournament setup spec, with a "Select a time zone" prompt when nothing is chosen.

**Buttons.**

- **Add button.** Small, primary, filled, with the `Add` icon and the label "Add". It takes an accessible label for context, such as "Add tournament" or "Add venue".
- **Icon button for row actions.** Small, with a tooltip and an accessible label that names the item, such as "Edit Spring Classic". Delete uses the error color.
- **Save button.** Primary and filled, with the `Save` icon. It is disabled while its save is running, so a double click saves once.

Names use a `Cf` prefix, following the proof of concept.

## Shared feedback

**Snackbars.**

- A successful save shows "Saved" as success.
- A successful delete shows "Deleted" as success.
- A rejected save shows the rule's message as a warning. This covers the domain rule messages the editor and the venue dialog already produce, such as a blank name or an end date on or before the start date.
- A failed API call shows the failure message as an error. The page keeps whatever it already shows, and an open dialog stays open so the director can retry.

The inline red alerts for errors on the editor and the venue form are replaced by these snackbars. The editor's two warnings, one for a span longer than five days and one for a duplicate name, stay inline beside the form. They are advice while typing, not rejected saves.

**Confirmation.** Deleting a tournament and deleting a venue open a modal confirmation. The title names the item, the body says what else is deleted (venues and courts, or courts), and the buttons are "Delete" and "Cancel". Cancel, Escape, and clicking outside all delete nothing. These replace today's inline confirmation panels.

**Venue dialog.** Adding or editing a venue opens a modal dialog instead of today's inline panel below the venue list. The title is "Add venue" or "Edit venue". "Cancel" sits on the left and the Save button on the right. It saves and then closes, and the venue list reloads. The dialog stays open on a rejected save or a failed call.

**Loading.** The tournament list and the editor show an indeterminate progress bar at the top of the content while their data loads. Today the editor shows an empty form while it loads. The form must not appear until the tournament is loaded.

**Empty states.** Empty states use an info alert that says what to do next.

- Tournament list: "No tournaments yet. Use Add to create one."
- Venue list: "No venues yet. Use Add to create one."
- The editor's note on a tournament that has not been saved yet stays: "Details must be saved first."

## Existing screens

**Tournament list.** The Add button sits in the header row beside the `h1`. The table is dense with hover highlighting. It shows name, start date, end date, time zone label, and default match duration shown as, for example, "60 min". The actions column is right-aligned with Edit and Delete icon buttons. Below MudBlazor's small breakpoint, each row stacks into a labeled block, so the list does not scroll sideways on a phone.

**Tournament editor.** A back icon button with the label "Back to tournaments" sits beside the `h1`. The details form is one card. On wide screens, start and end date sit side by side. Below the small breakpoint, they stack. The Venues card has its own Add button in its header. Each venue shows its name, its address when it has one, and its court count with the `SportsVolleyball` icon. Edit and Delete icon buttons sit beside each venue.

**Phone width.** At 360 px wide, no page scrolls sideways. A later slice may add wide grids that scroll sideways on purpose. That is allowed only inside the grid, never for the whole page.

## Accessibility

- Every icon-only button has an accessible label, and a tooltip where a mouse user would otherwise have to guess.
- The menu button, theme toggle, drawer links, toolbar buttons, form fields, and dialog buttons can all be reached and used from the keyboard. Dialogs trap focus while open and return it to the button that opened them.
- Focus moves to the page's `h1` after every navigation.
- Color is never the only signal. Delete buttons carry an icon and a label. Snackbars and alerts carry text and an icon.

## How this is checked

- Component tests cover:
  - The layout renders the logo link, the menu button, the drawer's single Tournaments link, and the theme toggle with the right label for each mode.
  - With no cookie, the mode follows the reported operating system setting. With a valid cookie, the cookie wins. An invalid value is ignored.
  - Pressing the toggle flips the mode and writes the cookie.
  - Each page renders one `h1`.
  - Delete confirmations and the venue dialog open modally, and Cancel deletes or saves nothing.
  - Saves and deletes raise the listed snackbars.
  - Field wrappers render their label above the control and mark required fields.
- A test compares the brand values in `app.css` with the C# light palette.
- A test fails if a `*.razor.css` file exists, or if a `.razor` file under `CourtFlow.Web` has a `style` attribute or a hex color.
- Today's tournament and venue page tests keep passing for behavior. Their selectors change wherever the markup changes.

## Decisions this spec makes that the intent did not

The intent left these to the spec. Change any of them before a plan is written.

- **Modal dialogs.** The venue form and the delete confirmations become modal dialogs, matching the proof of concept, instead of inline panels.
- **Date picker.** Start and end date use MudBlazor's date picker instead of the browser's native date input.
- **Snackbars for rejected saves.** Rejected saves show in a snackbar, as the intent says, rather than as inline alerts. Warnings that do not block a save stay inline.
- **Spacing classes.** Spacing in markup uses MudBlazor's typed spacing parameters, such as `MudStack Spacing`, where they exist. Elsewhere it uses MudBlazor's spacing utility classes, such as `pa-4`, which follow MudBlazor's spacing scale. These are the only class names allowed in markup. Raw pixel or rem values are not.
- **Font source.** Inter loads from Google Fonts, the way Roboto does today, rather than being bundled with the app.
- **Tournament list route.** The tournament list stays at `/`. The proof of concept used `/tournaments`.

## Deliberately not in this slice

- A public layout for coaches and spectators.
- Sign-in chrome and a tournament selector.
- TV mode, print styles, and per-division colors.
- Breadcrumbs.
- Wrappers for controls no current page uses, such as autocomplete, time pickers, and data grids.
- Following operating system theme changes while the app is open.
- A dark-mode loading screen.
