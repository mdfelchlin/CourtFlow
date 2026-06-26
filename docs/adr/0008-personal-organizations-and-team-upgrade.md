# Invite-only tenants; Clerk Organizations provisioned by operator

CourtFlow is **invite-only** in production. There is no open sign-up and no auto-provisioned personal workspace. A **CourtFlow operator** creates each customer **Organization**, mints a matching **Clerk Organization** at that moment (reserving a Clerk org slot deliberately), and sends the **first org invitation** to the owner. The owner signs in and lands in that org; they **invite colleagues** from in-app team settings (Clerk org invitations mirrored into `organization_memberships`).

Prospective customers who do not yet have an account use a **Request access** flow (CourtFlow form, not Clerk Waitlist). Operators review requests and **provision** org + first invite when qualified.

Clerk **sign-up is disabled** (Restricted / invitation-only mode). The public login surface shows **Sign in** and **Request access** side by side.

## Status

**Accepted (partial)** — model locked (2026-06-17 review). Legacy **personal workspace auto-provision** still runs in code today; production must disable it. Request access, admin provision UI, in-app staff invites, and Clerk webhook membership sync are backlog work.

**Supersedes** the personal-workspace-first narrative in the original ADR-0008 draft; `Organization.CreatePersonal` and `UpgradeToTeamWorkspace` remain in the domain for **Development** bootstrap only unless removed later.

## Decisions (2026-06-17)

1. **Production onboarding:** Invite-only. No auto personal workspace. Unauthenticated users cannot create a tenant.
2. **Operator flow (Option A):** Admin creates `organizations` row + Clerk Organization + first owner invitation. Owner accepts → membership exists → `ClerkClaimsTransformation` resolves existing membership (no provision).
3. **Clerk org timing:** Mint Clerk Organization **when the operator provisions the customer org**, not on waitlist/request submit.
4. **Request access (Option W2):** CourtFlow **`/request-access`** page (form: org/club name, contact name, email, optional note) → persists **`access_requests`** (or equivalent) → operator reviews in **CourtFlow admin** → **Provision** action creates org + Clerk org + first invite. **Not** Clerk Waitlist for V1.
5. **Login UX:** `/login` shows **Sign in** (Clerk, invited users only) and **Request access** (link/button). Operator disables public account creation in Clerk Dashboard.
6. **Staff invites:** Org **Admin** invites additional users from in-app **Team** settings → Clerk org invitation API → webhook mirrors to `organization_memberships`.
7. **Development exception:** When `ASPNETCORE_ENVIRONMENT=Development` (or explicit `Dev:AllowPersonalWorkspace=true`), legacy auto-provision **may** remain for local work without admin tooling. Production **never** auto-provisions.

## Operator journey

```mermaid
flowchart LR
  lead[Prospect submits Request access]
  review[Operator reviews queue]
  provision[Provision org + Clerk org + owner invite]
  owner[Owner accepts invite and signs in]
  team[Owner invites staff from Team settings]

  lead --> review --> provision --> owner --> team
```

## Request access form (V1 fields)

| Field | Required | Notes |
| --- | --- | --- |
| Organization / club name | Yes | Becomes default org display name on provision |
| Contact email | Yes | First invite target |
| Contact name | No | Operator reference |
| Message | No | Free text — event volume, timeline, etc. |

On submit: confirmation copy only (no account created). Operator notified (email or admin queue — implementation choice).

## Considered alternatives

- **Auto personal workspace on first login.** Rejected for production — consumes uncontrolled tenants; conflicts with invite-only and Clerk org seat planning.
- **Clerk Waitlist only.** Rejected as sole funnel — app-level waitlist does not create CourtFlow orgs or tie leads to tenant provisioning.
- **Upgrade personal → team.** Rejected — all production orgs are team orgs from provision; no upgrade path needed.
- **Mint Clerk org on first invite accept.** Rejected — operator cannot reserve seats at provision time; org would sit without Clerk backing.

## Consequences

- **`ClerkClaimsTransformation`:** Production path resolves **existing** `organization_memberships` only; return **401 / no org** if user signed in without invite. Remove or gate `Organization.CreatePersonal` behind Development.
- **New aggregates/tables:** `AccessRequest` (pending / approved / denied / provisioned); admin provision command orchestrates CourtFlow org + Clerk API + first invitation.
- **New admin surface:** Access request queue + **Provision organization** (pre-fill from request).
- **New org member surface:** Team / invite settings for org Admins (depends on Clerk org invitations + webhooks).
- **CONTEXT.md** and [`features/auth.md`](../features/auth.md) describe login + request access; [`local-development.md`](../local-development.md) documents dev bootstrap (`Dev:ClerkUserId`, demo org).
- **Clerk Dashboard:** Restrict sign-up; enable Organizations; configure invitation emails.
- Planned **ADR-0017** (Clerk auth topology) should reference this ADR for onboarding.

## Implementation phases

| Phase | Scope | Backlog |
| --- | --- | --- |
| **8a** | `/request-access` page + API; `access_requests` persistence; login page Sign in + Request access | AUTH-01 |
| **8b** | Admin queue + **Provision org** (CourtFlow org + Clerk org + owner invite) | AUTH-02 |
| **8c** | Disable personal auto-provision in non-Development; membership-only claims resolution | AUTH-03 |
| **8d** | In-app **Invite team member** + Clerk webhook membership mirror | AUTH-04 |
