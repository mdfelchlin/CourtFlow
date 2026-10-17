# Spec: Public landing page

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack. This slice changes only the Blazor WebAssembly app. The API, the domain, and the database do not change. The director frame, brand, and conventions in [../look-and-feel/spec.md](../look-and-feel/spec.md) stay the same except where this spec says otherwise.

The proof of concept's [public landing](../../poc/docs/features/public-landing.md) and [shell](../../poc/docs/ui/shell.md) docs are evidence of past behavior. Their events grid, hero panel, real sign-in, and "Admin" button are not part of this slice.

## Routes

| Address | Page | Frame |
| --- | --- | --- |
| `/` | Landing page | Public |
| `/tournaments` | Tournament list | Director |
| `/tournaments/new`, `/tournaments/{id}` | Tournament editor | Director (unchanged) |
| `/teams` | Teams | Director (unchanged) |
| Any unknown address | Not found | Director |

- The tournament list moves from `/` to `/tournaments`. It no longer answers at `/`.
- Visiting `/` with or without a trailing slash shows the landing page. Visiting `/tournaments/` with a trailing slash shows the tournament list.
- Old bookmarks to `/` now open the landing page. No redirect is added.

Every director link that pointed at the tournament list now points at `/tournaments`:

- The director app bar's logo and name.
- The drawer's **Tournaments** link. It is active on `/tournaments`, `/tournaments/new`, and `/tournaments/{id}`. It is not active on `/`, `/teams`, or the not-found page.
- The tournament editor's back button ("Back to tournaments") and its "This tournament was not found" link.
- The Teams page's "Go to tournaments" link.
- The not-found page's "Back to tournaments" link.

Existing navigation to `/tournaments/new` and `/tournaments/{id}` does not change.

## The public frame

The landing page renders inside a public frame. It is a separate layout from the director frame and shares its brand: the same palettes, typography, shape, and logo, all from the same C# theme definition.

**Top bar.** It has the same height, color, and elevation as the director app bar. From left to right:

- The CourtFlow logo at 32 px and the word CourtFlow. Both sit inside one link to the landing page at `/`. The logo's alt text is empty because the link text names it.
- A spacer.
- The theme toggle. In light mode it shows the `DarkMode` icon with the label "Switch to dark theme". In dark mode it shows the `LightMode` icon with the label "Switch to light theme". These are the director toggle's icons and labels.
- The **Login** button. It is filled, uses the secondary color, reads "Login" in sentence case, and has no icon. It sits in the upper-right corner, after the toggle.

The public frame has no menu button, no drawer, no user name, and no tournament selector.

**Content.** Page content sits in a centered container at most MudBlazor's large width, with vertical padding. On narrower screens it takes the full width.

**Phone width.** At 360 px wide, the page does not scroll sideways. The logo, the toggle, and the Login button are always fully visible. If space runs out, the word CourtFlow shrinks or truncates first. Nothing in the top bar wraps onto a second line.

## The landing page

- The browser tab title is "CourtFlow".
- The page shows "Welcome" as its one `h1`. It uses the same weight as director page titles, at a larger size.
- Directly under it, "Coming soon" appears as a subheading in the secondary text color.
- Both lines are centered horizontally, with generous space above them.
- There is no card, hero panel, background image, or other content.
- The page calls no API and shows no tournament data. Nothing on it can change data.
- Focus moves to the `h1` after navigation, as on director pages.

## Login

- Pressing **Login** navigates to `/tournaments` inside the app, without a full page reload.
- No sign-in, account, or credential step happens. There is no `/login` route.
- The director frame still shows no sign-in or sign-out controls.

## Light and dark

The public frame and the director frame each have their own light or dark choice.

- The director's choice keeps the existing `courtflow-theme` cookie and all its current rules. Directors who already chose a mode keep it.
- The public frame's choice uses a new cookie named `courtflow-public-theme`. Its rules match the director cookie:
  - A value of `light` or `dark`, ignoring case, selects that mode.
  - A missing cookie, or any other value, follows the operating system's light or dark setting at startup. It does not follow later operating system changes while the app is open.
  - It is written only when someone presses the public toggle. It holds the new mode, applies to the whole site, and lasts one year. Following the operating system never writes it.
  - If the browser refuses the cookie, the toggle still switches the public mode for the rest of the visit.
