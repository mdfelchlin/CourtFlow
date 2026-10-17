# Spec: Sign-in and access

Follows the accepted intent in [intent.md](intent.md). The north star in [../product/intent.md](../product/intent.md) locks the stack: .NET API, Blazor WebAssembly with MudBlazor, WorkOS for sign-in, EF Core on Postgres, domain-driven design. The public frame and landing page from [../public-landing-page/spec.md](../public-landing-page/spec.md) and the conventions in [../look-and-feel/spec.md](../look-and-feel/spec.md) stay the same except where this spec says otherwise.

The proof of concept's [auth](../../poc/docs/features/auth.md) page and [ADR 0008](../../poc/docs/adr/0008-personal-organizations-and-team-upgrade.md) are evidence of past behavior. They used Clerk, and their routes and tables are not requirements.

## Who does what

| System | Owns |
| --- | --- |
| WorkOS | Accounts, passwords, Google sign-in, email confirmation codes, password resets, sessions, the invite-only sign-up gate, and the stored Waitlist entries |
| CourtFlow database | Organizations and their status, memberships, roles, CourtFlow invitations (organization, role, expiry), access request tracking, and outgoing email |
| CourtFlow configuration | Who is an operator |

WorkOS organizations, WorkOS roles, and WorkOS membership features are not used. A person is identified by their WorkOS user ID. Their email and name are copied from WorkOS at each sign-in.

## Sessions

- "Login" sends the browser to the API at `/auth/login`. The API redirects to the WorkOS sign-in page. WorkOS returns to the API at `/auth/callback`, which exchanges the code with the WorkOS secret and sets the session cookie.
- The session cookie is named `courtflow-session`. It is `HttpOnly` and `Secure`, uses `SameSite=Lax`, and holds no WorkOS tokens in a form page code can read. The WorkOS refresh token stays on the server side of the cookie.
- A session lasts 30 days from sign-in, whether or not the person uses CourtFlow. WorkOS is set to a 30-day maximum session length with no inactivity timeout. The API renews WorkOS's short-lived access token quietly when it expires. When the 30 days are up, the next API call answers "not signed in."
- The Blazor app sends the cookie with every API call. It learns who is signed in from `GET /api/me`.
- `/auth/logout` clears the cookie, ends the WorkOS session, and returns the browser to the landing page at `/`.
- In production, the Blazor app and the API must share a parent domain so the cookie reaches the API. On a developer's machine both run on `localhost`, which already works.
- The API accepts calls carrying the cookie only from the configured CourtFlow web origin. Calls that change data must come from that origin.

### `GET /api/me`

Answers anyone, signed in or not.

- Signed out: `signedIn: false`.
- Signed in: the person's name and email, `isOperator`, and, if they have an active membership, their organization's ID, name, and status, and their role.

Only one membership is ever returned in this slice.

## Who can do what

Every API call that reads or changes organization data checks the CourtFlow database on that call: is the person signed in, do they have a membership, is the organization active, and does their role allow the action. Removing a member, changing a role, or deactivating an organization therefore takes effect on the person's next action.

| Action | Owner | Editor | Viewer | Operator with no membership |
| --- | --- | --- | --- | --- |
| See tournaments, with their venues, divisions, and teams | All, including archived | All, including archived | Published only | None |
| Change tournaments, divisions, venues, teams, imports | Yes | Yes | No | No |
| Publish, unpublish, archive, restore, delete tournaments | Yes | Yes | No | No |
| See the Teams page | Yes | Yes | Read only, published tournaments only | No |
| See and use the Organization page | Yes | No | No | No |
| Operator screens | Only if also an operator | Only if also an operator | Only if also an operator | Yes |

- Being an operator never opens an organization's data. That always needs a membership.
- API answers:
  - Not signed in: `401`.
  - Signed in but no active membership, or the organization is deactivated: `403` with the reason `no_access` or `organization_deactivated`.
  - Signed in but the role forbids the action: `403` with the reason `forbidden`.
  - Asking for another organization's tournament, team, or other record: `404`, as if it did not exist.
- `IOrganizationScope` stops returning the fixed Test Organization. It returns the signed-in person's organization. `TestOrganizationScope` is used only by tests that need it.

### Viewers

