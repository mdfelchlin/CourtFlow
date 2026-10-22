using CourtFlow.Domain;

namespace CourtFlow.Web.Services;

public static class RoleLabels
{
    public static string Of(OrganizationRole role) => role.ToString();
}