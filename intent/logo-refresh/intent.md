# Intent: Logo refresh
Author: product owner. Status: draft.

## Problem
CourtFlow's logo assets look dated and do not hold up on screen. The mark is a navy and grey volleyball set inside a white "CF" monogram. It ships only as fixed-size raster images: a 512px app bar logo, a 64px favicon, and a 192px icon. Those blur when scaled up or shown on high-density displays. The white letterforms all but disappear on a light background, so in light mode the logo reads as a lone ball floating in empty space rather than a brand. The mark also has fine detail and thin strokes that do not survive at favicon size. Everyone who opens the site sees this: directors in the app bar, and soon coaches, players, and spectators in the public top bar.

## Proposed outcome
CourtFlow has a refreshed logo that is sharp at every size, sits naturally on both light and dark backgrounds, and follows modern logo design: simple, flat, legible at small sizes, and recognizable at a glance.

Every place the logo appears uses the refreshed assets. That covers the director app bar, the public top bar, the browser tab favicon, and the app icon. Each looks crisp on high-density screens and does not look cut out, boxed, or washed out against the surrounding theme in either light or dark mode.

The logo still says "volleyball tournament" and still reads as CourtFlow. It is a redraw of the volleyball: a flat ball in the brand teal and cream, with the seams cut out so the surface behind shows through, and one panel in a clay accent. The "CF" monogram is dropped. The name stays as separate text beside the mark rather than becoming part of the logo.

The mark comes in three forms. A cream mark sits on the brand-teal app bar, which stays teal in both themes. A teal mark sits on light surfaces. A teal rounded tile with the cream ball is the favicon and app icon, so it reads in both light and dark browser chrome. The favicon has its own simpler drawing at 16 pixels, with only the three main seams.

## Affected users and systems
Directors see the logo in the director app bar. Coaches, players, and spectators will see it in the public top bar and browser tab. The slice touches the Blazor WebAssembly app's logo and icon assets, the brand definition that names the logo and its size, and the page head that declares the favicon. The API and the database do not change. The proof of concept keeps its own assets and is reference only.

## Constraints
- Every logo asset in the app is replaced. No old asset stays in use.
- The logo is crisp on high-density displays and at every size it is shown.
- The logo reads clearly on both the light and the dark theme.
- The favicon stays legible at browser tab size.
- The brand palettes do not change. The logo uses the brand teal and cream. Its one extra color is a clay accent, a lighter version of the rust Secondary so it reads on teal.
- The asset set is: scalable vector marks for dark and light surfaces, a vector favicon, a multi-size favicon file (16, 32, and 48 pixels), a 180-pixel Apple touch icon, and 192- and 512-pixel install icons plus a 512-pixel maskable icon, all declared in a web app manifest.
- The artwork is redrawn in-house and generated from code so it can be regenerated. The product owner reviews it.
- New work stays outside the proof of concept.

## Open questions
- Should this land before or with the public landing page, since that page puts the logo in a new top bar?
