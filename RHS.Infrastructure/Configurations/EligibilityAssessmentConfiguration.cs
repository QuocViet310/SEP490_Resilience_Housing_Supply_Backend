using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class EligibilityAssessmentConfiguration : IEntityTypeConfiguration<EligibilityAssessment>
{
    public void Configure(EntityTypeBuilder<EligibilityAssessment> builder)
    {
        builder.ToTable("EligibilityAssessments");

        builder.HasKey(x => x.AssessmentId);

        builder.Property(x => x.EstimatedScore)
            .HasPrecision(18, 2);

        builder.Property(x => x.ReasonsJson)
            .HasMaxLength(4000);

        // Relationships
        builder.HasOne(x => x.User)
            .WithMany(u => u.EligibilityAssessments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Application)
            .WithMany()
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ApplicationId);
        builder.HasIndex(x => x.AssessmentDate);
    }
}
