using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Configurations;

public class WishlistConfiguration : IEntityTypeConfiguration<Wishlist>
{
    public void Configure(EntityTypeBuilder<Wishlist> builder)
    {
        builder.ToTable("Wishlists");

        builder.HasKey(x => x.Id);

        // ── Thời gian ─────────────────────────────────────────────────────
        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // ── Unique constraint: mỗi user chỉ thêm một project 1 lần ───────
        builder.HasIndex(x => new { x.UserId, x.HousingProjectId })
            .IsUnique();

        // ── Index riêng cho UserId để tăng tốc query GET wishlist ─────────
        builder.HasIndex(x => x.UserId);

        // ── Relationships ─────────────────────────────────────────────────

        // Khi User bị xóa → xóa toàn bộ wishlist của họ (Cascade)
        builder.HasOne(x => x.User)
            .WithMany(u => u.Wishlists)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Khi HousingProject bị xóa → xóa các wishlist liên quan (Cascade)
        builder.HasOne(x => x.HousingProject)
            .WithMany(p => p.Wishlists)
            .HasForeignKey(x => x.HousingProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
