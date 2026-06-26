# Auth

## Purpose

Get authenticated admins into CourtFlow and signed-out users back to the public surface, using Clerk as the identity provider. Production is **invite-only**: Clerk sign-up is disabled; new customers use **Request access** and operators provision organizations.

See [`../adr/0008-personal-organizations-and-team-upgrade.md`](../adr/0008-personal-organizations-and-team-upgrade.md).

## Routes

| Route | Page | Layout |
| --- | --- | --- |
| `/login` | Sign-in + link to request access | `PublicLayout` |
| `/request-access` | Request access form (prospective customers) | `PublicLayout` |
| `/logout` | Sign-out then redirect to `/` | (no chrome) |

Plus the framework-level `RedirectToLogin` component used by `AuthorizeRouteView` for any protected route hit by an unauthenticated user.

## Page header / toolbar

### Login

- Page title: "Sign In · CourtFlow".
- No app toolbar; centered card or split actions on the public layout.

### Request access

- Page title: "Request Access · CourtFlow".
- Subtitle: explain invite-only onboarding (1–2 sentences).

### Logout

- No visible chrome at all.

## Page body

### Login

- If the request carries an `?Error=` query param (e.g. missing publishable key), shows a dense `MudAlert Error` at the top.
- Primary: **Sign in** — hands off to Clerk (invited / existing users only).
- Secondary: **Request access** — navigates to `/request-access` (`Variant.Outlined` or text button beside primary).
- Do **not** expose a public sign-up path; Clerk Restricted mode blocks account creation on the Clerk UI.

### Request access

- Form fields per ADR-0008: organization/club name, contact email, contact name (optional), message (optional).
- **Submit** → API persists request → success state: "Thanks — we'll review your request and email you if approved." No account is created.
- Link back to **Sign in** for users who already have an invite.

### Unauthorized entry into protected routes

- `AuthorizeRouteView` shows the `Authorizing` slot (indeterminate `MudProgressLinear` + "Preparing sign-in…") while auth state resolves.
- If unauthorized, renders `RedirectToLogin`, which navigates to `/login?returnUrl={current path}`.

## Interactions

### Login — Sign in

- On **Sign in**, read Clerk client config via JS interop.
- If Clerk already has a session, navigate to return URL (default `/tournaments`) when membership resolves.
- Otherwise call `courtflowClerk.redirectToSignIn`.
- Return URL must start with `/`; otherwise fall back to `/tournaments`.
- If user has Clerk account but **no** `organization_memberships` row (signed in without invite), show friendly error: contact support or use the email that received the invitation — do not auto-provision a workspace in production.

### Request access

- Client POSTs to public API (rate-limited) with form payload.
- No Clerk call on submit.

### Logout

- Calls `IAuthSessionService.SignOutAsync()` then `Nav.NavigateTo("/")`.

## Admin (operator — backlog AUTH-02)

- Queue of `access_requests` (pending / denied / provisioned).
- **Provision** action: create Organization + Clerk Organization + send owner invitation; mark request provisioned.

## Org member invites (backlog AUTH-04)

- **Team** settings in admin shell: invite by email → Clerk org invitation → membership webhook → `organization_memberships`.

## Dialogs

None on login/request-access routes.

## References

- ADR: [`../adr/0008-personal-organizations-and-team-upgrade.md`](../adr/0008-personal-organizations-and-team-upgrade.md)
- Cross-cutting: [`../ui/shell.md`](../ui/shell.md), [`../ui/design-system.md`](../ui/design-system.md)
- Architecture: [`../architectural-prd.md`](../architectural-prd.md) §5.4
- Local dev: [`../local-development.md`](../local-development.md)