- Pressing the public toggle never changes the director's mode or cookie, and pressing the director toggle never changes the public mode or cookie.
- Moving between the landing page and a director page in the same visit shows each frame in its own mode. If the two choices differ, the mode switches on that navigation.
- The loading screen shown before the app starts stays light, whichever page is opened.

## Styling

The look-and-feel rules apply to the public frame and the landing page unchanged:

- Colors, typography, sizes, and spacing come from the C# theme definition and MudBlazor's typed parameters. No new hex colors, inline `style` attributes, or `*.razor.css` files.
- MudBlazor spacing utility classes are the only class names allowed in markup.
- Any shared value the two frames both need, such as the app bar elevation or the logo size, is defined once and used by both.
- If the CourtFlow word in the public top bar cannot shrink or truncate through MudBlazor's typed parameters and utility classes alone, the plan must name the one hand-written CSS rule it needs and why. The look-and-feel spec requires every such exception to be named.

## Accessibility

- The theme toggle has its accessible label and tooltip. The Login button's visible text is its accessible name.
- The brand link, the toggle, and the Login button can be reached and used from the keyboard, in that order.
- The landing page has exactly one `h1`.
- Text meets WCAG AA in both modes, including the Login button's text on the secondary color against the top bar.

## How this is checked

- Component tests cover:
  - The public frame renders the brand link to `/` with the logo and the name, the theme toggle, and the Login button, in that order. It has no menu button and no drawer.
  - The landing page renders one `h1` reading "Welcome" and the text "Coming soon", and calls no API.
  - Pressing Login navigates to `/tournaments`.
  - With no public cookie, the public mode follows the reported operating system setting. A valid public cookie wins. An invalid value is ignored.
  - Pressing the public toggle flips the public mode and writes only the public cookie. Pressing the director toggle flips the director mode and writes only the director cookie. Neither changes the other frame's mode.
  - The director app bar's brand link, the drawer's Tournaments link, and the not-found, editor, and Teams links all point at `/tournaments`.
  - The Tournaments drawer link is active on `/tournaments`, `/tournaments/new`, and `/tournaments/{id}`, and not active on `/`, `/teams`, or `/not-found`.
  - The tournament list page answers at `/tournaments`, and the landing page answers at `/`.
- The existing director layout test that checks for no "Login", "Sign in", or "Logout" text keeps passing. "Login" appears only in the public frame.
- The existing styling tests, for no `*.razor.css` files, no `style` attributes, no hex colors, and loading-screen colors matching the C# palette, keep passing with the new files included.
- Today's tournament, editor, and Teams page tests keep passing for behavior. Their expected link addresses change from `/` to `/tournaments`.

## Decisions this spec makes that the intent did not

These were settled with the product owner while this spec was written.

- **Not found stays in the director frame.** An unknown address still renders inside the director frame, and its link goes to `/tournaments`. A later public slice may add a public not-found page.
- **Cookie names.** The director keeps `courtflow-theme`. The public frame adds `courtflow-public-theme`.
- **Top bar look.** The public top bar matches the director app bar's height, color, and elevation. The toggle comes before Login. Login is a filled, secondary-color button with no icon.
- **Landing page look.** "Welcome" is the `h1` and "Coming soon" is the subheading, both centered on the page background with no card or hero. The tab title is "CourtFlow".
- **Phone width.** Everything in the top bar stays visible. The word CourtFlow shrinks or truncates before anything else gives way.
- **No redirect.** `/` becomes the landing page with no redirect for old tournament list bookmarks.

## Deliberately not in this slice

- Real sign-in, sign-out, accounts, and a `/login` route.
- Public tournament, event, schedule, or standings pages.
- A public not-found page.
- A hero panel, events grid, or marketing copy.
- Any API or database change.
- Following operating system theme changes while the app is open.
- A dark-mode loading screen.
