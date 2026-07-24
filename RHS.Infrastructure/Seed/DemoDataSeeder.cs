using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Seed;

/// <summary>
/// Seed dữ liệu demo để thao tác luồng Applicant / CĐT / SXD.
/// Idempotent: chạy lại không nhân đôi (theo email + ProjectName cố định).
/// </summary>
public static class DemoDataSeeder
{
    public static readonly Guid DemoDeveloperUserId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    public static readonly Guid DemoSxdUserId = Guid.Parse("a2222222-2222-2222-2222-222222222222");

    public const string DemoDeveloperEmail = "cdt.demo@rhs.local";
    public const string DemoSxdEmail = "sxd.demo@rhs.local";
    public const string DemoPassword = "Demo@123456";

    /// <summary>Account trống — dùng test tạo hồ sơ / rào 1 tài khoản 1 hồ sơ.</summary>
    public const string DemoApplicantFreeEmail = "dan.free@rhs.local";

    /// <summary>Marker trong Description để nhận diện dự án seed.</summary>
    private const string SeedMarker = "[DEMO_SEED]";

    public static async Task EnsureSeededAsync(AppDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        await EnsureProjectStatusesAsync(db, logger, ct);
        var developer = await EnsureDemoStaffAsync(db, logger, ct);
        await EnsureDemoProjectsAsync(db, developer.Id, logger, ct);
        await EnsureDemoApplicantsAndApplicationsAsync(db, logger, ct);
    }