Tournaments now have three states from the [tournament lifecycle](../tournament-lifecycle/spec.md): Unpublished, Published, and Archived. Viewers see only Published tournaments. They do not see Unpublished or Archived ones.

- **The tournament list** shows only Published tournaments. With none, it shows "Nothing has been published yet." and no Add button.
- **A Published tournament's page** opens read-only: every field is disabled, and there are no add, edit, or delete controls. Its status line still shows, but without Publish, Unpublish, Archive, or Restore.
- **The Teams page** opens read-only. Its tournament picker lists only Published tournaments. It has no add, import, edit, or remove controls.
- **An Unpublished or Archived tournament**, opened directly by address, shows "This tournament was not found." The API answers `404` for it and for its venues, divisions, and teams.
- **Schedules** do not exist yet. When they do, viewers see draft schedules of Published tournaments, as the intent says.
- **A tournament that leaves Published** while a viewer has it open, by being unpublished or archived, answers `404` on their next action.

### The automatic archive

The lifecycle's hourly automatic archive runs with no one signed in, across every organization. It must keep reading every organization's tournaments after the organization filter changes from the fixed Test Organization to the signed-in person's organization. It already bypasses the filter for that one query.

## Operators

- Operators are the email addresses listed in the API setting `Operators:Emails`. It starts with `mdfelchlin@gmail.com`. Every listed operator has the same powers. Adding one means editing the setting and redeploying.
- Emails are compared after trimming and lowercasing, as WorkOS does.
- An operator can also be a member of an organization.

## Routes

| Address | Page | Frame | Who |
| --- | --- | --- | --- |
| `/` | Landing page | Public | Anyone |
| `/request-access` | Request Access | Public | Anyone |
| `/no-access` | You don't have access yet | Public | Signed in |
| `/tournaments`, `/tournaments/{id}` | Tournaments (existing) | Director | Members (viewers read only, see above) |
| `/tournaments/new` | New tournament (existing) | Director | Owners and editors |
| `/teams` | Teams (existing) | Director | Members (viewers read only, see above) |
| `/organization` | Organization | Director | Owners |
| `/operator/requests` | Access requests | Director | Operators |
| `/operator/organizations` | Organizations | Director | Operators |
| Any unknown address | Not found | Director | Anyone |

API sign-in endpoints: `/auth/login?returnUrl=`, `/auth/callback`, `/auth/logout`.

### Where people land

- **Return address.** `returnUrl` is honored only if it is a path on the CourtFlow site: it starts with `/`, does not start with `//`, and has no scheme or host. Anything else is ignored.
- **After signing in**, the first match wins:
  1. A valid return address, if the person may open it.
  2. A member with an active organization goes to `/tournaments`.
  3. An operator with no membership goes to `/operator/requests`.
  4. Anyone else goes to `/no-access`.
- **Signed out, opening a director page.** The app sends the browser to `/auth/login?returnUrl={that page}`. While it checks whether anyone is signed in, the page shows a thin progress bar and nothing else.
- **Signed in, opening a page their role forbids.** A viewer opening `/tournaments/new`, a non-owner opening `/organization`, or a non-operator opening an operator page sees the director frame's "not found" page.
- **Signed in without an active membership**, opening a director page: sent to `/no-access`. Operator pages still open for operators.
- **The not-found page** does not require sign-in.

## Public frame changes

| State | Top bar, after the theme toggle |
| --- | --- |
| Signed out | "Request Access" as a text button, then "Login" as the filled secondary button. Login sits in the upper-right corner. |
| Signed in with an active membership | "Dashboard" as the filled secondary button, linking to `/tournaments`. "Request Access" is hidden. |
| Signed in without an active membership | "Sign out" as the filled secondary button. "Request Access" is hidden from the top bar; the no-access page offers it. |

- The top bar reads `/api/me` once when the app starts and again after signing in or out. Until the answer arrives, it shows neither set of buttons, so it never flashes the wrong one.
- At 360 px wide, Login and the theme toggle always stay fully visible. How "Request Access" fits is chosen during implementation by trying the options side by side, for example shortening it to "Request" or moving it below the top bar. The plan names the options to try. The product owner picks the one that looks best.

## Director frame changes

