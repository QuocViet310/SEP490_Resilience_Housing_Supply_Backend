using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Apartment;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class ApartmentService : IApartmentService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ApartmentService> _logger;

    public ApartmentService(
        AppDbContext context,
        ILogger<ApartmentService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task<PagedResultDto<ApartmentDto>> GetApartmentsAsync(
        Guid projectId,
        ApartmentFilterRequestDto filter,
        CancellationToken ct = default)
    {
        var projectExists = await _context.HousingProjects
            .AsNoTracking()
            .AnyAsync(p => p.Id == projectId, ct);

        if (!projectExists)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        var query = _context.Apartments
            .AsNoTracking()
            .Include(a => a.ApartmentType)
            .Where(a => a.ProjectId == projectId);

        // Filter search (UnitName / Description)
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(a => a.UnitName.ToLower().Contains(search) ||
                                     (a.Description != null && a.Description.ToLower().Contains(search)));
        }

        // Filter Floor
        if (filter.FloorNumber.HasValue)
        {
            query = query.Where(a => a.FloorNumber == filter.FloorNumber.Value);
        }

        // Filter Building Block
        if (!string.IsNullOrWhiteSpace(filter.BuildingBlock))
        {
            var block = filter.BuildingBlock.Trim();
            query = query.Where(a => a.BuildingBlock == block);
        }

        // Filter ApartmentType
        if (filter.ApartmentTypeId.HasValue)
        {
            query = query.Where(a => a.ApartmentTypeId == filter.ApartmentTypeId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(filter.ApartmentTypeCode))
        {
            var code = filter.ApartmentTypeCode.Trim().ToUpperInvariant();
            query = query.Where(a => a.ApartmentType != null && a.ApartmentType.TypeCode == code);
        }

        // Filter UnitGroup (PRIORITY / STANDARD)
        if (!string.IsNullOrWhiteSpace(filter.UnitGroup))
        {
            var group = filter.UnitGroup.Trim().ToUpperInvariant();
            query = query.Where(a => a.UnitGroup == group);
        }

        // Filter SaleType (FULL_OWNERSHIP / CO_OWNERSHIP)
        if (!string.IsNullOrWhiteSpace(filter.SaleType))
        {
            var saleType = filter.SaleType.Trim().ToUpperInvariant();
            query = query.Where(a => a.SaleType == saleType);
        }

        // Filter Status (AVAILABLE / ASSIGNED)
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim().ToUpperInvariant();
            query = query.Where(a => a.Status == status);
        }

        // Filter Direction
        if (!string.IsNullOrWhiteSpace(filter.Direction))
        {
            var dir = filter.Direction.Trim().ToUpperInvariant();
            query = query.Where(a => a.MainDoorDirection == dir || a.BalconyDirection == dir);
        }

        // Filter Price
        if (filter.MinPrice.HasValue)
            query = query.Where(a => a.Price >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue)
            query = query.Where(a => a.Price <= filter.MaxPrice.Value);

        // Filter Area
        if (filter.MinArea.HasValue)
            query = query.Where(a => a.Area >= filter.MinArea.Value);
        if (filter.MaxArea.HasValue)
            query = query.Where(a => a.Area <= filter.MaxArea.Value);

        // Filter Bedrooms
        if (filter.NumberOfBedrooms.HasValue)
            query = query.Where(a => a.NumberOfBedrooms == filter.NumberOfBedrooms.Value);

        var totalItems = await query.CountAsync(ct);

        var pageIndex = filter.PageIndex < 1 ? 1 : filter.PageIndex;
        var pageSize  = filter.PageSize < 1 ? 50 : Math.Min(filter.PageSize, 200);

        var apartments = await query
            .OrderBy(a => a.BuildingBlock)
            .ThenBy(a => a.FloorNumber)
            .ThenBy(a => a.UnitName)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResultDto<ApartmentDto>
        {
            Items      = apartments.Select(MapToApartmentDto).ToList(),
            TotalCount = totalItems,
            PageIndex  = pageIndex,
            PageSize   = pageSize
        };
    }

    public async Task<ApartmentDto> GetApartmentByIdAsync(
        Guid projectId,
        Guid apartmentId,
        CancellationToken ct = default)
    {
        var apartment = await _context.Apartments
            .AsNoTracking()
            .Include(a => a.ApartmentType)
            .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Id == apartmentId, ct);

        if (apartment == null)
            throw new KeyNotFoundException($"Không tìm thấy căn hộ với ID {apartmentId} trong dự án {projectId}.");

        return MapToApartmentDto(apartment);
    }

    public async Task<ApartmentDto> CreateApartmentAsync(
        Guid projectId,
        Guid userId,
        CreateApartmentDto dto,
        CancellationToken ct = default)
    {
        var project = await _context.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        // Validate developer ownership if user is Developer
        await ValidateDeveloperAccessAsync(project, userId, ct);

        // Validate unique unit name within project + block
        await ValidateUniqueUnitNameAsync(projectId, dto.BuildingBlock, dto.UnitName, null, ct);

        // Resolve ApartmentTypeId
        var typeId = await ResolveApartmentTypeIdAsync(dto.ApartmentTypeId, dto.ApartmentType, ct);

        // Validate constants
        ValidateApartmentEnums(dto.UnitGroup, dto.SaleType, dto.MainDoorDirection, dto.BalconyDirection);

        var now = DateTime.UtcNow;
        var apartment = new Apartment
        {
            Id                 = Guid.NewGuid(),
            ProjectId          = projectId,
            UnitName           = dto.UnitName.Trim(),
            FloorNumber        = dto.FloorNumber,
            BuildingBlock      = string.IsNullOrWhiteSpace(dto.BuildingBlock) ? null : dto.BuildingBlock.Trim(),
            NumberOfBedrooms   = dto.NumberOfBedrooms,
            NumberOfBathrooms  = dto.NumberOfBathrooms,
            Area               = dto.Area,
            GrossArea          = dto.GrossArea,
            MainDoorDirection  = NormalizeEnum(dto.MainDoorDirection),
            BalconyDirection   = NormalizeEnum(dto.BalconyDirection),
            ViewDescription    = dto.ViewDescription?.Trim(),
            MaxOccupants       = dto.MaxOccupants,
            MinSuitableIncome  = dto.MinSuitableIncome,
            MaxSuitableIncome  = dto.MaxSuitableIncome,
            UnitGroup          = string.IsNullOrWhiteSpace(dto.UnitGroup) ? UnitGroupConstants.Standard : dto.UnitGroup.Trim().ToUpperInvariant(),
            SaleType           = string.IsNullOrWhiteSpace(dto.SaleType) ? SaleTypeConstants.FullOwnership : dto.SaleType.Trim().ToUpperInvariant(),
            CoOwnershipRatio   = dto.CoOwnershipRatio,
            Price              = dto.Price,
            Status             = ApartmentStatusConstants.Available,
            Description        = dto.Description?.Trim(),
            Model3DUrl         = dto.Model3DUrl?.Trim(),
            VirtualTourUrl     = dto.VirtualTourUrl?.Trim(),
            ApartmentTypeId    = typeId,
            CreatedAt          = now,
            UpdatedAt          = now
        };

        _context.Apartments.Add(apartment);
        await _context.SaveChangesAsync(ct);

        // Sync project aggregates
        await SyncProjectAggregatesAsync(projectId, ct);

        _logger.LogInformation("Tạo căn hộ mới thành công: {UnitName} (ID: {ApartmentId}) trong dự án {ProjectId}",
            apartment.UnitName, apartment.Id, projectId);

        return await GetApartmentByIdAsync(projectId, apartment.Id, ct);
    }

    public async Task<List<ApartmentDto>> BatchCreateApartmentsAsync(
        Guid projectId,
        Guid userId,
        BatchCreateApartmentsRequestDto dto,
        CancellationToken ct = default)
    {
        var project = await _context.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        await ValidateDeveloperAccessAsync(project, userId, ct);

        if (dto.Apartments == null || dto.Apartments.Count == 0)
            throw new ArgumentException("Danh sách căn hộ không được rỗng.");

        // Check duplicate within payload
        var duplicatesInPayload = dto.Apartments
            .GroupBy(a => $"{a.BuildingBlock?.Trim()}_{a.UnitName.Trim()}".ToUpperInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatesInPayload.Any())
            throw new InvalidOperationException($"Có mã căn hộ bị trùng trong danh sách gửi lên: {string.Join(", ", duplicatesInPayload)}");

        // Check duplicates against existing in database
        var existingUnits = await _context.Apartments
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .Select(a => new { a.BuildingBlock, a.UnitName })
            .ToListAsync(ct);

        var existingKeys = existingUnits
            .Select(a => $"{a.BuildingBlock?.Trim()}_{a.UnitName.Trim()}".ToUpperInvariant())
            .ToHashSet();

        foreach (var item in dto.Apartments)
        {
            var key = $"{item.BuildingBlock?.Trim()}_{item.UnitName.Trim()}".ToUpperInvariant();
            if (existingKeys.Contains(key))
            {
                throw new InvalidOperationException($"Mã căn '{item.UnitName}' (Tòa: {item.BuildingBlock ?? "Mặc định"}) đã tồn tại trong dự án.");
            }
        }

        var now = DateTime.UtcNow;
        var createdList = new List<Apartment>();

        foreach (var item in dto.Apartments)
        {
            var typeId = await ResolveApartmentTypeIdAsync(item.ApartmentTypeId, item.ApartmentType, ct);
            ValidateApartmentEnums(item.UnitGroup, item.SaleType, item.MainDoorDirection, item.BalconyDirection);

            var apt = new Apartment
            {
                Id                 = Guid.NewGuid(),
                ProjectId          = projectId,
                UnitName           = item.UnitName.Trim(),
                FloorNumber        = item.FloorNumber,
                BuildingBlock      = string.IsNullOrWhiteSpace(item.BuildingBlock) ? null : item.BuildingBlock.Trim(),
                NumberOfBedrooms   = item.NumberOfBedrooms,
                NumberOfBathrooms  = item.NumberOfBathrooms,
                Area               = item.Area,
                GrossArea          = item.GrossArea,
                MainDoorDirection  = NormalizeEnum(item.MainDoorDirection),
                BalconyDirection   = NormalizeEnum(item.BalconyDirection),
                ViewDescription    = item.ViewDescription?.Trim(),
                MaxOccupants       = item.MaxOccupants,
                MinSuitableIncome  = item.MinSuitableIncome,
                MaxSuitableIncome  = item.MaxSuitableIncome,
                UnitGroup          = string.IsNullOrWhiteSpace(item.UnitGroup) ? UnitGroupConstants.Standard : item.UnitGroup.Trim().ToUpperInvariant(),
                SaleType           = string.IsNullOrWhiteSpace(item.SaleType) ? SaleTypeConstants.FullOwnership : item.SaleType.Trim().ToUpperInvariant(),
                CoOwnershipRatio   = item.CoOwnershipRatio,
                Price              = item.Price,
                Status             = ApartmentStatusConstants.Available,
                Description        = item.Description?.Trim(),
                Model3DUrl         = item.Model3DUrl?.Trim(),
                VirtualTourUrl     = item.VirtualTourUrl?.Trim(),
                ApartmentTypeId    = typeId,
                CreatedAt          = now,
                UpdatedAt          = now
            };

            createdList.Add(apt);
        }

        _context.Apartments.AddRange(createdList);
        await _context.SaveChangesAsync(ct);

        // Sync project aggregates
        await SyncProjectAggregatesAsync(projectId, ct);

        _logger.LogInformation("Tạo hàng loạt {Count} căn hộ cho dự án {ProjectId} thành công.",
            createdList.Count, projectId);

        var createdIds = createdList.Select(a => a.Id).ToList();
        var resultList = await _context.Apartments
            .AsNoTracking()
            .Include(a => a.ApartmentType)
            .Where(a => createdIds.Contains(a.Id))
            .OrderBy(a => a.BuildingBlock)
            .ThenBy(a => a.FloorNumber)
            .ThenBy(a => a.UnitName)
            .ToListAsync(ct);

        return resultList.Select(MapToApartmentDto).ToList();
    }

    public async Task<ApartmentDto> UpdateApartmentAsync(
        Guid projectId,
        Guid apartmentId,
        Guid userId,
        UpdateApartmentDto dto,
        CancellationToken ct = default)
    {
        var project = await _context.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        await ValidateDeveloperAccessAsync(project, userId, ct);

        var apartment = await _context.Apartments
            .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Id == apartmentId, ct);

        if (apartment == null)
            throw new KeyNotFoundException($"Không tìm thấy căn hộ {apartmentId} trong dự án {projectId}.");

        // Nếu căn hộ đã ASSIGNED, không cho phép sửa thông tin giá / diện tích / nhóm quỹ căn
        if (string.Equals(apartment.Status, ApartmentStatusConstants.Assigned, StringComparison.OrdinalIgnoreCase))
        {
            if (apartment.Price != dto.Price || Math.Abs(apartment.Area - dto.Area) > 0.001 || apartment.UnitGroup != dto.UnitGroup)
            {
                throw new InvalidOperationException(
                    $"Căn hộ '{apartment.UnitName}' đã được cấp cho hồ sơ trúng tuyển (ASSIGNED). Không được sửa Giá, Diện tích hoặc Phân nhóm quỹ căn.");
            }
        }

        // Validate unique unit name (nếu đổi tên)
        await ValidateUniqueUnitNameAsync(projectId, dto.BuildingBlock, dto.UnitName, apartmentId, ct);

        // Resolve ApartmentTypeId
        var typeId = await ResolveApartmentTypeIdAsync(dto.ApartmentTypeId, dto.ApartmentType, ct);

        ValidateApartmentEnums(dto.UnitGroup, dto.SaleType, dto.MainDoorDirection, dto.BalconyDirection);

        apartment.UnitName           = dto.UnitName.Trim();
        apartment.FloorNumber        = dto.FloorNumber;
        apartment.BuildingBlock      = string.IsNullOrWhiteSpace(dto.BuildingBlock) ? null : dto.BuildingBlock.Trim();
        apartment.NumberOfBedrooms   = dto.NumberOfBedrooms;
        apartment.NumberOfBathrooms  = dto.NumberOfBathrooms;
        apartment.Area               = dto.Area;
        apartment.GrossArea          = dto.GrossArea;
        apartment.MainDoorDirection  = NormalizeEnum(dto.MainDoorDirection);
        apartment.BalconyDirection   = NormalizeEnum(dto.BalconyDirection);
        apartment.ViewDescription    = dto.ViewDescription?.Trim();
        apartment.MaxOccupants       = dto.MaxOccupants;
        apartment.MinSuitableIncome  = dto.MinSuitableIncome;
        apartment.MaxSuitableIncome  = dto.MaxSuitableIncome;
        apartment.UnitGroup          = string.IsNullOrWhiteSpace(dto.UnitGroup) ? UnitGroupConstants.Standard : dto.UnitGroup.Trim().ToUpperInvariant();
        apartment.SaleType           = string.IsNullOrWhiteSpace(dto.SaleType) ? SaleTypeConstants.FullOwnership : dto.SaleType.Trim().ToUpperInvariant();
        apartment.CoOwnershipRatio   = dto.CoOwnershipRatio;
        apartment.Price              = dto.Price;
        apartment.Description        = dto.Description?.Trim();
        apartment.Model3DUrl         = dto.Model3DUrl?.Trim();
        apartment.VirtualTourUrl     = dto.VirtualTourUrl?.Trim();
        apartment.ApartmentTypeId    = typeId;
        apartment.UpdatedAt          = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        // Sync aggregates
        await SyncProjectAggregatesAsync(projectId, ct);

        _logger.LogInformation("Cập nhật căn hộ {ApartmentId} ({UnitName}) thành công.", apartmentId, apartment.UnitName);

        return await GetApartmentByIdAsync(projectId, apartmentId, ct);
    }

    public async Task<bool> DeleteApartmentAsync(
        Guid projectId,
        Guid apartmentId,
        Guid userId,
        CancellationToken ct = default)
    {
        var project = await _context.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        await ValidateDeveloperAccessAsync(project, userId, ct);

        var apartment = await _context.Apartments
            .FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Id == apartmentId, ct);

        if (apartment == null)
            return false;

        // Check if assigned to any application
        if (string.Equals(apartment.Status, ApartmentStatusConstants.Assigned, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Không thể xóa căn hộ '{apartment.UnitName}' vì đã được cấp cho hồ sơ người mua.");
        }

        var isReferencedInApps = await _context.HousingApplications
            .AnyAsync(app => app.ApartmentId == apartmentId, ct);

        if (isReferencedInApps)
        {
            throw new InvalidOperationException(
                $"Không thể xóa căn hộ '{apartment.UnitName}' vì có hồ sơ đăng ký đang liên kết.");
        }

        _context.Apartments.Remove(apartment);
        await _context.SaveChangesAsync(ct);

        // Sync aggregates
        await SyncProjectAggregatesAsync(projectId, ct);

        _logger.LogInformation("Xóa căn hộ {ApartmentId} khỏi dự án {ProjectId} thành công.", apartmentId, projectId);
        return true;
    }

    public async Task<FloorPlanResponseDto> GetFloorPlanAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var project = await _context.HousingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        var apartments = await _context.Apartments
            .AsNoTracking()
            .Include(a => a.ApartmentType)
            .Where(a => a.ProjectId == projectId)
            .OrderBy(a => a.BuildingBlock)
            .ThenBy(a => a.FloorNumber)
            .ThenBy(a => a.UnitName)
            .ToListAsync(ct);

        var response = new FloorPlanResponseDto
        {
            ProjectId           = project.Id,
            ProjectName         = project.ProjectName,
            TotalApartments     = apartments.Count,
            AvailableApartments = apartments.Count(a => string.Equals(a.Status, ApartmentStatusConstants.Available, StringComparison.OrdinalIgnoreCase)),
            AssignedApartments  = apartments.Count(a => string.Equals(a.Status, ApartmentStatusConstants.Assigned, StringComparison.OrdinalIgnoreCase))
        };

        var blockGroups = apartments
            .GroupBy(a => string.IsNullOrWhiteSpace(a.BuildingBlock) ? "Khối Nhà Chính" : a.BuildingBlock.Trim())
            .OrderBy(g => g.Key);

        foreach (var bg in blockGroups)
        {
            var blockDto = new FloorPlanBlockDto
            {
                BlockName               = bg.Key,
                TotalApartmentsInBlock = bg.Count()
            };

            var floorGroups = bg
                .GroupBy(a => a.FloorNumber)
                .OrderBy(g => g.Key);

            foreach (var fg in floorGroups)
            {
                blockDto.Floors.Add(new FloorPlanFloorDto
                {
                    FloorNumber             = fg.Key,
                    TotalApartmentsOnFloor = fg.Count(),
                    Apartments              = fg.Select(MapToApartmentDto).ToList()
                });
            }

            response.Blocks.Add(blockDto);
        }

        return response;
    }

    public async Task<ApartmentStatisticsResponseDto> GetApartmentStatisticsAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var project = await _context.HousingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        var apartments = await _context.Apartments
            .AsNoTracking()
            .Include(a => a.ApartmentType)
            .Where(a => a.ProjectId == projectId)
            .ToListAsync(ct);

        var total = apartments.Count;
        var available = apartments.Count(a => string.Equals(a.Status, ApartmentStatusConstants.Available, StringComparison.OrdinalIgnoreCase));
        var assigned  = apartments.Count(a => string.Equals(a.Status, ApartmentStatusConstants.Assigned, StringComparison.OrdinalIgnoreCase));

        var stats = new ApartmentStatisticsResponseDto
        {
            ProjectId           = project.Id,
            ProjectName         = project.ProjectName,
            TotalUnits          = total,
            AvailableUnits      = available,
            AssignedUnits       = assigned,
            PriorityUnits       = apartments.Count(a => a.UnitGroup == UnitGroupConstants.Priority),
            StandardUnits       = apartments.Count(a => a.UnitGroup == UnitGroupConstants.Standard),
            FullOwnershipUnits  = apartments.Count(a => a.SaleType == SaleTypeConstants.FullOwnership),
            CoOwnershipUnits    = apartments.Count(a => a.SaleType == SaleTypeConstants.CoOwnership),
            MinPrice            = total > 0 ? apartments.Min(a => a.Price) : 0,
            MaxPrice            = total > 0 ? apartments.Max(a => a.Price) : 0,
            MinArea             = total > 0 ? apartments.Min(a => a.Area) : 0,
            MaxArea             = total > 0 ? apartments.Max(a => a.Area) : 0
        };

        // Group by ApartmentType
        var typeGroups = apartments
            .GroupBy(a => a.ApartmentTypeId);

        foreach (var tg in typeGroups)
        {
            var first = tg.First();
            stats.ByType.Add(new ApartmentTypeStatDto
            {
                TypeId         = tg.Key,
                TypeCode       = first.ApartmentType?.TypeCode ?? "UNASSIGNED",
                TypeName       = first.ApartmentType?.TypeName ?? "Chưa phân loại",
                TotalCount     = tg.Count(),
                AvailableCount = tg.Count(a => string.Equals(a.Status, ApartmentStatusConstants.Available, StringComparison.OrdinalIgnoreCase)),
                AssignedCount  = tg.Count(a => string.Equals(a.Status, ApartmentStatusConstants.Assigned, StringComparison.OrdinalIgnoreCase))
            });
        }

        // Group by Floor
        var floorGroups = apartments
            .GroupBy(a => new { a.BuildingBlock, a.FloorNumber })
            .OrderBy(g => g.Key.BuildingBlock)
            .ThenBy(g => g.Key.FloorNumber);

        foreach (var fg in floorGroups)
        {
            stats.ByFloor.Add(new FloorStatDto
            {
                Block          = fg.Key.BuildingBlock ?? "Khối Nhà Chính",
                FloorNumber    = fg.Key.FloorNumber,
                TotalCount     = fg.Count(),
                AvailableCount = fg.Count(a => string.Equals(a.Status, ApartmentStatusConstants.Available, StringComparison.OrdinalIgnoreCase)),
                AssignedCount  = fg.Count(a => string.Equals(a.Status, ApartmentStatusConstants.Assigned, StringComparison.OrdinalIgnoreCase))
            });
        }

        return stats;
    }

    // ─────────────────────────────────────────────────────────────
    // Helper Methods
    // ─────────────────────────────────────────────────────────────

    private static ApartmentDto MapToApartmentDto(Apartment a)
    {
        return new ApartmentDto
        {
            Id                     = a.Id,
            ProjectId              = a.ProjectId,
            UnitName               = a.UnitName,
            FloorNumber            = a.FloorNumber,
            BuildingBlock          = a.BuildingBlock,
            NumberOfBedrooms       = a.NumberOfBedrooms,
            NumberOfBathrooms      = a.NumberOfBathrooms,
            Area                   = a.Area,
            GrossArea              = a.GrossArea,
            MainDoorDirection      = a.MainDoorDirection,
            MainDoorDirectionLabel = DirectionConstants.GetDisplayName(a.MainDoorDirection),
            BalconyDirection       = a.BalconyDirection,
            BalconyDirectionLabel  = DirectionConstants.GetDisplayName(a.BalconyDirection),
            ViewDescription        = a.ViewDescription,
            MaxOccupants           = a.MaxOccupants,
            MinSuitableIncome      = a.MinSuitableIncome,
            MaxSuitableIncome      = a.MaxSuitableIncome,
            UnitGroup              = a.UnitGroup,
            UnitGroupLabel         = UnitGroupConstants.GetDisplayName(a.UnitGroup),
            SaleType               = a.SaleType,
            SaleTypeLabel          = SaleTypeConstants.GetDisplayName(a.SaleType),
            CoOwnershipRatio       = a.CoOwnershipRatio,
            Price                  = a.Price,
            Status                 = a.Status,
            Description            = a.Description,
            Model3DUrl             = a.Model3DUrl,
            VirtualTourUrl         = a.VirtualTourUrl,
            ApartmentTypeId        = a.ApartmentTypeId,
            ApartmentType          = a.ApartmentType?.TypeCode ?? string.Empty,
            ApartmentTypeLabel     = a.ApartmentType?.TypeName ?? string.Empty,
            CreatedAt              = a.CreatedAt,
            UpdatedAt              = a.UpdatedAt
        };
    }

    private async Task ValidateDeveloperAccessAsync(HousingProject project, Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return; // Admin / system bypass

        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return;

        var role = user.Role?.RoleName ?? string.Empty;
        if (role == RoleConstants.HousingDeveloper)
        {
            if (project.DeveloperId.HasValue && project.DeveloperId.Value != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền quản trị quỹ căn của dự án này.");
            }
        }
    }

    private async Task ValidateUniqueUnitNameAsync(
        Guid projectId,
        string? buildingBlock,
        string unitName,
        Guid? currentApartmentId,
        CancellationToken ct)
    {
        var block = string.IsNullOrWhiteSpace(buildingBlock) ? null : buildingBlock.Trim();
        var name  = unitName.Trim();

        var query = _context.Apartments
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId && a.BuildingBlock == block && a.UnitName == name);

        if (currentApartmentId.HasValue)
            query = query.Where(a => a.Id != currentApartmentId.Value);

        var exists = await query.AnyAsync(ct);
        if (exists)
        {
            var blockText = block != null ? $" (Tòa: {block})" : string.Empty;
            throw new InvalidOperationException($"Mã căn hộ '{name}'{blockText} đã tồn tại trong dự án này.");
        }
    }

    private async Task<Guid?> ResolveApartmentTypeIdAsync(
        Guid? requestedTypeId,
        string? requestedTypeCode,
        CancellationToken ct)
    {
        if (requestedTypeId.HasValue && requestedTypeId.Value != Guid.Empty)
        {
            var exists = await _context.ApartmentTypes.AnyAsync(t => t.Id == requestedTypeId.Value, ct);
            if (exists)
                return requestedTypeId.Value;
        }

        if (!string.IsNullOrWhiteSpace(requestedTypeCode))
        {
            var code = requestedTypeCode.Trim().ToUpperInvariant();
            var matchedType = await _context.ApartmentTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TypeCode == code, ct);

            if (matchedType != null)
                return matchedType.Id;
        }

        return null;
    }

    private static void ValidateApartmentEnums(
        string? unitGroup,
        string? saleType,
        string? mainDoorDir,
        string? balconyDir)
    {
        if (!string.IsNullOrWhiteSpace(unitGroup) && !UnitGroupConstants.IsValid(unitGroup))
        {
            throw new ArgumentException($"Nhóm quỹ căn '{unitGroup}' không hợp lệ. Cho phép: {string.Join(", ", UnitGroupConstants.All)}");
        }

        if (!string.IsNullOrWhiteSpace(saleType) && !SaleTypeConstants.IsValid(saleType))
        {
            throw new ArgumentException($"Hình thức mở bán '{saleType}' không hợp lệ. Cho phép: {string.Join(", ", SaleTypeConstants.All)}");
        }

        if (!string.IsNullOrWhiteSpace(mainDoorDir) && !DirectionConstants.IsValid(mainDoorDir))
        {
            throw new ArgumentException($"Hướng cửa '{mainDoorDir}' không hợp lệ. Cho phép: {string.Join(", ", DirectionConstants.All)}");
        }

        if (!string.IsNullOrWhiteSpace(balconyDir) && !DirectionConstants.IsValid(balconyDir))
        {
            throw new ArgumentException($"Hướng ban công '{balconyDir}' không hợp lệ. Cho phép: {string.Join(", ", DirectionConstants.All)}");
        }
    }

    private static string? NormalizeEnum(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private async Task SyncProjectAggregatesAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _context.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null) return;

        var apartments = await _context.Apartments
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .ToListAsync(ct);

        if (apartments.Count > 0)
        {
            project.AvailableUnits = apartments.Count(a => string.Equals(a.Status, ApartmentStatusConstants.Available, StringComparison.OrdinalIgnoreCase));
            project.MinPrice       = apartments.Min(a => a.Price);
            project.MaxPrice       = apartments.Max(a => a.Price);
            project.MinArea        = apartments.Min(a => a.Area);
            project.MaxArea        = apartments.Max(a => a.Area);
            project.UpdatedAt      = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
        }
    }
}
