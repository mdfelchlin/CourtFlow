# Intent: Sign-in and access
Author: product owner. Status: accepted.

## Problem
Anyone can reach the director app. The landing page's "Login" button goes straight to the tournament list without asking who the visitor is, and every tournament, club, and team belongs to one built-in "Test Organization." A director cannot sign in, cannot see only their own organization's work, cannot ask for an account, and cannot bring other people in to help run the schedule.

## Proposed outcome
A director or organizer arrives at the landing page and signs in. Someone without an account asks for one with "Request Access," and a CourtFlow operator decides whether to let them in. Once signed in, a person sees their own organization's tournaments and everything linked to them. Owners manage who else in their organization can work on the schedule. Anyone signed in can visit the public site without signing out, and can sign out to return to the landing page.

### Requesting access
- The public top bar shows "Login" and "Request Access" when no one is signed in.
- "Request Access" asks for name, email, organization name, and an optional message. The form requires a CAPTCHA.
- Requests are kept in the WorkOS Waitlist. CourtFlow adds what the Waitlist lacks: its own form with the extra fields, the operator screen, approval into an organization with a role, the decline email, and the 7-day expiry with its reminder.
- Sending the form emails the CourtFlow operator and shows "Thanks — we'll email you when your account is ready." It does not create an account. The on-screen confirmation is the only acknowledgment; WorkOS's own "we got your request" email is turned off.
- The confirmation also says to contact mdfelchlin@gmail.com if the requester has not heard back within 48 hours.
- An email that already has a request waiting cannot send another one.
- A request that no one acts on expires after 7 days. The requester gets a reminder email 24 hours before it expires. After it expires, the same email can send a new request.
- A declined email cannot send a new request. The person contacts the operator instead. This matches how the WorkOS Waitlist treats declined entries.
- The WorkOS sign-in page's own "Sign up" link is hidden, because its Waitlist form only asks for an email. The sign-in page points people to CourtFlow's Request Access instead.

### Operator review
- An operator is a CourtFlow staff member, not a member of any customer organization. Operators are named by email address in configuration.
- mdfelchlin@gmail.com is the master operator. More operators can be added later. The spec decides how.
- Operators have their own screen that lists waiting requests. For each one, the operator can:
  - approve it by creating a new organization with the requester as its owner,
  - approve it by adding the requester to an existing organization with a chosen role, or
  - decline it.
- The requester is emailed either way. Approval emails an invitation to sign in. Declining emails a short note.
- The operator can also invite an owner straight into an existing organization. This is how the current Test Organization gets a real owner. Its data is kept.
- An operator can deactivate an organization, for example when a customer leaves, and reactivate it later. While it is deactivated, its members cannot get in, and they see the "You don't have access yet" page if they sign in. Its published tournaments are removed from the public site. Its data is kept.
- Deactivating an organization emails all of its members.
- Reactivating an organization restores its members' access and puts its published tournaments back on the public site. All of its members are emailed.

### Signing in
- "Login" opens a sign-in page that offers email and password, or "Sign in with Google."
- Only invited people can create a sign-in. Anyone else is pointed to Request Access.
- An invited person can use either method, as long as the email exactly matches the invitation. CourtFlow checks this itself, because WorkOS would otherwise let someone at the same company domain accept an invitation sent to a coworker.
- A new person may be asked to confirm their email with a code during their first sign-in. WorkOS requires this.
- Forgotten passwords are reset through the sign-in page.
- After signing in, a person lands on their organization's tournament list.
- A sign-in lasts 30 days, whether or not the person uses CourtFlow in between. After that, the person signs in again.
- Someone who signs in but has no organization, or whose organization is deactivated, sees a "You don't have access yet" page with "Request Access" and "Sign out." They see no tournament data.
- Someone who is signed out and opens a director page directly, for example from a bookmark, is sent to sign in and then returned to that page.

