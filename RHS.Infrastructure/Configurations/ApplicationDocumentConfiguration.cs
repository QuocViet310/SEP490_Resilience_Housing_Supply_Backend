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

        // ── Loại giấy tờ ─────────────────────────────────────────
        // Giá trị hợp lệ: xem DocumentTypeConstants
        builder.Property(x => x.DocumentType)
            .IsRequired()
            .HasMaxLength(100);

        // ── Thông tin file ────────────────────────────────────────
        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.FileUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.FileSizeBytes)
            .IsRequired();

        // ── Trạng thái xác minh ───────────────────────────────────
        // Mặc định: "PENDING". Giá trị: PENDING, VERIFIED, REJECTED
        builder.Property(x => x.VerificationStatus)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("PENDING");

        // ── Relationships ─────────────────────────────────────────
        builder.HasOne(x => x.HousingApplication)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Người upload: Restrict để bảo toàn lịch sử dù User bị xóa
        builder.HasOne(x => x.UploadedByUser)
            .WithMany()
            .HasForeignKey(x => x.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VerificationResult)
            .WithOne(x => x.Document)
            .HasForeignKey<AIVerificationResult>(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────
        builder.HasIndex(x => x.ApplicationId);
        builder.HasIndex(x => x.UploadedAt);
        builder.HasIndex(x => x.DocumentType);
        builder.HasIndex(x => x.UploadedBy);
    }
}