- The app bar gets a user menu at the far right, after the theme toggle. Its button shows the person's initials or a person icon, labeled with their name.
- Menu items, in order:
  1. The person's name and email, as a header that does nothing when chosen.
  2. "View public site", which goes to `/` and keeps them signed in.
  3. "Organization", for owners only.
  4. "Operator", for operators only.
  5. "Sign out".
- **Drawer for members.** Tournaments and Teams for everyone.
- **Drawer for operators.** An "Operator" group with Requests and Organizations. A member who is also an operator sees both.
- The tournament selector and anything else that loads tournaments load nothing until `/api/me` confirms an active membership.

## Request Access

### The form

The page title is "Request Access · CourtFlow", and the `h1` is "Request Access". One sentence explains that CourtFlow is invite-only and an operator reviews each request.

| Field | Rules |
| --- | --- |
| Name | Required. Trimmed. At most 100 characters. |
| Email | Required. Trimmed and lowercased. Valid address. At most 254 characters. |
| Organization name | Required. Trimmed. At most 100 characters. |
| Message | Optional. Trimmed. At most 1,000 characters. |
| CAPTCHA | Cloudflare Turnstile. Required. The API verifies the answer before doing anything else. |

- If the person is signed in, which happens when they come from the no-access page, the email is filled in with their sign-in email and cannot be changed.
- A "Back to sign in" link points to `/auth/login`.

### What sending does

Checks run in this order. The first one that fails stops the submission.

1. **CAPTCHA fails.** "Please complete the check and try again." Nothing is stored.
2. **The email already belongs to someone with an active membership.** "This email already has access. Sign in instead," with a Login button.
3. **The email has a waiting request.** "There's already a request waiting for this email. If you haven't heard back within 48 hours, contact mdfelchlin@gmail.com."
4. **The email was declined before.** "This email can't send a new request. Contact mdfelchlin@gmail.com."
5. **Otherwise:**
   - CourtFlow creates a WorkOS Waitlist entry through the API. The entry carries name, organization name, and message as extra fields.
   - CourtFlow stores its own access request with the Waitlist entry ID, the details, the time requested, and an expiry 7 days later.
   - CourtFlow emails every operator (email 1).
   - The page replaces the form with: "Thanks — we'll email you when your account is ready. If you haven't heard back within 48 hours, contact mdfelchlin@gmail.com."

If WorkOS cannot be reached, the form stays filled in and shows "We couldn't send your request. Please try again in a few minutes." Nothing is stored in CourtFlow.

### Waitlist settings in WorkOS

- The Waitlist is on.
- WorkOS's Waitlist confirmation email is off.
- WorkOS's invitation emails are off.
- The WorkOS sign-in page's "Sign up" link is hidden with custom CSS in the branding settings. Where the free branding allows it, the sign-in page shows a link to CourtFlow's `/request-access`. The plan confirms which branding option supports a link.

## Access request lifecycle

| State | How it gets there | WorkOS Waitlist entry |
| --- | --- | --- |
| Waiting | Sent from the form | Pending |
| Approved | Operator approves | Approved, which creates a WorkOS invitation |
| Declined | Operator declines | Denied, so the email cannot join again |
| Expired | 7 days pass while Waiting | Deleted, so the email can ask again |

- **Reminder.** Between 24 hours before expiry and expiry, a waiting request's requester gets email 2, once. A request that is approved or declined before then gets no reminder.
- **Timing.** A background job in the API checks every 15 minutes. It sends due reminders, then expires due requests. A reminder or expiry can therefore happen up to 15 minutes late.
- **A deleted entry.** If the Waitlist entry was removed in the WorkOS dashboard, CourtFlow marks the request Expired at its next check.

## Operator screens

### Access requests (`/operator/requests`)

A table of waiting requests, oldest first. Columns: name, email, organization name, message, requested, and expires. Approved, declined, and expired requests are not listed.

**Approve** opens a dialog with two choices:

- **New organization.** The organization name is filled in from the request and can be edited. It is required, trimmed, and at most 100 characters.
  - CourtFlow creates the organization as active and approves the Waitlist entry, which creates the WorkOS invitation.
  - CourtFlow records a CourtFlow invitation for the requester's email as owner, linked to that WorkOS invitation, expiring in 7 days.
  - CourtFlow sends email 3.
- **Existing organization.** The operator picks an active organization and a role.
  - CourtFlow approves the Waitlist entry and records a CourtFlow invitation for that organization and role.
  - CourtFlow sends email 4.

