# Intent: Look and feel
Author: product owner. Status: accepted.

## Problem
The director app has no settled layout or theme. It shows a plain app bar with the word CourtFlow and default styling. There is no navigation, no brand, no dark mode, and no shared convention for forms or feedback, so each new page would invent its own look.

## Proposed outcome
Every director page sits in the same frame and looks like CourtFlow.

The brand carries over from the proof of concept: deep teal primary, warm rust secondary, warm off-white background, the Inter font, and 10px rounded corners. The app bar shows the CourtFlow logo and name, which link to the tournament list. A favicon matches the logo.

On a director's first visit, the app starts in light or dark to match the operating system. An app bar toggle switches between them, and the browser remembers the choice from then on. The loading screen shown before the app starts is always light. The saved or system theme applies once the app has loaded.

A side drawer holds navigation. Today it lists only Tournaments. Later slices add their pages to it. On a phone-width screen the layout still works: the drawer collapses, and pages stay readable without sideways scrolling, except for wide grids that scroll on purpose.

Pages share these conventions:
- Form fields put labels above dense, outlined inputs. Required fields are marked.
- Saves and deletes confirm in a snackbar. Validation problems and errors also show in a snackbar.
- Destructive actions ask for confirmation first.
- Loading shows a progress indicator. Empty lists say what to do next.
- Icon-only buttons have accessible labels. Every control can be reached by keyboard. Color is never the only signal.

Existing tournament and venue screens adopt the frame and the conventions.

## Affected users and systems
Directors use the Blazor WebAssembly app with MudBlazor. This slice changes the shared layout, theme, and styles. It also updates the existing tournament list, tournament editor, and venue dialog to match. The API and the database do not change. The proof of concept's design system and shell docs are reference only.

## Constraints
- Director layout only. The public layout for coaches and spectators arrives with the public site.
- No sign-in. The app bar shows no user name, login, or logout.
- No tournament selector in the app bar.
- No TV mode, print styles, or per-division colors.
- No new pages. The drawer lists only pages that exist.
- Colors, typography, spacing, radii, component defaults, and derived tints are defined in strongly typed C#. Razor holds markup. Hand-written CSS is limited to what the browser or MudBlazor cannot express in C#, and the spec names each case. This replaces the proof of concept's `--cf-*` CSS token layer.
- Pages and dialogs use CourtFlow wrapper components around MudBlazor controls wherever a wrapper can carry a shared look or behavior, so the look can be customized and reused in one place. A control gets a wrapper when a page first needs it, not ahead of time.
- The drawer uses the proof of concept's mini variant: icons only when collapsed, expanding to show labels.
- The light/dark choice is stored in a cookie. With no cookie, the app follows the operating system at startup.
- The proof of concept's logo image and icon choices are used for now.

## Open questions
None.
