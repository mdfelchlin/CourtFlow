# Intent: US date format
Author: product owner. Status: accepted.

## Problem
Dates are displayed in European format and not US. A director sees tournament start and end dates as year, month, then day, on the tournament list and in the start and end date fields.

## Proposed outcome
Every date a director sees is shown in US format: month, day, then year.

That includes the tournament list and the start and end date fields. Typing a date uses the same order.

## Affected users and systems
Directors use the Blazor WebAssembly app. This slice changes how dates are shown and typed there. The API and the database keep storing calendar dates. The proof of concept is reference only.

## Constraints
- Display and typed entry only. Stored dates do not change.
- The same US order applies wherever a date is shown in the director app.

## Open questions
None.
