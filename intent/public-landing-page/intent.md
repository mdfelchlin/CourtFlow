# Intent: Public landing page
Author: product owner. Status: accepted.

## Problem
CourtFlow has no public front door. Opening the site drops a visitor straight onto the director's tournament list. There is no page that says what the site is, and no clear place for a director to go in.

## Proposed outcome
Opening the site at its root shows a public landing page.

The landing page sits in a public-facing frame, separate from the director frame. Its top bar shows the CourtFlow logo and a "Login" button in the upper-right corner. There is no director side drawer. This frame is the start of the public site, where people without an account will later view published tournaments, schedules, and similar pages. For now it holds only the landing page.

The page body says "Welcome", with the subheading "Coming soon" underneath.

Choosing "Login" takes the visitor to the tournament list. It does not ask for a name, a password, or any other sign-in step yet.

The tournament list moves from the site root to `/tournaments`. The director app bar's logo and name link to the tournament list, not the landing page.

The public top bar has its own light/dark toggle. The public choice is separate from the director's choice, so the landing page can be dark while the director app is light, or the other way around. Each is remembered on its own. Without a saved choice, each follows the operating system, as the director app does today.

## Affected users and systems
Anyone who opens the site sees the landing page first: directors, coaches, players, and spectators. Directors use "Login" to reach the tournament list. This slice touches the Blazor WebAssembly app: it adds the public frame and the landing page, moves the tournament list to `/tournaments`, and points the director app bar's logo and any links to the tournament list at the new address. The API and the database do not change. The proof of concept is reference only.

## Constraints
- The landing page is at the site root. The tournament list is at `/tournaments`.
- The director app bar's logo links to the tournament list.
- The landing page uses a public frame, not the director frame.
- "Login" is a button in the upper-right corner of the public top bar.
- The page body shows "Welcome" and the subheading "Coming soon". Nothing else.
- "Login" goes straight to the tournament list. There is no real sign-in in this slice.
- The landing page shows no tournament data and allows no changes.
- The public top bar has its own light/dark toggle. The public and director choices are remembered separately and can differ.
- New work stays outside the proof of concept.

## Open questions
None.