    private static async Task EnsureProjectStatusesAsync(AppDbContext db, ILogger? logger, CancellationToken ct)
    {
        var required = new (string Code, string Name)[]
        {
            ("PENDING", "Pending"),
            ("UPCOMING", "Upcoming"),
            ("OPEN", "Open"),
            ("CLOSED", "Closed"),
            ("FULL", "Full"),
            ("REJECTED", "Rejected"),
        };

        var existing = await db.HousingProjectStatuses
            .AsNoTracking()
            .Select(s => s.StatusCode)
            .ToListAsync(ct);

        var toAdd = required
            .Where(r => !existing.Contains(r.Code))
            .Select(r => new HousingProjectStatus
            {
                Id = Guid.NewGuid(),
                StatusCode = r.Code,
                StatusName = r.Name,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        if (toAdd.Count == 0) return;

        db.HousingProjectStatuses.AddRange(toAdd);
        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Demo seed: added {Count} HousingProjectStatus codes.", toAdd.Count);
    }

    private static async Task<User> EnsureDemoStaffAsync(AppDbContext db, ILogger? logger, CancellationToken ct)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);

        async Task<User> EnsureUser(Guid id, string email, Guid roleId, string fullName)
        {
            var roleExists = await db.Roles.AnyAsync(r => r.Id == roleId, ct);
            if (!roleExists)
            {
                var roleName = roleId == RoleConstants.HousingDeveloperId
                    ? RoleConstants.HousingDeveloper
                    : roleId == RoleConstants.DepartmentOfConstructionId
                        ? RoleConstants.DepartmentOfConstruction
                        : "Unknown";
                db.Roles.Add(new Role { Id = roleId, RoleName = roleName });
                await db.SaveChangesAsync(ct);
                logger?.LogInformation("Demo seed: ensured role {RoleName}", roleName);
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id || u.Email == email, ct);
            if (user != null)
            {
                if (user.Status != "Active")
                {
                    user.Status = "Active";
                    user.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
                return user;
            }

            user = new User
            {
                Id = id,
                Email = email,
                FullName = fullName,
                PasswordHash = passwordHash,
                RoleId = roleId,
                Status = "Active",
                IsEmailVerified = true,
                PhoneNumber = "0900000000",
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            logger?.LogInformation("Demo seed: created staff {Email} / {Password}", email, DemoPassword);
            return user;
        }

        var developer = await EnsureUser(
            DemoDeveloperUserId,
            DemoDeveloperEmail,
            RoleConstants.HousingDeveloperId,
            "CĐT Demo RHS");

        await EnsureUser(
            DemoSxdUserId,
            DemoSxdEmail,
            RoleConstants.DepartmentOfConstructionId,
            "SXD Demo RHS");

        return developer;
    }

    private static async Task EnsureDemoProjectsAsync(
        AppDbContext db,
        Guid developerId,
        ILogger? logger,
        CancellationToken ct)
    {
        var statuses = await db.HousingProjectStatuses.ToDictionaryAsync(s => s.StatusCode, ct);
        if (!statuses.ContainsKey("OPEN") || !statuses.ContainsKey("UPCOMING") || !statuses.ContainsKey("CLOSED"))
        {
            logger?.LogWarning("Demo seed: missing project statuses — skip projects.");
            return;
        }

        // Chỉ giữ demo trong TP.HCM — soft-delete seed cũ sai tỉnh / sai format Province
        var staleDemo = await db.HousingProjects
            .Where(p => !p.IsDeleted
                        && p.Description.Contains(SeedMarker)
                        && p.Province != "Thành phố Hồ Chí Minh")
            .ToListAsync(ct);

        var obsoleteNames = new[]
        {
            "NOXH Bình Minh — Quận 9",
            "NOXH An Phú — Hà Đông",
            "NOXH Hòa Xuân — Cẩm Lệ",
            "NOXH Cần Giuộc — Đã đóng",
            "NOXH An Phú — Quận 2", // sẽ tạo lại với District format chuẩn
        };
        var renamed = await db.HousingProjects
            .Where(p => !p.IsDeleted
                        && p.Description.Contains(SeedMarker)
                        && obsoleteNames.Contains(p.ProjectName))
            .ToListAsync(ct);

        foreach (var p in staleDemo.Concat(renamed).DistinctBy(x => x.Id))
        {
            p.IsDeleted = true;
            p.UpdatedAt = DateTime.UtcNow;
        }

        if (staleDemo.Count + renamed.Count > 0)
            await db.SaveChangesAsync(ct);

        var now = DateTime.UtcNow;
        var defs = BuildProjectDefs(now, statuses, developerId);

        // Đồng bộ Province/District/Ward/Street về format name_with_type (khớp mobile filter)
        var existingDemos = await db.HousingProjects
            .Where(p => p.Description.Contains(SeedMarker) && !p.IsDeleted)
            .ToListAsync(ct);
        var existingByName = existingDemos.ToDictionary(p => p.ProjectName, p => p);

        var updated = 0;
        var added = 0;
        foreach (var def in defs)
        {
            if (existingByName.TryGetValue(def.Project.ProjectName, out var existing))
            {
                var changed =
                    existing.Province != def.Project.Province
                    || existing.District != def.Project.District
                    || existing.Ward != def.Project.Ward
                    || existing.Street != def.Project.Street;

                if (changed)
                {
                    existing.Province = def.Project.Province;
                    existing.District = def.Project.District;
                    existing.Ward = def.Project.Ward;
                    existing.Street = def.Project.Street;
                    existing.LotteryLocation = def.Project.LotteryLocation;
                    existing.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
                continue;
            }

            db.HousingProjects.Add(def.Project);
            foreach (var q in def.Quotas)
                db.HousingQuotas.Add(q);
            foreach (var img in def.Images)
                db.ProjectImages.Add(img);
            added++;
        }

        // Bổ sung ảnh demo nếu dự án seed đã có nhưng chưa có ProjectImage
        var imageAdded = await EnsureDemoImagesAsync(db, defs, ct);

        if (added > 0 || updated > 0 || imageAdded > 0)
        {
            await db.SaveChangesAsync(ct);
            logger?.LogInformation(
                "Demo seed: added {Added} projects, updated address on {Updated}, images +{Images}.",
                added, updated, imageAdded);
        }
        else
        {
            logger?.LogInformation("Demo seed: housing projects already present — skip.");
        }
    }

    private static async Task<int> EnsureDemoImagesAsync(
        AppDbContext db,
        List<(HousingProject Project, List<HousingQuota> Quotas, List<ProjectImage> Images)> defs,
        CancellationToken ct)
    {
        var projectIds = defs.Select(d => d.Project.Id).ToList();
        var existingIds = await db.ProjectImages
            .AsNoTracking()
            .Where(i => projectIds.Contains(i.ProjectId))
            .Select(i => i.ProjectId)
            .Distinct()
            .ToListAsync(ct);

        var missing = defs.Where(d => !existingIds.Contains(d.Project.Id)).ToList();
        foreach (var def in missing)
        {
            foreach (var img in def.Images)
                db.ProjectImages.Add(img);
        }

        return missing.Sum(d => d.Images.Count);
    }

    private static List<(HousingProject Project, List<HousingQuota> Quotas, List<ProjectImage> Images)> BuildProjectDefs(
        DateTime now,
        IReadOnlyDictionary<string, HousingProjectStatus> statuses,
        Guid developerId)
    {
        var openId = statuses["OPEN"].Id;
        var upcomingId = statuses["UPCOMING"].Id;
        var closedId = statuses["CLOSED"].Id;

        // Ảnh demo ổn định (Unsplash) — chỉ để test UI mobile
        string[] DemoThumbs =
        [
            "https://images.unsplash.com/photo-1545324418-cc1a3fa10c00?w=800&q=80",
            "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?w=800&q=80",
            "https://images.unsplash.com/photo-1460317442991-0ec209397118?w=800&q=80",
            "https://images.unsplash.com/photo-1512917774080-9991f1c4c750?w=800&q=80",
            "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?w=800&q=80",
            "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=800&q=80",
            "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=800&q=80",
            "https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=800&q=80",
        ];

        HousingProject Make(
            Guid id,
            string name,
            string province,
            string district,
            string ward,
            string street,
            Guid statusId,
            int units,
            decimal deposit,
            decimal minPrice,
            decimal maxPrice,
            int minArea,
            int maxArea,
            DateTime? open,
            DateTime? close,
            DateTime? announce,
            string descExtra) => new()
        {
            Id = id,
            ProjectName = name,
            Description = $"{SeedMarker} {descExtra}",
            Province = province,
            District = district,
            Ward = ward,
            Street = street,
            LotteryDate = now.AddDays(45),
            LotteryLocation = $"Hội trường UBND {district}, {province}",
            DepositAmount = deposit,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            MinArea = minArea,
            MaxArea = maxArea,
            AvailableUnits = units,
            HousingProjectStatusId = statusId,
            IsDeleted = false,
            IsConfirmed = true,
            DecisionNumber = $"QĐ-DEMO-{id.ToString()[..8].ToUpperInvariant()}",
            ApprovalDate = now.AddDays(-60),
            ApplicationOpenDate = open,
            ApplicationCloseDate = close,
            PublicAnnounceAt = announce,
            DeveloperId = developerId,
            CreatedAt = now.AddDays(-60),
            UpdatedAt = now
        };

        List<HousingQuota> Quotas(Guid projectId, int units)
        {
            // Phân bổ suất theo nhóm phổ biến (demo); còn lại vào bốc thăm chung nếu không khớp quota
            var poor = Math.Max(1, units / 4);
            var nearPoor = Math.Max(1, units / 4);
            var lowIncome = Math.Max(1, units / 5);
            var worker = Math.Max(1, units / 5);
            var rest = Math.Max(1, units - poor - nearPoor - lowIncome - worker);

            return
            [
                new HousingQuota
                {
                    QuotaId = Guid.NewGuid(),
                    ProjectId = projectId,
                    PriorityGroup = PriorityGroupConstants.UrbanPoor,
                    AllocatedSlots = poor,
                    RemainingSlots = poor
                },
                new HousingQuota
                {
                    QuotaId = Guid.NewGuid(),
                    ProjectId = projectId,
                    PriorityGroup = PriorityGroupConstants.UrbanNearPoor,
                    AllocatedSlots = nearPoor,
                    RemainingSlots = nearPoor
                },
                new HousingQuota
                {
                    QuotaId = Guid.NewGuid(),
                    ProjectId = projectId,
                    PriorityGroup = PriorityGroupConstants.LowIncomeUrban,
                    AllocatedSlots = lowIncome,
                    RemainingSlots = lowIncome
                },
                new HousingQuota
                {
                    QuotaId = Guid.NewGuid(),
                    ProjectId = projectId,
                    PriorityGroup = PriorityGroupConstants.Worker,
                    AllocatedSlots = worker,
                    RemainingSlots = worker
                },
                new HousingQuota
                {
                    QuotaId = Guid.NewGuid(),
                    ProjectId = projectId,
                    PriorityGroup = PriorityGroupConstants.CivilServant,
                    AllocatedSlots = rest,
                    RemainingSlots = rest
                },
            ];
        }

        List<ProjectImage> Images(Guid projectId, params int[] thumbIndexes) =>
            thumbIndexes.Select((idx, order) => new ProjectImage
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ImageUrl = DemoThumbs[idx % DemoThumbs.Length],
                DisplayOrder = order,
                CreatedAt = now
            }).ToList();

        var p1 = Guid.Parse("b1000001-0001-0001-0001-000000000001");
        var p2 = Guid.Parse("b1000001-0001-0001-0001-000000000002");
        var p3 = Guid.Parse("b1000001-0001-0001-0001-000000000003");
        var p4 = Guid.Parse("b1000001-0001-0001-0001-000000000004");
        var p5 = Guid.Parse("b1000001-0001-0001-0001-000000000005");
        var p6 = Guid.Parse("b1000001-0001-0001-0001-000000000006");
        var p7 = Guid.Parse("b1000001-0001-0001-0001-000000000007");
        var p8 = Guid.Parse("b1000001-0001-0001-0001-000000000008");

        // Format địa chỉ khớp mobile assets: Province/District = name_with_type
        return
        [
            (
                Make(p1, "NOXH Bình Minh — Thủ Đức", "Thành phố Hồ Chí Minh", "Thành phố Thủ Đức", "Phường Long Thạnh Mỹ",
                    "12 Đại lộ Mai Chí Thọ", openId, 80, 5_000_000m, 480_000_000m, 780_000_000m, 28, 52,
                    now.AddDays(-10), now.AddDays(60), now.AddDays(-40),
                    "Dự án OPEN demo Thủ Đức — đang nhận hồ sơ."),
                Quotas(p1, 80),
                Images(p1, 0, 1)
            ),
            (
                Make(p2, "NOXH An Phú — Thủ Đức", "Thành phố Hồ Chí Minh", "Thành phố Thủ Đức", "Phường An Phú",
                    "88 Đường Song Hành", openId, 120, 3_000_000m, 520_000_000m, 920_000_000m, 30, 60,
                    now.AddDays(-5), now.AddDays(90), now.AddDays(-35),
                    "Dự án OPEN demo khu An Phú (TP.HCM)."),
                Quotas(p2, 120),
                Images(p2, 2, 3)
            ),
            (
                Make(p3, "NOXH Bình Tân — An Lạc", "Thành phố Hồ Chí Minh", "Quận Bình Tân", "Phường An Lạc A",
                    "45 Đường Kinh Dương Vương", openId, 50, 2_500_000m, 390_000_000m, 650_000_000m, 25, 48,
                    now.AddDays(-3), now.AddDays(45), now.AddDays(-33),
                    "Dự án OPEN demo Bình Tân (TP.HCM)."),
                Quotas(p3, 50),
                Images(p3, 4, 5)
            ),
            (
                Make(p4, "NOXH Phước Long B — Thủ Đức", "Thành phố Hồ Chí Minh", "Thành phố Thủ Đức", "Phường Phước Long B",
                    "210 Đường Đỗ Xuân Hợp", openId, 30, 5_000_000m, 450_000_000m, 700_000_000m, 26, 45,
                    now.AddDays(-1), now.AddDays(30), now.AddDays(-31),
                    "Dự án OPEN số suất ít — test oversubscribe."),
                Quotas(p4, 30),
                Images(p4, 6)
            ),
            (
                Make(p7, "NOXH Nhà Ở Xã Hội — Quận 7", "Thành phố Hồ Chí Minh", "Quận 7", "Phường Tân Thuận",
                    "120 Nguyễn Văn Linh", openId, 90, 4_000_000m, 550_000_000m, 980_000_000m, 32, 58,
                    now.AddDays(-7), now.AddDays(75), now.AddDays(-38),
                    "Dự án OPEN Quận 7 — test filter quận + sort giá."),
                Quotas(p7, 90),
                Images(p7, 1, 7)
            ),
            (
                Make(p8, "NOXH Nhà Ở Xã Hội — Quận 12", "Thành phố Hồ Chí Minh", "Quận 12", "Phường Trung Mỹ Tây",
                    "55 Quốc lộ 1A", openId, 70, 2_000_000m, 320_000_000m, 580_000_000m, 24, 42,
                    now.AddDays(-2), now.AddDays(50), now.AddDays(-32),
                    "Dự án OPEN Quận 12 — giá thấp hơn để test sort."),
                Quotas(p8, 70),
                Images(p8, 3, 5)
            ),
            (
                Make(p5, "NOXH Tân Phú — Sắp mở", "Thành phố Hồ Chí Minh", "Quận Tân Phú", "Phường Tân Sơn Nhì",
                    "15 Đường Lũy Bán Bích", upcomingId, 60, 4_000_000m, 450_000_000m, 850_000_000m, 25, 55,
                    now.AddDays(7), now.AddDays(70), now.AddDays(-5),
                    "Dự án UPCOMING — chưa mở đăng ký (mobile sẽ ẩn)."),
                Quotas(p5, 60),
                Images(p5, 0)
            ),
            (
                Make(p6, "NOXH Nhà Bè — Đã đóng", "Thành phố Hồ Chí Minh", "Huyện Nhà Bè", "Xã Phước Kiển",
                    "01 Đường Nguyễn Văn Tạo", closedId, 0, 2_000_000m, 400_000_000m, 700_000_000m, 25, 50,
                    now.AddDays(-90), now.AddDays(-10), now.AddDays(-120),
                    "Dự án CLOSED — hết hạn nhận hồ sơ."),
                Quotas(p6, 40),
                Images(p6, 2)
            ),
        ];
    }

    /// <summary>
    /// Seed người dân + hồ sơ nhiều trạng thái (1 account = 1 hồ sơ) để test end-to-end.
    /// Idempotent theo User.Id / ApplicationId cố định.
    /// </summary>
    private static async Task EnsureDemoApplicantsAndApplicationsAsync(
        AppDbContext db,
        ILogger? logger,
        CancellationToken ct)
    {
        var projectId = Guid.Parse("b1000001-0001-0001-0001-000000000001"); // NOXH Bình Minh — Thủ Đức
        var projectExists = await db.HousingProjects.AnyAsync(p => p.Id == projectId && !p.IsDeleted, ct);
        if (!projectExists)
        {
            logger?.LogWarning("Demo seed: project Bình Minh missing — skip applicants/applications.");
            return;
        }

        var roleExists = await db.Roles.AnyAsync(r => r.Id == RoleConstants.ApplicantId, ct);
        if (!roleExists)
        {
            db.Roles.Add(new Role { Id = RoleConstants.ApplicantId, RoleName = RoleConstants.Applicant });
            await db.SaveChangesAsync(ct);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);
        var now = DateTime.UtcNow;
        var defs = BuildApplicantApplicationDefs(projectId, now);

        var userAdded = 0;
        var appAdded = 0;
        var agreementAdded = 0;

        foreach (var def in defs)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == def.UserId || u.Email == def.Email, ct);
            if (user == null)
            {
                user = new User
                {
                    Id = def.UserId,
                    Email = def.Email,
                    FullName = def.FullName,
                    PasswordHash = passwordHash,
                    RoleId = RoleConstants.ApplicantId,
                    Status = "Active",
                    IsEmailVerified = true,
                    PhoneNumber = def.Phone,
                    CitizenId = def.CitizenId,
                    CreatedAt = now.AddDays(-30)
                };
                db.Users.Add(user);
                userAdded++;
            }
            else
            {
                // Đồng bộ CCCD / active nếu seed cũ thiếu
                if (string.IsNullOrWhiteSpace(user.CitizenId))
                    user.CitizenId = def.CitizenId;
                if (user.Status != "Active")
                    user.Status = "Active";
                user.UpdatedAt = DateTime.UtcNow;
            }

            if (def.SkipApplication)
                continue;

            var app = await db.HousingApplications
                .FirstOrDefaultAsync(a => a.ApplicationId == def.ApplicationId, ct);
            if (app != null)
                continue;

            // Tránh trùng ApplicantId+ProjectId nếu đã có hồ sơ khác
            var existsPair = await db.HousingApplications.AnyAsync(
                a => a.ApplicantId == def.UserId
                     && a.ProjectId == projectId
                     && a.ApplicationStatus != ApplicationStatusConstants.Rejected
                     && a.ApplicationStatus != ApplicationStatusConstants.Canceled, ct);
            if (existsPair)
                continue;

            app = new HousingApplication
            {
                ApplicationId = def.ApplicationId,
                ApplicantId = def.UserId,
                ProjectId = projectId,
                ApplicationStatus = def.Status,
                SubmittedAt = now.AddDays(def.SubmittedDaysAgo),
                CreatedAt = now.AddDays(def.SubmittedDaysAgo - 1),
                UpdatedAt = now,
                FullName = def.FullName,
                CitizenId = def.CitizenId,
                Occupation = def.Occupation,
                WorkPlace = "Công ty TNHH Demo RHS",
                CurrentResidence = "12 Nguyễn Văn Linh, Quận 7, TP.HCM",
                PermanentAddress = "12 Nguyễn Văn Linh, Quận 7, TP.HCM",
                HousingStatus = HousingStatusConstants.NoHouse,
                MaritalStatus = "SINGLE",
                HouseholdMembersCount = 3,
                PriorityGroup = def.PriorityGroup,
                PriorityScore = def.PriorityScore,
                MonthlyIncome = def.MonthlyIncome,
                LotteryResult = def.LotteryResult,
                SlotCode = def.SlotCode,
                IsViolation = false
            };
            db.HousingApplications.Add(app);
            appAdded++;

            db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = def.ApplicationId,
                ChangedBy = def.UserId,
                Action = ReviewActionConstants.Submit,
                OldStatus = ApplicationStatusConstants.Draft,
                NewStatus = def.Status,
                Note = $"[DEMO_SEED] Hồ sơ demo trạng thái {def.Status}",
                ChangedAt = now.AddDays(def.SubmittedDaysAgo)
            });

