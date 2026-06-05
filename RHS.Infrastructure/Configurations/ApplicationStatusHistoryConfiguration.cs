using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class ApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<ApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusHistory> builder)
    {
        builder.ToTable("ApplicationStatusHistories");

        builder.HasKey(x => x.HistoryId);

        builder.Property(x => x.OldStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.NewStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Note)
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(x => x.Application)
            .WithMany(x => x.StatusHistories)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.ApplicationId);
        builder.HasIndex(x => x.ChangedAt);
    }
}
