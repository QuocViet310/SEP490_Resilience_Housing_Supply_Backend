using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.HousingProjectId);
        builder.HasIndex(x => x.ApplicationId);

        builder.Property(x => x.OrderId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.OrderInfo)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Amount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.VnpResponseCode)
            .HasMaxLength(10);

        builder.Property(x => x.VnpTransactionNo)
            .HasMaxLength(50);

        builder.Property(x => x.VnpBankCode)
            .HasMaxLength(20);

        builder.Property(x => x.VnpBankTranNo)
            .HasMaxLength(50);

        builder.Property(x => x.VnpCardType)
            .HasMaxLength(20);

        builder.Property(x => x.VnpPayDate)
            .HasMaxLength(20);

        builder.Property(x => x.VnpTransactionStatus)
            .HasMaxLength(10);

        // Relationships
        builder.HasOne(x => x.User)
            .WithMany(u => u.Payments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.HousingProject)
            .WithMany()
            .HasForeignKey(x => x.HousingProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.HousingApplication)
            .WithMany()
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
