namespace CourtFlow.Domain;

public sealed class Organization
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";

    private Organization()
    {
    }

    public Organization(Guid id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("Organization name is required.");

        Id = id;
        Name = name.Trim();
    }
}
