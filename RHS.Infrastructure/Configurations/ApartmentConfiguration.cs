using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Constants;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class ApartmentConfiguration : IEntityTypeConfiguration<Apartment>
{
    public void Configure(EntityTypeBuilder<Apartment> builder)
    {
        builder.ToTable("Apartments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UnitName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Price)
            .HasPrecision(18, 2);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(ApartmentStatusConstants.Available);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Model3DUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.VirtualTourUrl)
            .HasMaxLength(1000);

        builder.HasOne(x => x.HousingProject)
            .WithMany(p => p.Apartments)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ApartmentType)
            .WithMany(t => t.Apartments)
            .HasForeignKey(x => x.ApartmentTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}
