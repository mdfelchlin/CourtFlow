# Styling stays on plain CSS + custom properties; no SASS in V1

CourtFlow's styling layer is **plain CSS** (`wwwroot/css/app.css`) plus **Blazor scoped CSS** (`*.razor.css`), with MudBlazor's runtime palette variables (`--mud-palette-*`) as the source for brand colors and a thin set of `--cf-*` custom properties as the source for derived semantic tints. No SASS, no PostCSS, no Tailwind, no Node toolchain. Theme variants (dark mode, TV mode, print) are expressed as `:root` blocks that redefine the `--cf-*` tokens — consumers never see the change. This keeps the build single-tool (`dotnet`), keeps the brand palette single-source (`ThemeState.BuildTheme()`), and avoids the most common preprocessor anti-pattern: a parallel SCSS variables file that drifts from the C# theme the moment someone tweaks a hex.

## Status

accepted — applies to all UI work going forward. The token catalog lives in [`../ui/design-system.md` §2 → Semantic surface tints](../ui/design-system.md#semantic-surface-tints); the print overrides live in [`../features/print-outputs.md`](../features/print-outputs.md); the TV-mode override lives in [`../features/court-status.md`](../features/court-status.md#tv-mode).

## Considered alternatives

- **SASS/SCSS via `AspNetCore.SassCompiler` (or Node-based Dart Sass).** Gives nesting, mixins, partials, color functions, `@extend`. Rejected because (a) modern CSS already covers all of those — native nesting (2023), `color-mix()` (2023), custom properties, `@layer` — without a preprocessor; (b) it duplicates the brand palette out of C# into `.scss` partials, creating an inevitable drift surface; (c) it adds a watch process and CI step that the current `dotnet`-only pipeline does not have; (d) MudBlazor itself does the heavy lifting (theme objects, slot system, runtime CSS) so the preprocessor wouldn't be doing much.
- **PostCSS / Tailwind utility-first.** Rejected on different grounds: Tailwind's philosophy (utilities sprayed across markup) clashes with Blazor scoped CSS and with MudBlazor's component-prop styling model. Mixing all three would make the UI noticeably harder to navigate.
- **Inline `rgba(...)` / `color-mix(...)` literals in component styles.** Rejected because it makes the TV-mode and print overrides impossible to express as a single block — every override would have to hunt down every literal. The whole point of the `--cf-*` token layer is that **one `:root` block flips the whole UI's behavior** for a theme variant.
- **Move the brand palette out of C# into CSS-only.** Rejected because MudBlazor's component theming is C#-driven; we'd lose programmatic access to the palette (e.g. for chart libraries or dynamically-generated images). Keeping the palette in C# and emitting it as CSS variables at runtime — which MudBlazor does by default — is the best of both worlds.

## Consequences

- **No new toolchain.** `dotnet build` / `dotnet watch` remain the entire developer loop. CI does not gain a styling step.
- **One token layer.** Every tinted background, striped pattern, or saturation-bumped color in CourtFlow reads from a `--cf-*` token defined in `app.css`. Components do **not** inline `rgba(...)` or `color-mix(...)` literals. New surfaces that need a new tint add a new token to the catalog rather than spreading literals through `.razor.css` files.
- **Theme variants are `:root` overrides.** Light↔dark flips happen at the MudBlazor `--mud-palette-*` layer (driven by `ThemeState`). TV mode and print flips happen at the `--cf-*` layer in `app.css`. Adding a future variant (high-contrast, sponsor skin) is one more `:root` block, not a component-by-component rewrite.
- **Browser baseline.** `color-mix()` and native nesting set our minimum target at Chromium 111 / Firefox 113 / Safari 16.2 (all Q1 2023). This is well within the support window for an Azure-hosted Blazor admin app and is consistent with the [`../product-overview.md`](../product-overview.md) browser assumptions.
- **Revisit trigger.** If we ever ship multi-tenant *brand skinning* (sponsor-colored builds where the palette decision can't be one C# `MudTheme` per app), or if we adopt a build-time component-class generator that needs a real preprocessor, reopen this ADR. Both are off the current roadmap.
