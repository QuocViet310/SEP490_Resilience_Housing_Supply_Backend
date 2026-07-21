using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class PaymentInstallmentConfiguration : IEntityTypeConfiguration<PaymentInstallment>
{
    public void Configure(EntityTypeBuilder<PaymentInstallment> builder)
    {
        builder.ToTable("PaymentInstallments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Note)
            .HasMaxLength(500);

        builder.HasOne(x => x.HousingApplication)
            .WithMany(a => a.PaymentInstallments)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Milestone)
            .WithMany(m => m.Installments)
            .HasForeignKey(x => x.MilestoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Payment)
            .WithMany()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Mỗi hồ sơ chỉ có 1 installment cho mỗi milestone
        builder.HasIndex(x => new { x.ApplicationId, x.MilestoneId }).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.DueDate);
    }
}
