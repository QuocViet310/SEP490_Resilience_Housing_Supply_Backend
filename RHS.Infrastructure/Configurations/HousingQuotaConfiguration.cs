using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class HousingQuotaConfiguration : IEntityTypeConfiguration<HousingQuota>
{
    public void Configure(EntityTypeBuilder<HousingQuota> builder)
    {
        builder.ToTable("HousingQuotas");

        builder.HasKey(x => x.QuotaId);

        builder.Property(x => x.PriorityGroup)
            .IsRequired()
            .HasMaxLength(100);

        // Relationships
        builder.HasOne(x => x.HousingProject)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.ProjectId);
    }
}
