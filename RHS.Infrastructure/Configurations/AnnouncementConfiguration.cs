using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Content)
            .IsRequired();

        builder.Property(x => x.AnnouncementType)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("General");

        builder.Property(x => x.LegalDocumentNumber)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Draft");

        builder.Property(x => x.IsPinned)
            .HasDefaultValue(false);

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(x => x.CreatedByUser)
            .WithMany(u => u.Announcements)
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Project)
            .WithMany(p => p.Announcements)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AnnouncementType);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.IsPinned);
        builder.HasIndex(x => x.IsDeleted);
        builder.HasIndex(x => x.EffectiveDate);

        // Global query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