### Organizations, roles, and members
- Each person belongs to one organization in this slice. Records can hold more than one membership per person, so a later slice can add a way to switch without reworking the data. Nothing on screen mentions more than one organization.
- An organization has three roles:
  - **Owner**: everything an editor can do, plus inviting, managing, and removing people.
  - **Editor**: changes tournaments, teams, schedules, and the rest of the organization's work.
  - **Viewer**: sees the organization's published tournaments, including draft schedules for those tournaments, but cannot change anything. Viewers do not see unpublished tournaments.
- There is no limit on the number of people in an organization.
- An owner invites someone by email and picks a role. The invitation link expires after 7 days.
- The organization page lists members and pending invitations. An owner can resend or cancel an invitation, change a member's role, and remove a member.
- An organization always keeps at least one owner. The last owner cannot be removed or demoted.
- Inviting someone who already belongs to another organization is refused with a clear message.
- A removed member loses access on their next action, not just at their next sign-in.

### Moving between the public site and the director app
- When signed in, the public top bar shows "Dashboard" in place of "Login," and "Request Access" is hidden.
- The director app bar has a user menu with the person's name. It holds "View public site," which keeps them signed in, "Organization," which only owners see, and "Sign out."
- "Sign out" ends the session and returns to the landing page.

### Emails
CourtFlow sends the emails below itself, including every invitation, because WorkOS's own invitation email can't use this wording. WorkOS still sends its password reset and email confirmation codes, branded as CourtFlow. Before real customers use CourtFlow, those come from CourtFlow's own sending domain instead of WorkOS's shared one, which WorkOS only delivers on a best-effort basis.

These are first drafts of the wording. Text in `{{ }}` is filled in when the email is sent. The sender address is decided in the spec.

**1. New access request (to operators)**

> Subject: CourtFlow access request from {{name}}
>
> {{name}} ({{email}}) has asked for access to CourtFlow.
>
> Organization: {{organizationName}}
> Message: {{message or "None"}}
> Requested: {{requestedAt}}
> Expires: {{expiresAt}}
>
> Review it here: {{operatorRequestsLink}}

**2. Request expiring tomorrow (to the requester)**

> Subject: Your CourtFlow access request is still waiting
>
> Hi {{name}},
>
> We haven't finished reviewing your request for {{organizationName}} yet, and it expires on {{expiresAt}}. If it expires, you can send a new one from {{landingPageLink}}.
>
> If you'd like to follow up, reply to mdfelchlin@gmail.com.
>
> The CourtFlow team

**3. Request approved: new organization (to the requester)**

> Subject: Your CourtFlow account is ready
>
> Hi {{name}},
>
> Your request was approved. {{organizationName}} is set up on CourtFlow, and you are its owner.
>
> Sign in with this email address, using a password or Google: {{acceptInvitationLink}}
>
> This link expires on {{invitationExpiresAt}}.
>
> The CourtFlow team

**4. Request approved: added to an existing organization (to the requester)**

> Subject: You've been added to {{organizationName}} on CourtFlow
>
> Hi {{name}},
>
> Your request was approved. You've been added to {{organizationName}} as {{a/an role}}.
>
> Sign in with this email address, using a password or Google: {{acceptInvitationLink}}
>
> This link expires on {{invitationExpiresAt}}.
>
> The CourtFlow team

**5. Request declined (to the requester)**

> Subject: Your CourtFlow access request
>
> Hi {{name}},
>
> Thanks for your interest in CourtFlow. We're not able to set up access for {{organizationName}} right now.
>
> If you have questions, contact mdfelchlin@gmail.com.
>
> The CourtFlow team

