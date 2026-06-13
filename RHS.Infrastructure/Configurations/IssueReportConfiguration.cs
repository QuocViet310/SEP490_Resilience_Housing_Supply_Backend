using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class IssueReportConfiguration : IEntityTypeConfiguration<IssueReport>
{
    public void Configure(EntityTypeBuilder<IssueReport> builder)
    {
        builder.ToTable("IssueReports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Description)
            .IsRequired();

        builder.Property(x => x.IssueType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Open");

        builder.Property(x => x.ScreenshotUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.ResolvedAt);

        // Relationships
        builder.HasOne(x => x.User)
            .WithMany(x => x.IssueReports)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.IssueType);
        builder.HasIndex(x => x.CreatedAt);
    }
}
