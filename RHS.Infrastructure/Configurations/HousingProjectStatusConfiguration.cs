using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class HousingProjectStatusConfiguration : IEntityTypeConfiguration<HousingProjectStatus>
{
    public void Configure(EntityTypeBuilder<HousingProjectStatus> builder)
    {
        builder.ToTable("HousingProjectStatuses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StatusName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.StatusCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.StatusCode)
            .IsUnique();
    }
}