**6. Invitation from an owner (to the invited person, also used when it's resent)**

> Subject: {{inviterName}} invited you to {{organizationName}} on CourtFlow
>
> Hi,
>
> {{inviterName}} invited you to join {{organizationName}} on CourtFlow as {{a/an role}}.
>
> Accept the invitation and sign in with this email address, using a password or Google: {{acceptInvitationLink}}
>
> This link expires on {{invitationExpiresAt}}. If you weren't expecting this, you can ignore it.
>
> The CourtFlow team

**7. Invitation from an operator to an existing organization (to the new owner)**

> Subject: You're the owner of {{organizationName}} on CourtFlow
>
> Hi,
>
> You've been made an owner of {{organizationName}} on CourtFlow.
>
> Sign in with this email address, using a password or Google: {{acceptInvitationLink}}
>
> This link expires on {{invitationExpiresAt}}.
>
> The CourtFlow team

**8. Organization deactivated (to every member)**

> Subject: {{organizationName}} has been deactivated on CourtFlow
>
> Hi {{name}},
>
> {{organizationName}} has been deactivated on CourtFlow. You can no longer sign in to it, and its tournaments no longer appear on the public site. Its information has been kept.
>
> If you think this is a mistake, contact mdfelchlin@gmail.com.
>
> The CourtFlow team

**9. Organization reactivated (to every member)**

> Subject: {{organizationName}} is active again on CourtFlow
>
> Hi {{name}},
>
> {{organizationName}} has been reactivated on CourtFlow. You can sign in again at {{signInLink}}, and its published tournaments are back on the public site.
>
> The CourtFlow team

## Affected users and systems
Directors and organizers now sign in, and are owners, editors, or viewers in one organization. CourtFlow operators review access requests and set up organizations. Coaches, players, and spectators still have no account and still use the public site.

This touches the Blazor WebAssembly app (public top bar, director app bar, sign-in, Request Access, "You don't have access yet" page, organization page, and operator screen), the .NET API (who is signed in, which organization's data they can see, and what their role lets them change), the database (people, memberships, roles, invitations, and access requests), and outgoing email. It replaces the built-in Test Organization scope with the signed-in person's organization. It changes the public landing page slice's rule that "Login" goes straight to the tournament list.

## Constraints
- WorkOS handles sign-in, as the product intent already decided.
- Only WorkOS's free features are used for now. Without the paid custom domain, the sign-in page lives at a WorkOS web address, branded with the CourtFlow logo and colors.
- In production, "Sign in with Google" needs CourtFlow's own Google sign-in setup. The test environment uses WorkOS's shared Google setup, which shows WorkOS's name on Google's screen.
- Sign-in offers email and password, and Google.
- Directors and organizers are the only people with accounts. Public visitors keep reading without one.
- Each organization's data stays separate from every other organization's.
- One organization per person in this slice. CourtFlow enforces this, because WorkOS allows a person in many organizations. Records still allow several memberships per person.
- Sign-up is invite-only. Access requests live in the WorkOS Waitlist, with CourtFlow adding what it lacks.
- The Request Access form requires a CAPTCHA.
- Owner, editor, and viewer are the only roles. Viewers cannot change anything and cannot see unpublished tournaments.
- mdfelchlin@gmail.com is the master operator.
- Access requests expire after 7 days, with a reminder to the requester 24 hours before. Sign-ins last 30 days.
- Requesters who haven't heard back within 48 hours are told to contact mdfelchlin@gmail.com.
- Local development signs in through a WorkOS test environment. Automated tests do not call WorkOS.
- No limit on organization size.
- Request Access never creates an account on its own. An operator approves every new person who doesn't come through an owner's invitation.
- The existing Test Organization and its data are kept.

## Open questions
- Email provider and sender address. Left to the spec.
- How more operators get added: by editing configuration, or by the master operator from inside the app. Left to the spec.
- Which CAPTCHA service, preferably one with a free tier. Left to the spec.
- How an approval in CourtFlow maps onto the Waitlist. WorkOS's own approval creates an invitation with no organization, so the spec decides how CourtFlow invites the person into the right organization with the right role. Left to the spec.
- Later slices: joining and switching between more than one organization, two-step verification (MFA), recording who made each change, and a custom sign-in domain once paid WorkOS features are in scope.
