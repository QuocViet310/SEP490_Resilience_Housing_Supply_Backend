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

        builder.Property(x => x.Street)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Ward)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LotteryDate)
            .IsRequired(false);

        builder.Property(x => x.LotteryLocation)
            .IsRequired(false)
            .HasMaxLength(255);

        builder.Property(x => x.LotteryType)
            .IsRequired(false)
            .HasMaxLength(50);

        builder.Property(x => x.LotteryDescription)
            .IsRequired(false)
            .HasMaxLength(2000);

        builder.Property(x => x.LotterySessionStatus)
            .IsRequired(false)
            .HasMaxLength(30);

        builder.Property(x => x.LotteryJoinCode)
            .IsRequired(false)
            .HasMaxLength(10);

        builder.Property(x => x.DepositAmount)
            .HasPrecision(18, 2);

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

        // New relationships
        builder.HasMany(x => x.HousingApplications)
            .WithOne(x => x.HousingProject)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.ProjectImages)
            .WithOne(x => x.HousingProject)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.HousingQuotas)
            .WithOne(x => x.HousingProject)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // New legal and developer mappings
        builder.Property(x => x.DecisionNumber)
            .IsRequired(false)
            .HasMaxLength(100);

        builder.Property(x => x.ApprovalDate)
            .IsRequired(false);

        builder.Property(x => x.IsConfirmed)
            .HasDefaultValue(false);

        builder.Property(x => x.ApplicationOpenDate)
            .IsRequired(false);

        builder.Property(x => x.ApplicationCloseDate)
            .IsRequired(false);

        builder.Property(x => x.RejectReason)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(x => x.PublicAnnounceAt)
            .IsRequired(false);

        builder.HasOne(x => x.Developer)
            .WithMany()
            .HasForeignKey(x => x.DeveloperId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter for soft delete
        builder.HasQueryFilter(x => !x.IsDeleted);

        // Indexes
        builder.HasIndex(x => x.Province);
        builder.HasIndex(x => x.District);
        builder.HasIndex(x => x.HousingProjectStatusId);
    }
}
