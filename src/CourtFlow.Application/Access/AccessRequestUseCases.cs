using CourtFlow.Application.Email;
using CourtFlow.Application.Organizations;
using CourtFlow.Domain;

namespace CourtFlow.Application.Access;

public sealed class AccessRequestUseCases(
    IAccessRepository repository,
    ICurrentAccess access,
    IWorkOs workOs,
    ICaptchaVerifier captcha,
    IEmailOutbox outbox,
    TimeProvider clock,
    OperatorOptions operators,
    WebOptions web)
{
    public const string CaptchaFailedMessage = "Please complete the check and try again.";
    public const string AlreadyHasAccessMessage = "This email already has access. Sign in instead.";
    public static readonly string AlreadyWaitingMessage =
        $"There's already a request waiting for this email. If you haven't heard back within 48 hours, contact {CourtFlowContact.Email}.";
    public static readonly string DeclinedMessage = $"This email can't send a new request. Contact {CourtFlowContact.Email}.";
    public static readonly string SentMessage =
        $"Thanks — we'll email you when your account is ready. If you haven't heard back within 48 hours, contact {CourtFlowContact.Email}.";
    public const string CouldNotSendMessage = "We couldn't send your request. Please try again in a few minutes.";

    public const string RequesterHasAccess = "This person already has access.";
    public const string RequesterHasInvitation = "This email already has a pending invitation.";
    public const string ChooseOrganization = "Choose a new organization or an existing one with a role.";

    /// <summary>
    /// The spec's checks in order; the first that fails stops the submission. A signed-in person's own email wins
    /// over the form's. The Waitlist entry is created before anything is saved, so a WorkOS failure stores nothing.
    /// </summary>
    public async Task<AccessRequestResult> SubmitAsync(SubmitAccessRequest form, CancellationToken cancellationToken)
    {
        if (!await captcha.VerifyAsync(form.CaptchaToken, cancellationToken))
            return new(AccessRequestOutcome.CaptchaFailed, CaptchaFailedMessage);

        var email = access.IsSignedIn && !string.IsNullOrWhiteSpace(access.Email) ? access.Email : form.Email;
        AccessRequest.Validate(form.Name, email, form.OrganizationName, form.Message);
        email = EmailAddress.Normalize(email);

        var memberships = await repository.OrganizationsWithMemberEmailAsync(email, cancellationToken);
        if (memberships.Any(organization => organization.IsActive))
            return new(AccessRequestOutcome.AlreadyHasAccess, AlreadyHasAccessMessage);

        var earlier = await repository.AccessRequestsForEmailAsync(email, cancellationToken);
        if (earlier.Any(request => request.State == AccessRequestState.Waiting))
            return new(AccessRequestOutcome.AlreadyWaiting, AlreadyWaitingMessage);
        if (earlier.Any(request => request.State == AccessRequestState.Declined))
            return new(AccessRequestOutcome.Declined, DeclinedMessage);

        string entryId;
        try
        {
            entryId = await workOs.CreateWaitlistEntryAsync(email, Fields(form), cancellationToken);
        }
        catch (WorkOsUnavailableException)
        {
            return new(AccessRequestOutcome.CouldNotSend, CouldNotSendMessage);
        }

        var created = AccessRequest.Create(entryId, form.Name, email, form.OrganizationName, form.Message, clock.GetUtcNow());
        repository.AddAccessRequest(created);
        var recipients = operators.Normalized();
        if (recipients.Count > 0)
            outbox.Enqueue(EmailTemplates.NewAccessRequest(recipients, created, web.Link(SignInUseCases.OperatorRequests)));

        await repository.SaveChangesAsync(cancellationToken);
        return new(AccessRequestOutcome.Sent, SentMessage);
    }

    public async Task<IReadOnlyList<AccessRequestDetails>> ListWaitingAsync(CancellationToken cancellationToken) =>
        (await repository.WaitingAccessRequestsAsync(cancellationToken))
            .OrderBy(request => request.RequestedAt)
            .Select(request => new AccessRequestDetails(
                request.Id,
                request.Name,
                request.Email,
                request.OrganizationName,
                request.Message,
                request.RequestedAt,
                request.ExpiresAt))
            .ToList();

    public async Task<bool> ApproveAsync(Guid id, ApproveAccessRequest command, CancellationToken cancellationToken)
    {
        var request = await repository.GetAccessRequestAsync(id, cancellationToken);
        if (request is null)
            return false;
        if (request.State != AccessRequestState.Waiting)
            throw new DomainRuleException(AccessRequest.NotWaiting);

        var now = clock.GetUtcNow();
        var memberships = await repository.OrganizationsWithMemberEmailAsync(request.Email, cancellationToken);
        if (memberships.Any(organization => organization.IsActive))
            throw new DomainRuleException(RequesterHasAccess);
        if ((await repository.InvitationsForEmailAsync(request.Email, cancellationToken)).Any(invitation => invitation.IsOpen(now)))
            throw new DomainRuleException(RequesterHasInvitation);

        Organization organization;
        OrganizationRole role;
        var isNew = command.NewOrganizationName is not null;
        if (isNew)
        {
            organization = Organization.Create(command.NewOrganizationName);
            role = OrganizationRole.Owner;
        }
        else if (command is { OrganizationId: { } organizationId, Role: { } chosenRole })
        {
            organization = await repository.GetOrganizationAsync(organizationId, cancellationToken)
                ?? throw new DomainRuleException("Organization was not found.");
            if (!organization.IsActive)
                throw new DomainRuleException(InvitationRules.Deactivated);
            role = chosenRole;
        }
        else
        {
            throw new DomainRuleException(ChooseOrganization);
        }

        var invitation = request.WaitlistEntryId.Length > 0
            ? await workOs.ApproveWaitlistEntryAsync(request.WaitlistEntryId, request.Email, cancellationToken)
            : await workOs.SendInvitationAsync(request.Email, cancellationToken);

        if (isNew)
            repository.AddOrganization(organization);

        var recorded = Invitation.Create(organization.Id, request.Email, role, null, access.Email, invitation.Id, now);
        repository.AddInvitation(recorded);
        request.Approve();

        var link = invitation.AcceptUrl ?? web.Link(SignInUseCases.Tournaments);
        outbox.Enqueue(isNew
            ? EmailTemplates.ApprovedNewOrganization(request.Email, request.Name, organization.Name, link, recorded.ExpiresAt)
            : EmailTemplates.ApprovedExistingOrganization(request.Email, request.Name, organization.Name, role, link, recorded.ExpiresAt));

        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeclineAsync(Guid id, CancellationToken cancellationToken)
    {
        var request = await repository.GetAccessRequestAsync(id, cancellationToken);
        if (request is null)
            return false;

        request.Decline();
        if (request.WaitlistEntryId.Length > 0)
            await workOs.DenyWaitlistEntryAsync(request.WaitlistEntryId, cancellationToken);

        outbox.Enqueue(EmailTemplates.Declined(request.Email, request.Name, request.OrganizationName));
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static Dictionary<string, string> Fields(SubmitAccessRequest form)
    {
        var fields = new Dictionary<string, string>
        {
            ["name"] = form.Name!.Trim(),
            ["organization_name"] = form.OrganizationName!.Trim(),
        };
        if (!string.IsNullOrWhiteSpace(form.Message))
            fields["message"] = Truncate(form.Message.Trim(), 600);

        return fields;
    }

    /// <summary>WorkOS keeps up to 600 characters per field. CourtFlow keeps the whole message itself.</summary>
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