Approval is refused with a message, and nothing changes, if:

- the requester's email already has an active membership,
- the email has a pending CourtFlow invitation, or
- the chosen organization is deactivated.

**Decline** asks for confirmation. Then it denies the Waitlist entry, marks the request Declined, and sends email 5.

### Organizations (`/operator/organizations`)

A table of every organization: name, status (Active or Deactivated), number of members, and the owners' names.

- **Invite owner.** Takes an email.
  - CourtFlow creates a WorkOS invitation, records a CourtFlow invitation as owner, and sends email 7.
  - It is refused for the same reasons as owner invitations (see "Inviting" below).
  - This is how the Test Organization gets its first real owner.
- **Deactivate** asks for confirmation and names the organization. Then it:
  - sets the status to Deactivated,
  - cancels its pending invitations and revokes their WorkOS invitations,
  - emails every member (email 8).
- **Reactivate** asks for confirmation. Then it sets the status to Active and emails every member (email 9). Cancelled invitations stay cancelled.

While an organization is deactivated, its published tournaments are not shown on the public site. No public tournament pages exist yet, so this rule has nothing to hide until a public slice adds them. That slice must follow it. Reactivating puts them back.

## Organizations, memberships, and invitations

### The rules

- An organization has a name and a status: Active or Deactivated.
- A membership links one person to one organization with exactly one role: Owner, Editor, or Viewer. The records allow a person more than one membership. CourtFlow refuses to create a second one in this slice, and nothing on screen mentions more than one organization.
- An organization always keeps at least one owner. The domain refuses any change that would leave it with none: removing the last owner, demoting the last owner, or the last owner removing themselves.
- An invitation has an email, an organization, a role, who sent it (a member or an operator), a linked WorkOS invitation, an expiry 7 days after it was sent, and a state: Pending, Accepted, Cancelled, or Expired. Once the expiry passes, it counts as Expired, whether or not a job has marked it yet.

### Accepting an invitation

The link in every invitation email is the WorkOS invitation's own acceptance link. The person signs in or creates their sign-in on the WorkOS page and comes back through `/auth/callback`.

At every sign-in, CourtFlow looks for a pending, unexpired CourtFlow invitation whose email exactly matches the signed-in person's email, after trimming and lowercasing.

- **One is found and the person has no active membership:** the invitation becomes Accepted, and a membership is created with its role.
- **One is found but the person already has a membership:** the invitation stays pending and is not used. This can only happen if they were invited somewhere and then joined elsewhere.
- **None is found:** nothing happens. This covers someone who accepted a WorkOS invitation using a different address at the same company domain. They land on `/no-access`.

### Inviting (`/organization`, owners only)

The page title is "Organization · CourtFlow", and the `h1` is the organization's name.

**Members table:** name, email, role, and actions.

- **Changing a role** uses a role picker in the row and takes effect at once. The last owner's picker is disabled, with the tooltip "An organization needs at least one owner."
- **Remove** asks for confirmation. The last owner's Remove is disabled. An owner can remove themselves if another owner remains. They then lose access and land on `/no-access` at their next action.
- A removed person's WorkOS account is kept.

**Pending invitations table:** email, role, sent, expires, and actions.

- **Resend** always issues a fresh link. It revokes the old WorkOS invitation, creates a new one, resets the expiry to 7 days from now, and sends email 6 again. The old link stops working. Resend also works on expired invitations, which stay listed with "Expired" until resent or cancelled.
- **Cancel** asks for confirmation. Then it marks the invitation Cancelled and revokes the WorkOS invitation.

**Invite** opens a dialog with an email and a role (default Editor). Invitations are refused with a message if:

- the email already has a membership in this organization: "This person is already a member,"
- the email has a membership in another organization: "This person already belongs to another organization,"
- the email has a pending invitation to this organization: "There's already an invitation waiting for this email. Use Resend instead," or
- the email has a pending invitation to another organization: "This person already has an invitation to another organization."

There is no limit on the number of members.

## Sign-in page (WorkOS)

