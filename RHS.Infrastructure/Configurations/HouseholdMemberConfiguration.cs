using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.ToTable("HouseholdMembers");

        builder.HasKey(x => x.MemberId);

        // ── Thông tin thành viên ──────────────────────────────────
        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CitizenId)
            .IsRequired(false)
            .HasMaxLength(20);

        builder.Property(x => x.DateOfBirth)
            .IsRequired(false);

        builder.Property(x => x.Relationship)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Note)
            .IsRequired(false)
            .HasMaxLength(500);

        // ── Thời gian ────────────────────────────────────────────
        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false);

        // ── Relationships ────────────────────────────────────────
        builder.HasOne(x => x.HousingApplication)
            .WithMany(x => x.HouseholdMembers)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──────────────────────────────────────────────
        builder.HasIndex(x => x.ApplicationId);
        builder.HasIndex(x => x.CitizenId);

        // 1 CCCD chỉ xuất hiện 1 lần trong cùng 1 hồ sơ
        builder.HasIndex(x => new { x.ApplicationId, x.CitizenId })
            .IsUnique()
            .HasFilter("[CitizenId] IS NOT NULL");
    }
}
