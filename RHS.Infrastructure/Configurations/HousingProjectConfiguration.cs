using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class HousingProjectConfiguration : IEntityTypeConfiguration<HousingProject>
{
    public void Configure(EntityTypeBuilder<HousingProject> builder)
    {
        builder.ToTable("HousingProjects");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProjectName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.Province)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.District)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.MinPrice)
            .HasPrecision(18, 2);

        builder.Property(x => x.MaxPrice)
            .HasPrecision(18, 2);

        builder.Property(x => x.ThumbnailUrl)
            .HasMaxLength(500);

        builder.Property(x => x.IsDeleted)
            .HasDefaultValue(false);

        // Relationship configuration
        builder.HasOne(x => x.HousingProjectStatus)
            .WithMany(x => x.HousingProjects)
            .HasForeignKey(x => x.HousingProjectStatusId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Indexes
        builder.HasIndex(x => x.Province);
        builder.HasIndex(x => x.District);
        builder.HasIndex(x => x.HousingProjectStatusId);
    }
}
