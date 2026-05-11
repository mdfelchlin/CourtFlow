# Auth

## Purpose

Get authenticated admins into CourtFlow and signed-out users back to the public surface, using Clerk as the identity provider. Also defines what unauthenticated users see when they land on a protected route.

## Routes

| Route | Page | Layout |
| --- | --- | --- |
| `/login` | Sign-in redirect (Clerk handoff) | `PublicLayout` |
| `/logout` | Sign-out then redirect to `/` | (no chrome) |

Plus the framework-level `RedirectToLogin` component used by `AuthorizeRouteView` for any protected route hit by an unauthenticated user.

## Page header / toolbar

### Login

- Page title: "Sign In · CourtFlow".
- No toolbar; the page is a transient redirect surface.

### Logout

- No visible chrome at all.

## Page body

### Login

- If the request carries an `?Error=` query param (e.g. missing publishable key), shows a dense `MudAlert Error` at the top.
- Otherwise renders just the line "Redirecting to sign-in..." (`Typo.body1`).

### Logout

- Invisible to the user except for the brief navigation to `/`.

### Unauthorized entry into protected routes

- `AuthorizeRouteView` shows the `Authorizing` slot (indeterminate `MudProgressLinear` + "Preparing sign-in…") while auth state is resolving.
- If unauthorized, renders `RedirectToLogin`, which navigates to `/login?returnUrl={current path}`.

## Interactions

### Login

- On first render the page reads Clerk client config via JS interop.
- If Clerk already has a session, navigates directly to the return URL (default `/tournaments`).
- Otherwise calls `courtflowClerk.redirectToSignIn` to hand off to Clerk's hosted UI.
- Return URL must start with `/`; otherwise it falls back to `/tournaments`.

### Logout

- Calls `IAuthSessionService.SignOutAsync()` then `Nav.NavigateTo("/")`.

## Dialogs

None.

## References

- Cross-cutting: [`../ui/shell.md`](../ui/shell.md) (auth flow chrome), [`../ui/design-system.md`](../ui/design-system.md).
- Architecture: [`../architectural-prd.md`](../architectural-prd.md) §5.4 (Clerk + JWT).
- Product: [`../product-overview.md`](../product-overview.md) §3 (admin-only writes; public reads anonymous).
- Data: [`../data-model.md`](../data-model.md#1-account-and-organization-setup).
