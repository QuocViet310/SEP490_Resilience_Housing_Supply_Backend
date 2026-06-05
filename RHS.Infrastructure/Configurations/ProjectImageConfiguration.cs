using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class ProjectImageConfiguration : IEntityTypeConfiguration<ProjectImage>
{
    public void Configure(EntityTypeBuilder<ProjectImage> builder)
    {
        builder.ToTable("ProjectImages");

        builder.HasKey(x => x.ImageId);

        builder.Property(x => x.ImageUrl)
            .IsRequired()
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(x => x.HousingProject)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.ProjectId);
    }
}
