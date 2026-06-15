using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class PolicyConfigConfiguration : IEntityTypeConfiguration<PolicyConfig>
{
    public void Configure(EntityTypeBuilder<PolicyConfig> builder)
    {
        builder.ToTable("PolicyConfigs");

        builder.HasKey(x => x.PolicyId);

        builder.Property(x => x.PolicyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.PolicyValue)
            .IsRequired()
            .HasMaxLength(2000);

        // Relationships
        builder.HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.PolicyName).IsUnique();
        builder.HasIndex(x => x.EffectiveDate);
    }
}
