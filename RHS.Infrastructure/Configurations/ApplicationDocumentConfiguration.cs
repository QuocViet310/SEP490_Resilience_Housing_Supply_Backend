using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class ApplicationDocumentConfiguration : IEntityTypeConfiguration<ApplicationDocument>
{
    public void Configure(EntityTypeBuilder<ApplicationDocument> builder)
    {
        builder.ToTable("ApplicationDocuments");

        builder.HasKey(x => x.DocumentId);

        builder.Property(x => x.DocumentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FileUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.VerificationStatus)
            .IsRequired()
            .HasMaxLength(50);

        // Relationships
        builder.HasOne(x => x.HousingApplication)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.VerificationResult)
            .WithOne(x => x.Document)
            .HasForeignKey<AIVerificationResult>(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(x => x.ApplicationId);
        builder.HasIndex(x => x.UploadedAt);
    }
}
