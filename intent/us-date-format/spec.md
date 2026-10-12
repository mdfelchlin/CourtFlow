# Spec: US date format

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack. This slice changes only how the Blazor WebAssembly app shows and accepts calendar dates. The API, the domain, and the database do not change. The rules in [../tournament-setup/spec.md](../tournament-setup/spec.md) stay the same. Only the date text changes.

The proof of concept's [tournaments](../../poc/docs/features/tournaments.md) page and [design system](../../poc/docs/ui/design-system.md) are evidence of past behavior. The tournaments list there used the browser's default date formatting. The date field used month, day, then year. Where they disagree with this spec, this spec wins.

## The pattern

Every calendar date a director sees uses one pattern: two-digit month, two-digit day, four-digit year, separated by slashes.

`09/01/2026` is September 1, 2026. `02/03/2026` is February 3, 2026, never March 2.

The pattern does not follow the browser or the operating system. A day-first locale still shows and accepts month, day, then year.

The empty date field's placeholder is `mm/dd/yyyy`. It does not replace the label.

## Where it appears

**Tournament list.** The start date and end date columns use the pattern. A tournament from September 1 through September 3, 2026 shows `09/01/2026` and `09/03/2026`. Saved tournaments always have both dates, so those cells are never blank. The phone layout keeps the labels "Start date" and "End date".

**Start date and end date fields.** The date field shows its value in the pattern. Choosing a day on the calendar sets that calendar date and shows it in the pattern. Typing uses the same order.

The date field is the only place a director types a date. The list does not edit dates in place.

No other director screen shows a calendar date today. Any later screen that does uses this same pattern.

## Typed entry

A typed value is accepted only when it is a real calendar date in US order:

- `09/01/2026` and `9/1/2026` are both September 1, 2026. After acceptance, the field shows `09/01/2026`.
- `02/03/2026` is February 3, 2026.
- `02/31/2026` is not a date. The field does not take it.
- `13/01/2026` is not a date. Month 13 is not January.
- `2026-09-01` is not accepted. Year-month-day is no longer a way to type a date.
- A two-digit year, such as `09/01/26`, is not accepted.
- Clearing the field leaves it empty. Saving still requires both dates, as tournament setup already requires.

Picking a day on the calendar and typing a valid date produce the same calendar date. The five-day warning and the rule that the end date must be after the start date still compare those calendar dates, not the display text.

## What stays the same

The API still sends and accepts calendar dates as `yyyy-MM-dd`. Postgres still stores them as dates. A tournament saved from `09/01/2026` and `09/03/2026` is stored as September 1 through September 3, 2026.

Time zone labels, match duration, and every other tournament rule are unchanged. This slice does not format times of day. The director app shows none today.

## How this is checked

- A date field given September 3, 2026 shows `09/03/2026`. Typing `09/01/2026` or `9/1/2026` yields September 1, 2026. Typing `02/03/2026` yields February 3, 2026. Typing `13/01/2026`, `02/31/2026`, `09/01/26`, or `2026-09-01` does not yield a date.
- The tournament list shows `09/01/2026` and `09/03/2026` for a tournament from September 1 through September 3, 2026.
- Editor tests that type dates use the US pattern and still save the same calendar dates.
- API tests that send `yyyy-MM-dd` stay as they are.

## Decisions this spec makes that the intent did not

The intent left these to the spec. Change any of them before a plan is written.

- **Numeric pattern.** US order is `MM/dd/yyyy` with leading zeros and slashes, not a written month such as "September 1, 2026".
- **Single digits while typing.** `9/1/2026` is accepted and then shown as `09/01/2026`.
- **Locale.** The pattern is fixed. It does not follow the browser's locale.
- **Wire format.** The API keeps `yyyy-MM-dd`. Only the director app's text changes.

## Deliberately not in this slice

- Dates on a public page for coaches or spectators.
- Times of day, including match start times.
- Changing how dates are stored or sent on the API.
- Translating month names or moving the calendar's first day of the week.
