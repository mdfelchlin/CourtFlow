using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfig : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(x => x.Id);
        b.Property(x => x.TypeName).HasMaxLength(768).IsRequired();
        b.Property(x => x.PayloadJson).IsRequired();
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.HasIndex(x => new { x.ProcessedAtUtc, x.CreatedAtUtc });
    }
}
