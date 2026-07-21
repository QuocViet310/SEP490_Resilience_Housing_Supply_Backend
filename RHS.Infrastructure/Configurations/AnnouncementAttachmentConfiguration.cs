using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class AnnouncementAttachmentConfiguration : IEntityTypeConfiguration<AnnouncementAttachment>
{
    public void Configure(EntityTypeBuilder<AnnouncementAttachment> builder)
    {
        builder.ToTable("AnnouncementAttachments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.FileUrl)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FileSize)
            .IsRequired();

        // Relationship
        builder.HasOne(x => x.Announcement)
            .WithMany(a => a.Attachments)
            .HasForeignKey(x => x.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index
        builder.HasIndex(x => x.AnnouncementId);
    }
}
