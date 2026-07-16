using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class LotteryDrawConfiguration : IEntityTypeConfiguration<LotteryDraw>
{
    public void Configure(EntityTypeBuilder<LotteryDraw> builder)
    {
        builder.ToTable("LotteryDraws");

        builder.HasKey(x => x.DrawId);

        builder.Property(x => x.ResultJson)
            .IsRequired()
            .HasMaxLength(8000);

        builder.HasOne(x => x.HousingProject)
            .WithMany(p => p.LotteryDraws)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DrawnByUser)
            .WithMany()
            .HasForeignKey(x => x.DrawnBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.DrawnAt);
    }
}