            if (def.NeedsAgreement)
            {
                var hasAgreement = await db.PrincipleAgreements
                    .AnyAsync(a => a.ApplicationId == def.ApplicationId, ct);
                if (!hasAgreement)
                {
                    db.PrincipleAgreements.Add(new PrincipleAgreement
                    {
                        Id = Guid.NewGuid(),
                        ApplicationId = def.ApplicationId,
                        PdfUrl = $"/api/payment/download-contract/{def.ApplicationId}",
                        CreatedAt = now.AddDays(-2),
                        IsSigned = def.AgreementSigned,
                        SignedAt = def.AgreementSigned ? now.AddDays(-1) : null,
                        SignedIpAddress = def.AgreementSigned ? "127.0.0.1" : null
                    });
                    agreementAdded++;
                }
            }
        }

        if (userAdded > 0 || appAdded > 0 || agreementAdded > 0)
        {
            await db.SaveChangesAsync(ct);
            logger?.LogInformation(
                "Demo seed: applicants +{Users}, applications +{Apps}, agreements +{Agreements}. Password={Password}",
                userAdded, appAdded, agreementAdded, DemoPassword);
        }
        else
        {
            logger?.LogInformation("Demo seed: applicants/applications already present — skip.");
        }
    }

    private static List<DemoApplicantDef> BuildApplicantApplicationDefs(Guid projectId, DateTime now)
    {
        // Mỗi account một hồ sơ (khớp rào 1 TK = 1 hồ sơ active) — trừ dan.free (chưa có hồ sơ).
        return
        [
            new("c1000001-0001-0001-0001-000000000001", "d1000001-0001-0001-0001-000000000001",
                "dan.draft@rhs.local", "Nguyễn Văn Draft", "001090000001", "0901000001",
                ApplicationStatusConstants.Draft, PriorityGroupConstants.UrbanPoor, 10, 8_000_000m,
                null, null, false, false, -20, "Công nhân"),

            new("c1000001-0001-0001-0001-000000000002", "d1000001-0001-0001-0001-000000000002",
                "dan.submitted@rhs.local", "Trần Thị Submitted", "001090000002", "0901000002",
                ApplicationStatusConstants.Submitted, PriorityGroupConstants.UrbanNearPoor, 20, 9_000_000m,
                null, null, false, false, -18, "Nhân viên"),

            new("c1000001-0001-0001-0001-000000000003", "d1000001-0001-0001-0001-000000000003",
                "dan.reviewing@rhs.local", "Lê Văn Reviewing", "001090000003", "0901000003",
                ApplicationStatusConstants.Reviewing, PriorityGroupConstants.LowIncomeUrban, 30, 10_000_000m,
                null, null, false, false, -16, "Kỹ thuật viên"),

            new("c1000001-0001-0001-0001-000000000004", "d1000001-0001-0001-0001-000000000004",
                "dan.needdoc@rhs.local", "Phạm Thị NeedDoc", "001090000004", "0901000004",
                ApplicationStatusConstants.NeedMoreDocuments, PriorityGroupConstants.Worker, 25, 11_000_000m,
                null, null, false, false, -15, "Công nhân"),

            new("c1000001-0001-0001-0001-000000000005", "d1000001-0001-0001-0001-000000000005",
                "dan.pendingsxd@rhs.local", "Hoàng Văn PendingSxd", "001090000005", "0901000005",
                ApplicationStatusConstants.PendingSxdReview, PriorityGroupConstants.UrbanPoor, 40, 7_500_000m,
                null, null, false, false, -14, "Lao động tự do"),

            new("c1000001-0001-0001-0001-000000000006", "d1000001-0001-0001-0001-000000000006",
                "dan.approved@rhs.local", "Võ Thị Approved", "001090000006", "0901000006",
                ApplicationStatusConstants.Approved, PriorityGroupConstants.UrbanPoor, 50, 8_500_000m,
                null, null, false, false, -12, "Công nhân"),

            new("c1000001-0001-0001-0001-000000000007", "d1000001-0001-0001-0001-000000000007",
                "dan.timeout@rhs.local", "Đặng Văn Timeout", "001090000007", "0901000007",
                ApplicationStatusConstants.ApprovedByTimeout, PriorityGroupConstants.UrbanNearPoor, 45, 9_500_000m,
                null, null, false, false, -25, "Nhân viên"),

            new("c1000001-0001-0001-0001-000000000008", "d1000001-0001-0001-0001-000000000008",
                "dan.contract@rhs.local", "Bùi Thị ContractPending", "001090000008", "0901000008",
                ApplicationStatusConstants.ContractPending, PriorityGroupConstants.UrbanPoor, 60, 8_000_000m,
                LotteryResultConstants.Won, null, true, false, -10, "Công nhân"),

            new("c1000001-0001-0001-0001-000000000009", "d1000001-0001-0001-0001-000000000009",
                "dan.signed@rhs.local", "Ngô Văn ContractSigned", "001090000009", "0901000009",
                ApplicationStatusConstants.ContractSigned, PriorityGroupConstants.Worker, 55, 10_000_000m,
                LotteryResultConstants.PriorityWon, null, true, true, -9, "Công nhân"),

            new("c1000001-0001-0001-0001-00000000000a", "d1000001-0001-0001-0001-00000000000a",
                "dan.deposit@rhs.local", "Đỗ Thị DepositPaid", "001090000010", "0901000010",
                ApplicationStatusConstants.DepositPaid, PriorityGroupConstants.UrbanPoor, 70, 8_200_000m,
                LotteryResultConstants.Won, "NOXH-TD-001", true, true, -8, "Công nhân"),

            new("c1000001-0001-0001-0001-00000000000b", "d1000001-0001-0001-0001-00000000000b",
                "dan.priority@rhs.local", "Lý Văn PriorityApproved", "001090000011", "0901000011",
                ApplicationStatusConstants.Approved, PriorityGroupConstants.MeritPerson, 90, 7_000_000m,
                null, null, false, false, -11, "Người có công"),

            new("c1000001-0001-0001-0001-00000000000c", "d1000001-0001-0001-0001-00000000000c",
                "dan.lost@rhs.local", "Mai Thị LotteryLost", "001090000012", "0901000012",
                ApplicationStatusConstants.LotteryLost, PriorityGroupConstants.LowIncomeUrban, 15, 12_000_000m,
                LotteryResultConstants.Lost, null, false, false, -7, "Nhân viên"),

            new("c1000001-0001-0001-0001-00000000000d", "d1000001-0001-0001-0001-00000000000d",
                "dan.rejected@rhs.local", "Phan Văn Rejected", "001090000013", "0901000013",
                ApplicationStatusConstants.Rejected, PriorityGroupConstants.UrbanNearPoor, 5, 15_000_000m,
                null, null, false, false, -6, "Buôn bán"),

            new("c1000001-0001-0001-0001-00000000000e", "d1000001-0001-0001-0001-00000000000e",
                "dan.expired@rhs.local", "Trương Thị Expired", "001090000014", "0901000014",
                ApplicationStatusConstants.Expired, PriorityGroupConstants.Worker, 35, 9_000_000m,
                null, null, false, false, -30, "Công nhân"),

            new("c1000001-0001-0001-0001-00000000000f", "d1000001-0001-0001-0001-00000000000f",
                "dan.fullypaid@rhs.local", "Huỳnh Văn FullyPaid", "001090000015", "0901000015",
                ApplicationStatusConstants.FullyPaid, PriorityGroupConstants.UrbanPoor, 80, 8_000_000m,
                LotteryResultConstants.Won, "NOXH-TD-002", true, true, -5, "Công nhân"),

            // Account trống — test tạo hồ sơ mới + kiểm tra rào 1 TK 1 hồ sơ
            new("c1000001-0001-0001-0001-000000000010", "00000000-0000-0000-0000-000000000000",
                DemoApplicantFreeEmail, "Nguyễn Thị Free", "001090000016", "0901000016",
                "", PriorityGroupConstants.UrbanPoor, 0, 8_000_000m,
                null, null, false, false, 0, "Công nhân", SkipApplication: true),
        ];
    }

    private sealed record DemoApplicantDef(
        string UserIdRaw,
        string ApplicationIdRaw,
        string Email,
        string FullName,
        string CitizenId,
        string Phone,
        string Status,
        string? PriorityGroup,
        decimal PriorityScore,
        decimal MonthlyIncome,
        string? LotteryResult,
        string? SlotCode,
        bool NeedsAgreement,
        bool AgreementSigned,
        int SubmittedDaysAgo,
        string Occupation,
        bool SkipApplication = false)
    {
        public Guid UserId => Guid.Parse(UserIdRaw);
        public Guid ApplicationId => Guid.Parse(ApplicationIdRaw);
    }
}
