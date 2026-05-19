using CourtFlow.Domain.Common;

namespace CourtFlow.Domain.Entities;

public class PoolCourtAllocation : Entity, ITenantScoped
{
    public Guid OrganizationId { get; private set; }
    public Guid PoolId { get; private set; }
    public Guid CourtId { get; private set; }
    public string AllocationType { get; private set; } = "preferred";
    public int PriorityOrder { get; private set; }

    private PoolCourtAllocation() { }

    public PoolCourtAllocation(Guid organizationId, Guid poolId, Guid courtId, string allocationType = "preferred", int priorityOrder = 0)
    {
        OrganizationId = organizationId;
        PoolId = poolId;
        CourtId = courtId;
        AllocationType = allocationType;
        PriorityOrder = priorityOrder;
    }

    public void SetAllocationType(string allocationType)
    {
        AllocationType = allocationType;
        Touch();
    }

    public void SetPriorityOrder(int priorityOrder)
    {
        PriorityOrder = priorityOrder;
        Touch();
    }
}
