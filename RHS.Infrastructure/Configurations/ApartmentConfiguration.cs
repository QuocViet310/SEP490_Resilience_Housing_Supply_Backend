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

        builder.Property(x => x.FloorNumber)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.BuildingBlock)
            .HasMaxLength(50);

        builder.Property(x => x.NumberOfBedrooms)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.NumberOfBathrooms)
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(x => x.MainDoorDirection)
            .HasMaxLength(20);

        builder.Property(x => x.BalconyDirection)
            .HasMaxLength(20);

        builder.Property(x => x.ViewDescription)
            .HasMaxLength(255);

        builder.Property(x => x.MinSuitableIncome)
            .HasPrecision(18, 2);

        builder.Property(x => x.MaxSuitableIncome)
            .HasPrecision(18, 2);

        builder.Property(x => x.UnitGroup)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(UnitGroupConstants.Standard);

        builder.Property(x => x.SaleType)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(SaleTypeConstants.FullOwnership);

        builder.Property(x => x.CoOwnershipRatio)
            .HasPrecision(5, 2);

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
        builder.HasIndex(x => new { x.ProjectId, x.FloorNumber });
        builder.HasIndex(x => new { x.ProjectId, x.BuildingBlock });
        builder.HasIndex(x => new { x.ProjectId, x.UnitGroup });
        builder.HasIndex(x => new { x.ProjectId, x.SaleType });
    }
}