- WorkOS hosts it at its own web address in the free plan, with the CourtFlow logo, colors, and the display name "CourtFlow".
- Methods: email and password, and Google. The test environment uses WorkOS's shared Google setup. Production needs CourtFlow's own Google Cloud OAuth client, published so that any Google account can use it.
- WorkOS sends its own password reset and email confirmation code emails, branded as CourtFlow. Because CourtFlow sends invitations itself, a new person usually gets a confirmation code from WorkOS at their first sign-in.
- Sign-out returns to the landing page through WorkOS's configured sign-out address.

## No access page (`/no-access`)

Public frame. The `h1` is "You don't have access yet". The body depends on why:

- **No membership:** "Your account isn't part of an organization on CourtFlow yet. If you were invited, sign in with the email address the invitation was sent to."
- **Organization deactivated:** "{Organization name} has been deactivated. If you think this is a mistake, contact mdfelchlin@gmail.com."

Buttons: "Request Access" (to `/request-access`) and "Sign out". The page shows no tournament data and calls no organization API.

## Emails

### Sending

- Emails go through a small sending interface in the application layer. Deployed environments use **Resend**. Local runs use a **Mailpit** inbox started by Aspire. Automated tests capture emails in memory.
- The sender is `CourtFlow <no-reply@{Email:Domain}>`. Replies go to `mdfelchlin@gmail.com`.
- **CourtFlow has no domain yet.** `Email:Domain` is a setting. Until it is set, deployed environments can only use Resend's test sender, which delivers only to the Resend account owner's address.
- **Required before real customers:**
  - set the domain and verify it in Resend,
  - add the same domain as the WorkOS email domain, so password resets and confirmation codes stop coming from WorkOS's shared domain.
- **Delivery.** Each email is written to an outbox table in the same database change as the action that caused it. A background job sends it and retries a failed send up to 5 times over about an hour. A failed email never undoes the action, and failures are logged.
- **Format.** Each email has a plain-text body and a simple HTML body with the same words. The wording is the intent's drafts, emails 1 through 9.
- **Dates and links.** Dates appear as dates only, for example "October 20, 2026", so no time zone is needed. Links are built from the `Web:BaseUrl` setting, except invitation links, which come from WorkOS.

| Email | Sent to | When |
| --- | --- | --- |
| 1. New access request | Every operator | A request is sent |
| 2. Request expiring tomorrow | Requester | 24 hours before a waiting request expires |
| 3. Approved, new organization | Requester | Approved into a new organization |
| 4. Approved, existing organization | Requester | Approved into an existing organization |
| 5. Declined | Requester | Declined |
| 6. Invitation from an owner | Invited person | An owner invites or resends |
| 7. Invitation from an operator | New owner | An operator invites an owner |
| 8. Organization deactivated | Every member | Deactivated |
| 9. Organization reactivated | Every member | Reactivated |

## Configuration

| Setting | Where | Notes |
| --- | --- | --- |
| `WorkOS:ApiKey`, `WorkOS:ClientId` | API | Secret. Test environment keys locally, in user secrets. |
| `WorkOS:RedirectUri` | API | The API's `/auth/callback` |
| `Operators:Emails` | API | Starts with `mdfelchlin@gmail.com` |
| `Email:Domain`, `Email:ReplyTo`, `Resend:ApiKey` | API | Not set until a domain exists |
| `Turnstile:SiteKey` | Web | Cloudflare's always-pass test key locally |
| `Turnstile:SecretKey` | API | Cloudflare's always-pass test key locally |
| `Web:BaseUrl` | API | For links in emails and redirects after sign-in |
| `Cors:Origins` | API | Must allow credentials for the web origin |
| `Development:TestOrganizationOwners` | API | Development only. Ignored elsewhere. |

### Local development

- Local runs sign in through a WorkOS test environment. Aspire starts the API, the Blazor app, Postgres, and Mailpit.
- **Exception to the simple-local-startup constraint.** That spec says Aspire starts only the API, the Blazor app, and Postgres. Mailpit is a deliberate exception and must be called out in the plan.
- Anyone listed in `Development:TestOrganizationOwners` becomes an owner of the Test Organization the first time they sign in locally, if they have no membership.
- The Test Organization is still seeded in every environment. Outside development it starts with no members, until an operator invites its owner.

## How this is checked

- **Domain tests:**
  - the last-owner rule on remove, demote, and self-remove,
  - one-membership-per-person refusals,
  - invitation states and expiry,
  - access request states, including when a reminder is due.
