using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations
{
    public class PrincipleAgreementConfiguration : IEntityTypeConfiguration<PrincipleAgreement>
    {
        public void Configure(EntityTypeBuilder<PrincipleAgreement> builder)
        {
            builder.ToTable("PrincipleAgreements");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ApplicationId)
                .IsRequired();

            builder.Property(x => x.PdfUrl)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsSigned)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(x => x.SignedAt)
                .IsRequired(false);

            builder.Property(x => x.SignedIpAddress)
                .IsRequired(false)
                .HasMaxLength(50);

            // 1-to-1 Relationship with HousingApplication
            builder.HasOne(x => x.HousingApplication)
                .WithOne(x => x.PrincipleAgreement)
                .HasForeignKey<PrincipleAgreement>(x => x.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
