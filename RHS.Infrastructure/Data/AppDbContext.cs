using Microsoft.EntityFrameworkCore;
using RHS.Domain.Entities;
using RHS.Infrastructure.Configurations;

namespace RHS.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<OtpCode> OtpCodes { get; set; }
    public DbSet<HousingProject> HousingProjects { get; set; }
    public DbSet<HousingProjectStatus> HousingProjectStatuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfiguration(new HousingProjectStatusConfiguration());
        modelBuilder.ApplyConfiguration(new HousingProjectConfiguration());

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.GoogleId);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProfileImageUrl).HasMaxLength(500);
        });

        // RefreshToken Configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // OtpCode Configuration
        modelBuilder.Entity<OtpCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Code, e.Purpose });
            entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Purpose).IsRequired().HasMaxLength(50);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.OtpCodes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
