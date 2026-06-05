using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class HousingApplicationConfiguration : IEntityTypeConfiguration<HousingApplication>
{
    public void Configure(EntityTypeBuilder<HousingApplication> builder)
    {
        builder.ToTable("HousingApplications");

        builder.HasKey(x => x.ApplicationId);

        builder.Property(x => x.ApplicationStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.PriorityScore)
            .HasPrecision(18, 2);

        builder.Property(x => x.EstimatedMonthlyIncome)
            .HasPrecision(18, 2);

        // Relationships
        builder.HasOne(x => x.Applicant)
            .WithMany(u => u.HousingApplications)
            .HasForeignKey(x => x.ApplicantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Officer)
            .WithMany(u => u.AssignedApplications)
            .HasForeignKey(x => x.OfficerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HousingProject)
            .WithMany(p => p.HousingApplications)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Documents)
            .WithOne(x => x.HousingApplication)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.StatusHistories)
            .WithOne(x => x.Application)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Appointments)
            .WithOne(x => x.HousingApplication)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.ApplicantId);
        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.ApplicationStatus);
        builder.HasIndex(x => x.SubmittedAt);
        builder.HasIndex(x => new { x.ApplicantId, x.ProjectId }).IsUnique();
    }
}
