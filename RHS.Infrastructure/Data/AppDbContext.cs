using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RHS.Domain.Entities;
using RHS.Infrastructure.Configurations;
using System.Security.Claims;
using System.Text.Json;

namespace RHS.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IHttpContextAccessor httpContextAccessor) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
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
    public DbSet<PrincipleAgreement> PrincipleAgreements { get; set; }
    public DbSet<LotteryDraw> LotteryDraws { get; set; }
    public DbSet<ApartmentType> ApartmentTypes { get; set; }
    public DbSet<PaymentMilestone> PaymentMilestones { get; set; }
    public DbSet<PaymentInstallment> PaymentInstallments { get; set; }
    public DbSet<HouseholdMember> HouseholdMembers { get; set; }

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
        modelBuilder.ApplyConfiguration(new PrincipleAgreementConfiguration());
        modelBuilder.ApplyConfiguration(new LotteryDrawConfiguration());
        modelBuilder.ApplyConfiguration(new ApartmentTypeConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentMilestoneConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentInstallmentConfiguration());
        modelBuilder.ApplyConfiguration(new HouseholdMemberConfiguration());
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

    // ═══════════════════════════════════════════════════════════════════════
    // Automatic Audit Logging
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Entities to exclude from audit logging to avoid infinite loops and noise.
    /// </summary>
    private static readonly HashSet<string> ExcludedEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(AuditLog),
        nameof(RefreshToken),
        nameof(OtpVerification)
    };

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditEntries = OnBeforeSaveChanges();
        var result = await base.SaveChangesAsync(cancellationToken);

        // Sau khi save, các entity Added sẽ có Id được DB generate
        if (auditEntries.Count > 0)
        {
            await OnAfterSaveChangesAsync(auditEntries, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Collect audit entries BEFORE SaveChanges — entity states chưa bị reset.
    /// </summary>
    private List<AuditEntry> OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        var entries = new List<AuditEntry>();

        foreach (var entry in ChangeTracker.Entries())
        {
            // Skip excluded entities
            if (ExcludedEntities.Contains(entry.Entity.GetType().Name))
                continue;

            // Only track Added, Modified, Deleted
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntry
            {
                EntityName = entry.Entity.GetType().Name,
                Action = entry.State switch
                {
                    EntityState.Added    => "INSERT",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted  => "DELETE",
                    _ => entry.State.ToString()
                }
            };

            // Resolve EntityId from primary key
            var primaryKey = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            if (primaryKey?.CurrentValue is Guid guidId)
            {
                auditEntry.EntityId = guidId;
            }

            // Collect old/new values
            foreach (var property in entry.Properties)
            {
                var propName = property.Metadata.Name;

                // Skip navigation properties
                if (property.Metadata.IsPrimaryKey())
                    continue;

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.NewValues[propName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        auditEntry.OldValues[propName] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.OldValues[propName] = property.OriginalValue;
                            auditEntry.NewValues[propName] = property.CurrentValue;
                        }
                        break;
                }
            }

            // For Added entities, the PK might not be set yet — flag for post-save
            if (entry.State == EntityState.Added)
            {
                auditEntry.Entry = entry;
            }

            entries.Add(auditEntry);
        }

        return entries;
    }

    /// <summary>
    /// Write audit log entries AFTER SaveChanges — Added entities now have DB-generated IDs.
    /// </summary>
    private async Task OnAfterSaveChangesAsync(
        List<AuditEntry> auditEntries, CancellationToken cancellationToken)
    {
        // Resolve user info from HttpContext
        Guid? userId = null;
        var ipAddress = "Unknown";

        if (_httpContextAccessor?.HttpContext != null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? httpContext.User.FindFirst("sub");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        var jsonOptions = new JsonSerializerOptions { WriteIndented = false };

        foreach (var auditEntry in auditEntries)
        {
            // For Added entries, resolve the PK now
            if (auditEntry.Entry != null)
            {
                var pk = auditEntry.Entry.Properties
                    .FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                if (pk?.CurrentValue is Guid guidId)
                {
                    auditEntry.EntityId = guidId;
                }
            }

            var auditLog = new AuditLog
            {
                AuditId    = Guid.NewGuid(),
                UserId     = userId,
                Action     = auditEntry.Action,
                EntityName = auditEntry.EntityName,
                EntityId   = auditEntry.EntityId,
                OldValues  = auditEntry.OldValues.Count > 0
                    ? JsonSerializer.Serialize(auditEntry.OldValues, jsonOptions) : null,
                NewValues  = auditEntry.NewValues.Count > 0
                    ? JsonSerializer.Serialize(auditEntry.NewValues, jsonOptions) : null,
                IpAddress  = ipAddress,
                ActionTime = DateTime.UtcNow
            };

            AuditLogs.Add(auditLog);
        }

        // Save audit logs — call base to avoid recursion
        await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Helper class to hold audit entry data during processing.</summary>
    private class AuditEntry
    {
        public string EntityName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public Dictionary<string, object?> OldValues { get; set; } = new();
        public Dictionary<string, object?> NewValues { get; set; } = new();

        /// <summary>Reference to EntityEntry for Added entities (to resolve PK after save).</summary>
        public Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry? Entry { get; set; }
    }
}
