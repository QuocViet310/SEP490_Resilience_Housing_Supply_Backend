using Microsoft.EntityFrameworkCore;
using RHS.Domain.Entities;
using RHS.Infrastructure.Configurations;

namespace RHS.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<HousingProject> HousingProjects { get; set; }
    public DbSet<HousingProjectStatus> HousingProjectStatuses { get; set; }
    public DbSet<OtpVerification> OtpVerifications { get; set; }
    public DbSet<Payment> Payments { get; set; }

    // New DbSets
    public DbSet<HousingApplication> HousingApplications { get; set; }
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories { get; set; }
    public DbSet<ApplicationDocument> ApplicationDocuments { get; set; }
    public DbSet<AIVerificationResult> AIVerificationResults { get; set; }
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<ProjectImage> ProjectImages { get; set; }
    public DbSet<HousingQuota> HousingQuotas { get; set; }
    public DbSet<EligibilityAssessment> EligibilityAssessments { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<PolicyConfig> PolicyConfigs { get; set; }
    public DbSet<IssueReport> IssueReports { get; set; }
    public DbSet<Wishlist> Wishlists { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfiguration(new HousingProjectStatusConfiguration());
        modelBuilder.ApplyConfiguration(new HousingProjectConfiguration());
        modelBuilder.ApplyConfiguration(new HousingApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationStatusHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new AIVerificationResultConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectImageConfiguration());
        modelBuilder.ApplyConfiguration(new HousingQuotaConfiguration());
        modelBuilder.ApplyConfiguration(new EligibilityAssessmentConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new PolicyConfigConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new IssueReportConfiguration());
        modelBuilder.ApplyConfiguration(new WishlistConfiguration());
        // Role Configuration
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RoleName).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.RoleName).IsUnique();
        });

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.CitizenId);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(15);
            entity.Property(e => e.CitizenId).HasMaxLength(20);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProfileImageUrl).HasMaxLength(500);

            entity.HasOne(e => e.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // RefreshToken Configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // OtpVerification Configuration
        modelBuilder.Entity<OtpVerification>(entity =>
        {
            entity.ToTable("OtpVerifications");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.OtpCode });
            entity.Property(e => e.OtpCode).IsRequired().HasMaxLength(10);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.OtpVerifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed Roles
        SeedRoles(modelBuilder);
    }

    private void SeedRoles(ModelBuilder modelBuilder)
    {
        // Guest không cần role vì không cần đăng nhập
        // Chỉ seed 3 roles cho authenticated users
        var roles = new[]
        {
            new Role 
            { 
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), 
                RoleName = "Applicant"
            },
            new Role 
            { 
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), 
                RoleName = "Housing Authority Officer"
            },
            new Role 
            { 
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), 
                RoleName = "System Administrator"
            }
        };

        modelBuilder.Entity<Role>().HasData(roles);
    }
}