- **API tests** use a test-only sign-in that sets the caller's WorkOS user ID, email, and name, and never call WorkOS. WorkOS, Resend, and Turnstile are replaced with fakes. They cover:
  - `401` signed out, `403` with each reason, and `404` across organizations,
  - every row of the "Who can do what" table, including viewers seeing only Published tournaments and getting `404` for an Unpublished or Archived one and its venues, divisions, and teams,
  - viewers refused (`403`) on every write and every lifecycle action, and the automatic archive still archiving every organization's due tournaments with no one signed in,
  - Request Access in each of its checks, in order,
  - approve into a new or existing organization, decline, and the refusals,
  - invite, resend, cancel, change role, remove, and every refusal message,
  - acceptance at sign-in: exact email match, mismatch, expired, cancelled, and already a member,
  - deactivate and reactivate, including cancelled invitations and members getting `organization_deactivated`,
  - the background job sending the reminder once and expiring the request, which deletes the Waitlist entry,
  - each action writing the right email to the outbox,
  - `returnUrl` refusing outside addresses.
- **Component tests:**
  - the public top bar in each of its three states,
  - the user menu items for owner, editor, viewer, and operator,
  - drawer items by role,
  - the no-access page's two bodies,
  - the Request Access form's validation and success state,
  - the Organization page's disabled last-owner controls.
- **Existing tests that change on purpose:**
  - The director layout test that checks for no "Login", "Sign in", or "Logout" text now expects "Sign out" in the user menu.
  - The public frame test now expects Request Access with Login when signed out.
  - Tests that rely on the fixed Test Organization use the test-only sign-in as a Test Organization owner.

## Decisions this spec makes that the intent did not

These were settled with the product owner while this spec was written.

- **CourtFlow's database is the source of truth** for organizations, memberships, roles, and invitation details. WorkOS handles only identity, sign-in, sessions, the invite gate, and Waitlist storage.
- **The API holds the session** in a secure cookie. The Blazor app and the API share a parent domain in production.
- **Viewers see Published tournaments only**, read-only, including their venues, divisions, and teams. They never see Unpublished or Archived tournaments. The lifecycle spec left the archived question to this spec.
- **Owners and editors publish, unpublish, archive, restore, and delete.** The lifecycle spec left who may do this to this spec.
- **Operators come from the `Operators:Emails` setting**, all with equal powers.
- **An operator can also be a member of an organization.** This replaces the intent's line that an operator is "not a member of any customer organization." Operator status alone never opens organization data.
- **Email** uses Resend in deployed environments, Mailpit locally, and in-memory capture in tests. The sender is `no-reply@{domain}`, and replies go to mdfelchlin@gmail.com.
- **CAPTCHA** is Cloudflare Turnstile, the cheapest option: free with no cap.
- **Waitlist mapping.** Approving approves the WorkOS entry and uses the invitation WorkOS creates. CourtFlow records the organization and role and sends its own email. Declining denies the entry. Expiring deletes it.
- **Local access** comes from a development-only list of Test Organization owners. Automated tests use a test-only sign-in.

These were decided while writing, to close edge cases. Each can be changed before the plan:

- An owner can remove themselves or demote themselves if another owner remains.
- Resend always issues a fresh 7-day link and works on expired invitations.
- Deactivating cancels pending invitations, and reactivating does not restore them.
- Request Access tells the person when their email already has access, has a waiting request, or was declined. That reveals those facts to anyone who types the email. Showing the same thank-you message in every case would hide them.
- Emails go through an outbox with retries.
- Dates in emails show no time.
- The background job runs every 15 minutes.
- The not-found page doesn't require sign-in. A page someone's role forbids shows "not found."

## Still open

- **The sending domain.** It must be chosen, then verified in Resend and in WorkOS, before real customers use CourtFlow.
- **A link to Request Access on the WorkOS sign-in page.** The plan confirms whether the free branding's split layout or custom copy can carry a link to `/request-access`. If not, the hidden "Sign up" link is the only change, and Request Access is reached from the landing page.
## Deliberately not in this slice

- Joining or switching between more than one organization.
- Two-step verification (MFA).
- Recording who made each change.
- A custom sign-in domain, or any paid WorkOS feature.
- Schedules and public tournament pages.
- An in-app way to add operators.
- Operators opening an organization's data for support.
- A history of approved, declined, or expired requests on the operator screen.
