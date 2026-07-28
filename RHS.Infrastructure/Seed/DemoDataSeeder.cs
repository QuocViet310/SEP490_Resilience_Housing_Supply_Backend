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
    public const string DemoPassword = "123456";

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
                var changed = false;
                if (user.Status != "Active")
                {
                    user.Status = "Active";
                    changed = true;
                }
                if (user.DateOfBirth == null)
                {
                    user.DateOfBirth = new DateTime(1985, 6, 15, 0, 0, 0, DateTimeKind.Utc);
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(user.Address))
                {
                    user.Address = "Số 1 Đại Cồ Việt, Hai Bà Trưng, Hà Nội";
                    changed = true;
                }
                if (user.PasswordHash == null ||
                    !BCrypt.Net.BCrypt.Verify(DemoPassword, user.PasswordHash))
                {
                    user.PasswordHash = passwordHash;
                    changed = true;
                }
                if (changed)
                {
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
                DateOfBirth = new DateTime(1985, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                Address = "Số 1 Đại Cồ Việt, Hai Bà Trưng, Hà Nội",
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

        // Đồng bộ theo Id cố định → Province/District/Ward = tên phường API v2
        var demoIds = defs.Select(d => d.Project.Id).ToList();
        var existingDemos = await db.HousingProjects
            .Where(p => demoIds.Contains(p.Id) && !p.IsDeleted)
            .ToListAsync(ct);
        var existingById = existingDemos.ToDictionary(p => p.Id, p => p);

        var updated = 0;
        var added = 0;
        foreach (var def in defs)
        {
            if (existingById.TryGetValue(def.Project.Id, out var existing))
            {
                var changed =
                    existing.ProjectName != def.Project.ProjectName
                    || existing.Province != def.Project.Province
                    || existing.District != def.Project.District
                    || existing.Ward != def.Project.Ward
                    || existing.Street != def.Project.Street
                    || existing.LotteryDate != null
                    || existing.LotteryLocation != null
                    || existing.LotteryType != null
                    || existing.LotteryDescription != null
                    || existing.IsLotteryApproved != null
                    || existing.LotteryJoinCode != null
                    || existing.Description != def.Project.Description;

                if (changed)
                {
                    existing.ProjectName = def.Project.ProjectName;
                    existing.Province = def.Project.Province;
                    existing.District = def.Project.District;
                    existing.Ward = def.Project.Ward;
                    existing.Street = def.Project.Street;
                    // Không seed lịch — CĐT đề xuất sau khi chốt hồ sơ / vượt số căn.
                    existing.LotteryDate = null;
                    existing.LotteryLocation = null;
                    existing.LotteryType = null;
                    existing.LotteryDescription = null;
                    existing.IsLotteryApproved = null;
                    existing.LotteryApprovedAt = null;
                    existing.LotteryApprovedBy = null;
                    existing.LotteryJoinCode = null;
                    existing.LotterySessionStatus = null;
                    existing.Description = def.Project.Description;
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
        var aptAdded = await EnsureDemoApartmentsAsync(db, defs, logger, ct);

        if (added > 0 || updated > 0 || imageAdded > 0 || aptAdded > 0)
        {
            await db.SaveChangesAsync(ct);
            logger?.LogInformation(
                "Demo seed: added {Added} projects, updated address on {Updated}, images +{Images}, apartments +{Apts}.",
                added, updated, imageAdded, aptAdded);
        }
        else
        {
            logger?.LogInformation("Demo seed: housing projects already present — skip.");
        }
    }

    /// <summary>
    /// Seed 4–5 căn cụ thể / dự án OPEN (tên + diện tích + giá + AVAILABLE).
    /// </summary>
    private static async Task<int> EnsureDemoApartmentsAsync(
        AppDbContext db,
        List<(HousingProject Project, List<HousingQuota> Quotas, List<ProjectImage> Images)> defs,
        ILogger? logger,
        CancellationToken ct)
    {
        var openProjects = defs
            .Select(d => d.Project)
            .Where(p => p.AvailableUnits > 0)
            .ToList();

        var templates = new (string UnitName, double Area, decimal Price)[]
        {
            ("A-101", 38.5, 720_000_000m),
            ("A-205", 45.2, 860_000_000m),
            ("B-312", 52.0, 980_000_000m),
            ("B-408", 58.7, 1_120_000_000m),
            ("C-501", 66.3, 1_280_000_000m),
        };

        var added = 0;
        var now = DateTime.UtcNow;

        foreach (var project in openProjects)
        {
            var count = await db.Apartments.CountAsync(a => a.ProjectId == project.Id, ct);
            if (count > 0)
                continue;

            // 4 hoặc 5 căn tùy AvailableUnits (clamp 4–5)
            var n = Math.Clamp(project.AvailableUnits, 4, 5);
            if (project.AvailableUnits != n)
            {
                var tracked = await db.HousingProjects.FirstOrDefaultAsync(p => p.Id == project.Id, ct);
                if (tracked != null)
                {
                    tracked.AvailableUnits = n;
                    tracked.MinArea = templates.Take(n).Min(t => t.Area);
                    tracked.MaxArea = templates.Take(n).Max(t => t.Area);
                    tracked.MinPrice = templates.Take(n).Min(t => t.Price);
                    tracked.MaxPrice = templates.Take(n).Max(t => t.Price);
                }
            }

            for (var i = 0; i < n; i++)
            {
                var t = templates[i];
                db.Apartments.Add(new Apartment
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    UnitName = t.UnitName,
                    Area = t.Area,
                    Price = t.Price,
                    Status = ApartmentStatusConstants.Available,
                    Description = $"{SeedMarker} Căn demo {t.UnitName}",
                    CreatedAt = now
                });
                added++;
            }
        }

        if (added > 0)
            logger?.LogInformation("Demo seed: apartments +{Count}.", added);

        return added;
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
            // Không init lịch — sau khi chốt hồ sơ, CĐT đề xuất ONLINE rồi Sở duyệt mới thông báo.
            LotteryDate = null,
            LotteryLocation = null,
            LotteryType = null,
            LotteryDescription = null,
            IsLotteryApproved = null,
            LotteryApprovedAt = null,
            LotteryApprovedBy = null,
            LotteryJoinCode = null,
            LotterySessionStatus = null,
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

        // Địa giới API v2 (Tỉnh → Phường/Xã): District = Ward = tên phường/xã chuẩn từ provinces.open-api.vn
        return
        [
            (
                Make(p1, "NOXH Bình Minh — Thủ Đức", "Thành phố Hồ Chí Minh", "Phường Thủ Đức", "Phường Thủ Đức",
                    "12 Đại lộ Mai Chí Thọ", openId, 5, 5_000_000m, 720_000_000m, 1_280_000_000m, 38, 67,
                    now.AddDays(-10), now.AddDays(60), now.AddDays(-40),
                    "Dự án OPEN demo Thủ Đức — đang nhận hồ sơ."),
                Quotas(p1, 5),
                Images(p1, 0, 1)
            ),
            (
                Make(p2, "NOXH An Phú — Thủ Đức", "Thành phố Hồ Chí Minh", "Phường An Phú", "Phường An Phú",
                    "88 Đường Song Hành", openId, 5, 3_000_000m, 720_000_000m, 1_280_000_000m, 38, 67,
                    now.AddDays(-5), now.AddDays(90), now.AddDays(-35),
                    "Dự án OPEN demo khu An Phú (TP.HCM)."),
                Quotas(p2, 5),
                Images(p2, 2, 3)
            ),
            (
                Make(p3, "NOXH Bình Tân — An Lạc", "Thành phố Hồ Chí Minh", "Phường An Lạc", "Phường An Lạc",
                    "45 Đường Kinh Dương Vương", openId, 4, 2_500_000m, 720_000_000m, 1_120_000_000m, 38, 59,
                    now.AddDays(-3), now.AddDays(45), now.AddDays(-33),
                    "Dự án OPEN demo Bình Tân (TP.HCM)."),
                Quotas(p3, 4),
                Images(p3, 4, 5)
            ),
            (
                Make(p4, "NOXH Phước Long — Thủ Đức", "Thành phố Hồ Chí Minh", "Phường Phước Long", "Phường Phước Long",
                    "210 Đường Đỗ Xuân Hợp", openId, 4, 5_000_000m, 720_000_000m, 1_120_000_000m, 38, 59,
                    now.AddDays(-1), now.AddDays(30), now.AddDays(-31),
                    "Dự án OPEN số suất ít — test oversubscribe."),
                Quotas(p4, 4),
                Images(p4, 6)
            ),
            (
                Make(p7, "NOXH Nhà Ở Xã Hội — Tân Thuận", "Thành phố Hồ Chí Minh", "Phường Tân Thuận", "Phường Tân Thuận",
                    "120 Nguyễn Văn Linh", openId, 5, 4_000_000m, 720_000_000m, 1_280_000_000m, 38, 67,
                    now.AddDays(-7), now.AddDays(75), now.AddDays(-38),
                    "Dự án OPEN Tân Thuận — test filter phường + sort giá."),
                Quotas(p7, 5),
                Images(p7, 1, 7)
            ),
            (
                Make(p8, "NOXH Nhà Ở Xã Hội — Trung Mỹ Tây", "Thành phố Hồ Chí Minh", "Phường Trung Mỹ Tây", "Phường Trung Mỹ Tây",
                    "55 Quốc lộ 1A", openId, 4, 2_000_000m, 720_000_000m, 1_120_000_000m, 38, 59,
                    now.AddDays(-2), now.AddDays(50), now.AddDays(-32),
                    "Dự án OPEN Trung Mỹ Tây — giá thấp hơn để test sort."),
                Quotas(p8, 4),
                Images(p8, 3, 5)
            ),
            (
                Make(p5, "NOXH Tân Phú — Sắp mở", "Thành phố Hồ Chí Minh", "Phường Tân Sơn Nhì", "Phường Tân Sơn Nhì",
                    "15 Đường Lũy Bán Bích", upcomingId, 5, 4_000_000m, 720_000_000m, 1_280_000_000m, 38, 67,
                    now.AddDays(7), now.AddDays(70), now.AddDays(-5),
                    "Dự án UPCOMING — chưa mở đăng ký (mobile sẽ ẩn)."),
                Quotas(p5, 5),
                Images(p5, 0)
            ),
            (
                Make(p6, "NOXH Nhà Bè — Đã đóng", "Thành phố Hồ Chí Minh", "Xã Nhà Bè", "Xã Nhà Bè",
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
        var userUpdated = 0;
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
                    DateOfBirth = def.DateOfBirth,
                    Address = def.Address,
                    CreatedAt = now.AddDays(-30)
                };
                db.Users.Add(user);
                userAdded++;
            }
            else
            {
                // Đồng bộ CCCD / DOB / địa chỉ / active nếu seed cũ thiếu
                var changed = false;
                if (string.IsNullOrWhiteSpace(user.CitizenId))
                {
                    user.CitizenId = def.CitizenId;
                    changed = true;
                }
                if (user.DateOfBirth == null)
                {
                    user.DateOfBirth = def.DateOfBirth;
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(user.Address))
                {
                    user.Address = def.Address;
                    changed = true;
                }
                if (user.Status != "Active")
                {
                    user.Status = "Active";
                    changed = true;
                }
                if (user.PasswordHash == null ||
                    !BCrypt.Net.BCrypt.Verify(DemoPassword, user.PasswordHash))
                {
                    user.PasswordHash = passwordHash;
                    changed = true;
                }
                if (changed)
                {
                    user.UpdatedAt = DateTime.UtcNow;
                    userUpdated++;
                }
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

        if (userAdded > 0 || userUpdated > 0 || appAdded > 0 || agreementAdded > 0)
        {
            await db.SaveChangesAsync(ct);
            logger?.LogInformation(
                "Demo seed: applicants +{Users} (~{Updated} patched), applications +{Apps}, agreements +{Agreements}. Password={Password}",
                userAdded, userUpdated, appAdded, agreementAdded, DemoPassword);
        }
        else
        {
            logger?.LogInformation("Demo seed: applicants/applications already present — skip.");
        }
    }

    private const string DemoApplicantAddress = "12 Nguyễn Văn Linh, Phường Tân Phú, Quận 7, Thành phố Hồ Chí Minh";

    private static List<DemoApplicantDef> BuildApplicantApplicationDefs(Guid projectId, DateTime now)
    {
        // Mỗi account một hồ sơ (khớp rào 1 TK = 1 hồ sơ active) — trừ dan.free (chưa có hồ sơ).
        // DateOfBirth / Address bắt buộc cho eKYC & đối chiếu giấy tờ.
        return
        [
            Def("c1000001-0001-0001-0001-000000000001", "d1000001-0001-0001-0001-000000000001",
                "dan.draft@rhs.local", "Nguyễn Văn Draft", "001090000001", "0901000001",
                ApplicationStatusConstants.Draft, PriorityGroupConstants.UrbanPoor, 10, 8_000_000m,
                null, null, false, false, -20, "Công nhân", new DateTime(1992, 3, 12, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-000000000002", "d1000001-0001-0001-0001-000000000002",
                "dan.submitted@rhs.local", "Trần Thị Submitted", "001090000002", "0901000002",
                ApplicationStatusConstants.Submitted, PriorityGroupConstants.UrbanNearPoor, 20, 9_000_000m,
                null, null, false, false, -18, "Nhân viên", new DateTime(1990, 7, 21, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-000000000003", "d1000001-0001-0001-0001-000000000003",
                "dan.reviewing@rhs.local", "Lê Văn Reviewing", "001090000003", "0901000003",
                ApplicationStatusConstants.Reviewing, PriorityGroupConstants.LowIncomeUrban, 30, 10_000_000m,
                null, null, false, false, -16, "Kỹ thuật viên", new DateTime(1988, 11, 5, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-000000000004", "d1000001-0001-0001-0001-000000000004",
                "dan.needdoc@rhs.local", "Phạm Thị NeedDoc", "001090000004", "0901000004",
                ApplicationStatusConstants.NeedMoreDocuments, PriorityGroupConstants.Worker, 25, 11_000_000m,
                null, null, false, false, -15, "Công nhân", new DateTime(1995, 1, 18, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-000000000005", "d1000001-0001-0001-0001-000000000005",
                "dan.pendingsxd@rhs.local", "Hoàng Văn PendingSxd", "001090000005", "0901000005",
                ApplicationStatusConstants.PendingSxdReview, PriorityGroupConstants.UrbanPoor, 40, 7_500_000m,
                null, null, false, false, -14, "Lao động tự do", new DateTime(1987, 9, 30, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-000000000006", "d1000001-0001-0001-0001-000000000006",
                "dan.approved@rhs.local", "Võ Thị Approved", "001090000006", "0901000006",
                ApplicationStatusConstants.Approved, PriorityGroupConstants.UrbanPoor, 50, 8_500_000m,
                null, null, false, false, -12, "Công nhân", new DateTime(1993, 4, 8, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-000000000007", "d1000001-0001-0001-0001-000000000007",
                "dan.timeout@rhs.local", "Đặng Văn Timeout", "001090000007", "0901000007",
                ApplicationStatusConstants.ApprovedByTimeout, PriorityGroupConstants.UrbanNearPoor, 45, 9_500_000m,
                null, null, false, false, -25, "Nhân viên", new DateTime(1989, 12, 2, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-000000000008", "d1000001-0001-0001-0001-000000000008",
                "dan.contract@rhs.local", "Bùi Thị ContractPending", "001090000008", "0901000008",
                ApplicationStatusConstants.ContractPending, PriorityGroupConstants.UrbanPoor, 60, 8_000_000m,
                LotteryResultConstants.Won, null, true, false, -10, "Công nhân", new DateTime(1991, 6, 14, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-000000000009", "d1000001-0001-0001-0001-000000000009",
                "dan.signed@rhs.local", "Ngô Văn ContractSigned", "001090000009", "0901000009",
                ApplicationStatusConstants.ContractSigned, PriorityGroupConstants.Worker, 55, 10_000_000m,
                LotteryResultConstants.PriorityWon, null, true, true, -9, "Công nhân", new DateTime(1986, 8, 25, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-00000000000a", "d1000001-0001-0001-0001-00000000000a",
                "dan.deposit@rhs.local", "Đỗ Thị DepositPaid", "001090000010", "0901000010",
                ApplicationStatusConstants.DepositPaid, PriorityGroupConstants.UrbanPoor, 70, 8_200_000m,
                LotteryResultConstants.Won, "NOXH-TD-001", true, true, -8, "Công nhân", new DateTime(1994, 2, 9, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-00000000000b", "d1000001-0001-0001-0001-00000000000b",
                "dan.priority@rhs.local", "Lý Văn PriorityApproved", "001090000011", "0901000011",
                ApplicationStatusConstants.Approved, PriorityGroupConstants.MeritPerson, 90, 7_000_000m,
                null, null, false, false, -11, "Người có công", new DateTime(1984, 5, 17, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-00000000000c", "d1000001-0001-0001-0001-00000000000c",
                "dan.lost@rhs.local", "Mai Thị LotteryLost", "001090000012", "0901000012",
                ApplicationStatusConstants.LotteryLost, PriorityGroupConstants.LowIncomeUrban, 15, 12_000_000m,
                LotteryResultConstants.Lost, null, false, false, -7, "Nhân viên", new DateTime(1996, 10, 3, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-00000000000d", "d1000001-0001-0001-0001-00000000000d",
                "dan.rejected@rhs.local", "Phan Văn Rejected", "001090000013", "0901000013",
                ApplicationStatusConstants.Rejected, PriorityGroupConstants.UrbanNearPoor, 5, 15_000_000m,
                null, null, false, false, -6, "Buôn bán", new DateTime(1983, 1, 28, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-00000000000e", "d1000001-0001-0001-0001-00000000000e",
                "dan.expired@rhs.local", "Trương Thị Expired", "001090000014", "0901000014",
                ApplicationStatusConstants.Expired, PriorityGroupConstants.Worker, 35, 9_000_000m,
                null, null, false, false, -30, "Công nhân", new DateTime(1997, 7, 11, 0, 0, 0, DateTimeKind.Utc)),

            Def("c1000001-0001-0001-0001-00000000000f", "d1000001-0001-0001-0001-00000000000f",
                "dan.fullypaid@rhs.local", "Huỳnh Văn FullyPaid", "001090000015", "0901000015",
                ApplicationStatusConstants.FullyPaid, PriorityGroupConstants.UrbanPoor, 80, 8_000_000m,
                LotteryResultConstants.Won, "NOXH-TD-002", true, true, -5, "Công nhân", new DateTime(1990, 9, 19, 0, 0, 0, DateTimeKind.Utc)),

            // Account trống — test tạo hồ sơ mới + kiểm tra rào 1 TK 1 hồ sơ
            Def("c1000001-0001-0001-0001-000000000010", "00000000-0000-0000-0000-000000000000",
                DemoApplicantFreeEmail, "Nguyễn Thị Free", "001090000016", "0901000016",
                "", PriorityGroupConstants.UrbanPoor, 0, 8_000_000m,
                null, null, false, false, 0, "Công nhân", new DateTime(1998, 4, 22, 0, 0, 0, DateTimeKind.Utc),
                SkipApplication: true),
        ];
    }

    private static DemoApplicantDef Def(
        string userId,
        string applicationId,
        string email,
        string fullName,
        string citizenId,
        string phone,
        string status,
        string? priorityGroup,
        decimal priorityScore,
        decimal monthlyIncome,
        string? lotteryResult,
        string? slotCode,
        bool needsAgreement,
        bool agreementSigned,
        int submittedDaysAgo,
        string occupation,
        DateTime dateOfBirth,
        string? address = null,
        bool SkipApplication = false)
        => new(
            userId, applicationId, email, fullName, citizenId, phone, status, priorityGroup,
            priorityScore, monthlyIncome, lotteryResult, slotCode, needsAgreement, agreementSigned,
            submittedDaysAgo, occupation, dateOfBirth, address ?? DemoApplicantAddress, SkipApplication);

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
        DateTime DateOfBirth,
        string Address,
        bool SkipApplication = false)
    {
        public Guid UserId => Guid.Parse(UserIdRaw);
        public Guid ApplicationId => Guid.Parse(ApplicationIdRaw);
    }
}
