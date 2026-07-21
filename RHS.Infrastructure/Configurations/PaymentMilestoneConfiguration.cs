using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class PaymentMilestoneConfiguration : IEntityTypeConfiguration<PaymentMilestone>
{
    public void Configure(EntityTypeBuilder<PaymentMilestone> builder)
    {
        builder.ToTable("PaymentMilestones");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PhaseName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CalculationType)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.TriggerEvent)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.FixedAmount)
            .HasPrecision(18, 2);

        builder.Property(x => x.Percentage)
            .HasPrecision(5, 2); // max 100.00

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(x => x.HousingProject)
            .WithMany(p => p.PaymentMilestones)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mỗi project chỉ có 1 milestone ở mỗi PhaseOrder
        builder.HasIndex(x => new { x.ProjectId, x.PhaseOrder }).IsUnique();
    }
}
