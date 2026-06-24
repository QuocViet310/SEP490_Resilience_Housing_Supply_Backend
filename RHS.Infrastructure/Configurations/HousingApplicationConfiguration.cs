using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class HousingApplicationConfiguration : IEntityTypeConfiguration<HousingApplication>
{
    public void Configure(EntityTypeBuilder<HousingApplication> builder)
    {
        builder.ToTable("HousingApplications");

        builder.HasKey(x => x.ApplicationId);

        // ── Trạng thái hồ sơ ──────────────────────────────────────
        builder.Property(x => x.ApplicationStatus)
            .IsRequired()
            .HasMaxLength(50);

        // ── Điểm ưu tiên & Thu nhập ───────────────────────────────
        builder.Property(x => x.PriorityScore)
            .HasPrecision(18, 2);

        builder.Property(x => x.EstimatedMonthlyIncome)
            .HasPrecision(18, 2);

        builder.Property(x => x.SlotCode)
            .IsRequired(false)
            .HasMaxLength(50);

        // ── Thời gian ─────────────────────────────────────────────
        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false);

        // ── Form Fields: Thông tin cá nhân ────────────────────────
        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CitizenId)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Occupation)
            .IsRequired(false)
            .HasMaxLength(200);

        builder.Property(x => x.WorkPlace)
            .IsRequired(false)
            .HasMaxLength(500);

        // ── Form Fields: Thông tin địa chỉ & Nhà ở ───────────────
        builder.Property(x => x.CurrentResidence)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.PermanentAddress)
            .IsRequired()
            .HasMaxLength(500);

        /// Thực trạng nhà ở: "NO_HOUSE" hoặc "SMALL_HOUSE"
        builder.Property(x => x.HousingStatus)
            .IsRequired()
            .HasMaxLength(50);

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(x => x.Applicant)
            .WithMany(u => u.HousingApplications)
            .HasForeignKey(x => x.ApplicantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Officer)
            .WithMany(u => u.AssignedApplications)
            .HasForeignKey(x => x.OfficerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.HousingProject)
            .WithMany(p => p.HousingApplications)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Documents)
            .WithOne(x => x.HousingApplication)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.StatusHistories)
            .WithOne(x => x.Application)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Appointments)
            .WithOne(x => x.HousingApplication)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(x => x.ApplicantId);
        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.ApplicationStatus);
        builder.HasIndex(x => x.SubmittedAt);
        builder.HasIndex(x => x.CitizenId);
        builder.HasIndex(x => new { x.ApplicantId, x.ProjectId }).IsUnique();
    }
}

