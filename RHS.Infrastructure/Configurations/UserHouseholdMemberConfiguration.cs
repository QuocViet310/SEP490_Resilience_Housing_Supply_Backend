using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class UserHouseholdMemberConfiguration : IEntityTypeConfiguration<UserHouseholdMember>
{
    public void Configure(EntityTypeBuilder<UserHouseholdMember> builder)
    {
        builder.ToTable("UserHouseholdMembers");

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

        builder.Property(x => x.Occupation)
            .IsRequired(false)
            .HasMaxLength(200);

        builder.Property(x => x.MonthlyIncome)
            .HasColumnType("decimal(18,2)")
            .IsRequired(false);

        builder.Property(x => x.IsDependent)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.DependentReason)
            .IsRequired(false)
            .HasMaxLength(50);

        builder.Property(x => x.HasMeritService)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.MeritDetails)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(x => x.Note)
            .IsRequired(false)
            .HasMaxLength(500);

        // ── Thời gian ────────────────────────────────────────────
        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false);

        // ── Relationships ────────────────────────────────────────
        builder.HasOne(x => x.User)
            .WithMany(x => x.UserHouseholdMembers)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ──────────────────────────────────────────────
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CitizenId);

        // 1 CCCD chỉ xuất hiện 1 lần trong sổ hộ khẩu của cùng 1 user
        builder.HasIndex(x => new { x.UserId, x.CitizenId })
            .IsUnique()
            .HasFilter("[CitizenId] IS NOT NULL");
    }
}
