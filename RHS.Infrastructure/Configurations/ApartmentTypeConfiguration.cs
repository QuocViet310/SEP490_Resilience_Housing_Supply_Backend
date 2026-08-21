using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class ApartmentTypeConfiguration : IEntityTypeConfiguration<ApartmentType>
{
    public void Configure(EntityTypeBuilder<ApartmentType> builder)
    {
        builder.ToTable("ApartmentTypes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TypeCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.TypeName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => x.TypeCode)
            .IsUnique();
    }
}
