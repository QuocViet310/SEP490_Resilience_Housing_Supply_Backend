using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;
using RHS.Infrastructure.Helpers;

namespace RHS.Infrastructure.Repositories;

public class HousingProjectRepository : IHousingProjectRepository
{
    private readonly AppDbContext _context;

    public HousingProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request,
        Guid? currentUserId = null,
        string? currentUserRole = null)
    {
        // Build the query with filtering
        IQueryable<HousingProject> query = _context.HousingProjects
            .Include(x => x.HousingProjectStatus)
            .Include(x => x.ProjectImages)
            .AsNoTracking();

        // Apply security status filter
        query = query.Where(x =>
            (x.HousingProjectStatus != null && x.HousingProjectStatus.StatusCode == "UPCOMING") ||
            (x.HousingProjectStatus != null && x.HousingProjectStatus.StatusCode == "OPEN") ||
            (x.HousingProjectStatus != null && x.HousingProjectStatus.StatusCode == "CLOSED") ||
            (x.HousingProjectStatus != null && x.HousingProjectStatus.StatusCode == "FULL") ||
            (currentUserId.HasValue && x.DeveloperId == currentUserId.Value) ||
            (currentUserRole == "Department Of Construction" || currentUserRole == "System Administrator")
        );

        // Apply search filter (tên + địa chỉ — đồng bộ web/mobile)
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            query = query.Where(x =>
                x.ProjectName.ToLower().Contains(searchTerm)
                || (x.District != null && x.District.ToLower().Contains(searchTerm))
                || (x.Ward != null && x.Ward.ToLower().Contains(searchTerm))
                || (x.Street != null && x.Street.ToLower().Contains(searchTerm))
                || (x.Description != null && x.Description.ToLower().Contains(searchTerm)));
        }

        // Apply province filter
        if (!string.IsNullOrWhiteSpace(request.Province))
        {
            query = query.Where(x => x.Province == request.Province);
        }

        // Apply district filter (legacy quận/huyện)
        if (!string.IsNullOrWhiteSpace(request.District))
        {
            query = query.Where(x => x.District == request.District);
        }

        // Apply ward filter (địa giới v2) — exact match Ward hoặc District (CRUD đồng bộ cùng tên)
        if (!string.IsNullOrWhiteSpace(request.Ward))
        {
            var ward = request.Ward.Trim();
            query = query.Where(x =>
                (x.Ward != null && x.Ward == ward)
                || (x.District != null && x.District == ward));
        }

        // Apply min price filter
        if (request.MinPrice.HasValue)
        {
            query = query.Where(x => x.MaxPrice >= request.MinPrice.Value);
        }

        // Apply max price filter
        if (request.MaxPrice.HasValue)
        {
            query = query.Where(x => x.MinPrice <= request.MaxPrice.Value);
        }

        // Apply min area filter
        if (request.MinArea.HasValue)
        {
            query = query.Where(x => x.MaxArea >= request.MinArea.Value);
        }

        // Apply max area filter
        if (request.MaxArea.HasValue)
        {
            query = query.Where(x => x.MinArea <= request.MaxArea.Value);
        }

        // Apply status filter
        if (request.StatusId.HasValue)
        {
            query = query.Where(x => x.HousingProjectStatusId == request.StatusId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.StatusCode))
        {
            var code = request.StatusCode.Trim().ToUpper();
            if (code == "OPEN_FOR_REGISTRATION")
            {
                code = "OPEN";
            }
            query = query.Where(x => x.HousingProjectStatus != null && x.HousingProjectStatus.StatusCode == code);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting (newest first)
        query = query.OrderByDescending(x => x.CreatedAt);

        // Apply pagination
        var pageIndex = Math.Max(request.PageIndex, 1);
        var pageSize = Math.Max(request.PageSize, 1);
        var skip = (pageIndex - 1) * pageSize;

        var projects = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        // Map to DTOs
        var items = projects.Select(x => new HousingProjectResponseDto
        {
            Id = x.Id,
            ProjectName = x.ProjectName,
            Description = x.Description,
            Province = x.Province,
            District = x.District,
            Street = x.Street,
            Ward = x.Ward,
            LotteryDate = x.LotteryDate,
            LotteryLocation = x.LotteryLocation,
            MinPrice = x.MinPrice,
            MaxPrice = x.MaxPrice,
            MinArea = x.MinArea,
            MaxArea = x.MaxArea,
            AvailableUnits = x.AvailableUnits,
            ThumbnailUrl = x.ThumbnailUrl,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            Status = x.HousingProjectStatus?.StatusName,
            DecisionNumber = x.DecisionNumber,
            ApprovalDate = x.ApprovalDate,
            ApplicationOpenDate = x.ApplicationOpenDate,
            ApplicationCloseDate = x.ApplicationCloseDate,
            RejectReason = x.RejectReason,
            Images = x.ProjectImages
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new ProjectImageResponseDto
                {
                    Id = p.Id,
                    ImageUrl = p.ImageUrl,
                    DisplayOrder = p.DisplayOrder
                })
                .ToList()
        }).ToList();

        return new PagedResultDto<HousingProjectResponseDto>
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<HousingProject> CreateAsync(HousingProject entity)
    {
        entity.CreatedAt = DateTime.UtcNow;
        _context.HousingProjects.Add(entity);
        await _context.SaveChangesAsync();

        if (entity.Apartments.Count > 0)
        {
            await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_context, entity.Id);
            await _context.SaveChangesAsync();
        }

        return entity;
    }

    public async Task<HousingProject?> GetByIdAsync(Guid id)
    {
        return await _context.HousingProjects
            .AsSplitQuery()
            .Include(x => x.HousingProjectStatus)
            .Include(x => x.ProjectImages)
            .Include(x => x.Apartments)
                .ThenInclude(a => a.ApartmentType)
            .Include(x => x.PaymentMilestones)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task UpdateAsync(HousingProject entity)
    {
        var existing = await _context.HousingProjects
            .Include(x => x.ProjectImages)
            .Include(x => x.PaymentMilestones)
            .FirstOrDefaultAsync(x => x.Id == entity.Id);

        if (existing == null)
        {
            throw new InvalidOperationException($"Housing project with ID {entity.Id} not found.");
        }

        // Cập nhật thông tin dự án
        existing.ProjectName = entity.ProjectName;
        existing.Description = entity.Description;
        existing.Province = entity.Province;
        existing.District = entity.District;
        existing.Street = entity.Street;
        existing.Ward = entity.Ward;
        existing.MinPrice = entity.MinPrice;
        existing.MaxPrice = entity.MaxPrice;
        existing.MinArea = entity.MinArea;
        existing.MaxArea = entity.MaxArea;
        existing.AvailableUnits = entity.AvailableUnits;
        existing.ThumbnailUrl = entity.ThumbnailUrl;
        existing.DecisionNumber = entity.DecisionNumber;
        existing.DecisionDocumentUrl = entity.DecisionDocumentUrl;
        existing.ApplicationOpenDate = entity.ApplicationOpenDate;
        existing.ApplicationCloseDate = entity.ApplicationCloseDate;
        existing.UpdatedAt = DateTime.UtcNow;

        if (entity.DeveloperId.HasValue)
        {
            existing.DeveloperId = entity.DeveloperId;
        }

        // Cập nhật thư viện hình ảnh dự án
        if (entity.ProjectImages != null)
        {
            _context.ProjectImages.RemoveRange(existing.ProjectImages);
            existing.ProjectImages.Clear();

            foreach (var img in entity.ProjectImages)
            {
                existing.ProjectImages.Add(new ProjectImage
                {
                    Id = img.Id == Guid.Empty ? Guid.NewGuid() : img.Id,
                    ProjectId = existing.Id,
                    ImageUrl = img.ImageUrl,
                    DisplayOrder = img.DisplayOrder,
                    CreatedAt = img.CreatedAt == default ? DateTime.UtcNow : img.CreatedAt
                });
            }
        }

        // Cập nhật lịch thanh toán (nếu có)
        if (entity.PaymentMilestones != null && entity.PaymentMilestones.Count > 0)
        {
            _context.PaymentMilestones.RemoveRange(existing.PaymentMilestones);
            existing.PaymentMilestones.Clear();

            foreach (var m in entity.PaymentMilestones)
            {
                existing.PaymentMilestones.Add(new PaymentMilestone
                {
                    Id = m.Id == Guid.Empty ? Guid.NewGuid() : m.Id,
                    ProjectId = existing.Id,
                    PhaseOrder = m.PhaseOrder,
                    PhaseName = m.PhaseName,
                    CalculationType = m.CalculationType,
                    FixedAmount = m.FixedAmount,
                    Percentage = m.Percentage,
                    TriggerEvent = m.TriggerEvent,
                    DueDays = m.DueDays,
                    Description = m.Description,
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt == default ? DateTime.UtcNow : m.CreatedAt
                });
            }
        }

        await _context.SaveChangesAsync();

        // AvailableUnits = Count(AVAILABLE) − soft-hold
        await ProjectUnitSeatHelper.SyncAvailableUnitsAsync(_context, entity.Id);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusOnlyAsync(HousingProject entity)
    {
        var tracked = await _context.HousingProjects
            .FirstOrDefaultAsync(x => x.Id == entity.Id);
        if (tracked == null)
        {
            throw new InvalidOperationException($"Housing project with ID {entity.Id} not found.");
        }

        tracked.HousingProjectStatusId = entity.HousingProjectStatusId;
        tracked.RejectReason = entity.RejectReason;
        tracked.ApprovalDate = entity.ApprovalDate;
        tracked.PublicAnnounceAt = entity.PublicAnnounceAt;
        tracked.ApplicationOpenDate = entity.ApplicationOpenDate;
        tracked.ApplicationCloseDate = entity.ApplicationCloseDate;
        tracked.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteAsync(HousingProject entity)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        _context.HousingProjects.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.HousingProjects
            .AsNoTracking()
            .AnyAsync(x => x.Id == id);
    }

    public async Task<bool> StatusExistsAsync(Guid statusId)
    {
        return await _context.HousingProjectStatuses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x => x.Id == statusId);
    }

    public async Task<HousingProjectStatus?> GetStatusByCodeAsync(string code)
    {
        return await _context.HousingProjectStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.StatusCode == code);
    }

    public async Task<HousingProject?> GetActiveProjectByNameAsync(
        string projectName,
        Guid? developerId = null,
        Guid? excludeProjectId = null)
    {
        var normalizedName = projectName.Trim().ToLower();

        var query = _context.HousingProjects
            .Include(x => x.HousingProjectStatus)
            .Where(x => !x.IsDeleted
                        && x.ProjectName.ToLower() == normalizedName
                        && x.HousingProjectStatus != null
                        && x.HousingProjectStatus.StatusCode != "CLOSED"
                        && x.HousingProjectStatus.StatusCode != "REJECTED");

        if (excludeProjectId.HasValue && excludeProjectId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Id != excludeProjectId.Value);
        }

        if (developerId.HasValue && developerId.Value != Guid.Empty)
        {
            query = query.Where(x => x.DeveloperId == developerId.Value);
        }

        return await query.AsNoTracking().FirstOrDefaultAsync();
    }
}
