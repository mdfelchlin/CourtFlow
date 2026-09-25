using CourtFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CourtFlow.Infrastructure.Persistence.Configurations;

public sealed class ImportJobConfig : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> b)
    {
        b.ToTable("import_jobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.HasMany(x => x.Rows).WithOne().HasForeignKey(r => r.ImportJobId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.TournamentId);
    }
}

public sealed class ImportRowConfig : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> b)
    {
        b.ToTable("import_rows");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.ValidationErrors).HasMaxLength(2000);
        b.HasIndex(x => new { x.ImportJobId, x.RowNumber }).IsUnique();
    }
}
