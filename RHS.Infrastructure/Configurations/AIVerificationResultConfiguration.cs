using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class AIVerificationResultConfiguration : IEntityTypeConfiguration<AIVerificationResult>
{
    public void Configure(EntityTypeBuilder<AIVerificationResult> builder)
    {
        builder.ToTable("AIVerificationResults");

        builder.HasKey(x => x.VerificationId);

        builder.Property(x => x.ExtractedText)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.FaceMatchScore)
            .HasPrecision(5, 2);

        builder.Property(x => x.RiskScore)
            .HasPrecision(5, 2);

        builder.Property(x => x.ValidationResult)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ExtractedFullName)
            .HasMaxLength(255);

        builder.Property(x => x.ExtractedCitizenId)
            .HasMaxLength(50);

        builder.Property(x => x.ExtractedAddress)
            .HasMaxLength(500);

        builder.Property(x => x.ExtractedDateOfBirth)
            .HasMaxLength(50);

        builder.Property(x => x.ErrorDetails)
            .HasMaxLength(1000);

        builder.Property(x => x.AiModelUsed)
            .HasMaxLength(100);

        // Relationships
        builder.HasOne(x => x.Document)
            .WithOne(x => x.VerificationResult)
            .HasForeignKey<AIVerificationResult>(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.DocumentId);
        builder.HasIndex(x => x.VerifiedAt);
    }
}
