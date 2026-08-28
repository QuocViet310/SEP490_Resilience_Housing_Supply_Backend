using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using RHS.Application.DTOs.Apartment;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;
using System.Drawing;

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

    public async Task<byte[]> GenerateApartmentExcelTemplateAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await _context.HousingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();

        // Sheet 1: Danh sách căn hộ mẫu
        var ws = package.Workbook.Worksheets.Add("DanhSachCanHo");
        ws.Cells.Style.Font.Name = "Segoe UI";
        ws.Cells.Style.Font.Size = 11;

        // Headers
        var headers = new[]
        {
            "Mã / Tên căn (*)",
            "Tòa / Block (*)",
            "Tầng (*)",
            "Số phòng ngủ (*)",
            "Số phòng vệ sinh (*)",
            "Diện tích thông thủy (m²) (*)",
            "Diện tích tim tường (m²)",
            "Giá bán (VNĐ) (*)",
            "Hướng cửa chính",
            "Hướng ban công",
            "Phân nhóm căn",
            "Hình thức bán",
            "Tầm nhìn / View",
            "Số người ở tối đa",
            "Mô tả chi tiết"
        };

        for (int col = 1; col <= headers.Length; col++)
        {
            var cell = ws.Cells[1, col];
            cell.Value = headers[col - 1];
            cell.Style.Font.Bold = true;
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 78, 121)); // Navy Blue
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(180, 180, 180));
        }
        ws.Row(1).Height = 28;

        // Dữ liệu mẫu (3 dòng minh họa)
        var sampleRows = new List<object[]>
        {
            new object[] { "CH-01.01", "Tòa A", 1, 2, 1, 65.5, 69.0, 1250000000m, "DONG", "NAM", "STANDARD", "FULL_OWNERSHIP", "View công viên nội khu", 4, "Căn hộ 2PN tiêu chuẩn NOXH" },
            new object[] { "CH-01.02", "Tòa A", 1, 3, 2, 77.0, 82.5, 1480000000m, "TAY_NAM", "DONG_BAC", "PRIORITY", "FULL_OWNERSHIP", "Căn góc 3PN thoáng mát", 5, "Căn hộ ưu tiên hộ gia đình đông người" },
            new object[] { "CH-02.01", "Tòa A", 2, 2, 2, 70.0, 74.0, 1330000000m, "NAM", "BAC", "STANDARD", "CO_OWNERSHIP", "View hồ cảnh quan", 4, "Căn hộ mở bán theo hình thức đồng sở hữu" }
        };

        for (int r = 0; r < sampleRows.Count; r++)
        {
            int rowIdx = r + 2;
            var data = sampleRows[r];
            for (int c = 0; c < data.Length; c++)
            {
                var cell = ws.Cells[rowIdx, c + 1];
                cell.Value = data[c];
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(220, 220, 220));

                if (c == 2 || c == 3 || c == 4 || c == 13) // Floor, Beds, Baths, MaxOccupants
                {
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    cell.Style.Numberformat.Format = "#,##0";
                }
                else if (c == 5 || c == 6) // Areas
                {
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    cell.Style.Numberformat.Format = "#,##0.00";
                }
                else if (c == 7) // Price
                {
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    cell.Style.Numberformat.Format = "#,##0";
                }
                else if (c == 8 || c == 9 || c == 10 || c == 11) // Enums
                {
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
            }
            ws.Row(rowIdx).Height = 22;
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        for (int col = 1; col <= headers.Length; col++)
        {
            if (ws.Column(col).Width < 14)
                ws.Column(col).Width = 14;
        }

        // Sheet 2: Danh mục mã quy ước & Hướng dẫn
        var wsGuide = package.Workbook.Worksheets.Add("HuongDan_DanhMuc");
        wsGuide.Cells.Style.Font.Name = "Segoe UI";
        wsGuide.Cells.Style.Font.Size = 11;

        wsGuide.Cells["A1"].Value = "BẢNG DANH MỤC GIÁ TRỊ HỢP LỆ CHO CÁC CỘT ENUM TRONG FILE IMPORT CĂN HỘ";
        wsGuide.Cells["A1:E1"].Merge = true;
        wsGuide.Cells["A1"].Style.Font.Bold = true;
        wsGuide.Cells["A1"].Style.Font.Size = 13;
        wsGuide.Cells["A1"].Style.Font.Color.SetColor(Color.FromArgb(31, 78, 121));

        // Hướng nhà
        wsGuide.Cells["A3"].Value = "Cột Hướng cửa chính / Hướng ban công";
        wsGuide.Cells["A3:B3"].Merge = true;
        wsGuide.Cells["A3"].Style.Font.Bold = true;

        wsGuide.Cells["A4"].Value = "Mã nhập vào Excel";
        wsGuide.Cells["B4"].Value = "Ý nghĩa hiển thị";
        wsGuide.Cells["A4:B4"].Style.Font.Bold = true;
        wsGuide.Cells["A4:B4"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        wsGuide.Cells["A4:B4"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 230, 241));

        var directions = new[]
        {
            ("DONG", "Đông"),
            ("TAY", "Tây"),
            ("NAM", "Nam"),
            ("BAC", "Bắc"),
            ("DONG_NAM", "Đông Nam"),
            ("DONG_BAC", "Đông Bắc"),
            ("TAY_NAM", "Tây Nam"),
            ("TAY_BAC", "Tây Bắc")
        };
        for (int i = 0; i < directions.Length; i++)
        {
            wsGuide.Cells[5 + i, 1].Value = directions[i].Item1;
            wsGuide.Cells[5 + i, 2].Value = directions[i].Item2;
        }

        // Phân nhóm căn
        wsGuide.Cells["D3"].Value = "Cột Phân nhóm căn";
        wsGuide.Cells["D3:E3"].Merge = true;
        wsGuide.Cells["D3"].Style.Font.Bold = true;

        wsGuide.Cells["D4"].Value = "Mã nhập vào Excel";
        wsGuide.Cells["E4"].Value = "Ý nghĩa";
        wsGuide.Cells["D4:E4"].Style.Font.Bold = true;
        wsGuide.Cells["D4:E4"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        wsGuide.Cells["D4:E4"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 230, 241));

        wsGuide.Cells["D5"].Value = "STANDARD";
        wsGuide.Cells["E5"].Value = "Căn hộ Tiêu chuẩn (Mặc định nếu để trống)";
        wsGuide.Cells["D6"].Value = "PRIORITY";
        wsGuide.Cells["E6"].Value = "Căn hộ Ưu tiên (Dành cho đối tượng chính sách/điểm cao)";

        // Hình thức bán
        wsGuide.Cells["D8"].Value = "Cột Hình thức bán";
        wsGuide.Cells["D8:E8"].Merge = true;
        wsGuide.Cells["D8"].Style.Font.Bold = true;

        wsGuide.Cells["D9"].Value = "Mã nhập vào Excel";
        wsGuide.Cells["E9"].Value = "Ý nghĩa";
        wsGuide.Cells["D9:E9"].Style.Font.Bold = true;
        wsGuide.Cells["D9:E9"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        wsGuide.Cells["D9:E9"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 230, 241));

        wsGuide.Cells["D10"].Value = "FULL_OWNERSHIP";
        wsGuide.Cells["E10"].Value = "Sở hữu toàn bộ 100% (Mặc định nếu để trống)";
        wsGuide.Cells["D11"].Value = "CO_OWNERSHIP";
        wsGuide.Cells["E11"].Value = "Đồng sở hữu (với Nhà nước / CĐT)";

        wsGuide.Cells[wsGuide.Dimension.Address].AutoFitColumns();

        return await package.GetAsByteArrayAsync(ct);
    }

    public async Task<ApartmentExcelImportResultDto> ImportApartmentsFromExcelAsync(
        Guid projectId,
        Guid userId,
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Vui lòng chọn file Excel (.xlsx) để tải lên.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
        {
            throw new ArgumentException("Định dạng file không được hỗ trợ. Vui lòng tải lên file Excel (.xlsx hoặc .xls).");
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            throw new ArgumentException("Dung lượng file vượt quá giới hạn 10MB.");
        }

        var project = await _context.HousingProjects
            .Include(p => p.HousingProjectStatus)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        // Kiểm tra quyền CĐT sở hữu
        await ValidateDeveloperAccessAsync(project, userId, ct);

        // Lấy danh sách UnitName hiện có trong DB để check duplicate (ghép Block + UnitName)
        var existingUnits = await _context.Apartments
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .Select(a => new { a.BuildingBlock, a.UnitName })
            .ToListAsync(ct);

        var existingKeySet = existingUnits
            .Select(a => $"{a.BuildingBlock?.Trim()}_{a.UnitName.Trim()}".ToUpperInvariant())
            .ToHashSet();

        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        using var package = new ExcelPackage(stream);

        var ws = package.Workbook.Worksheets.FirstOrDefault();
        if (ws == null || ws.Dimension == null || ws.Dimension.End.Row < 2)
        {
            throw new ArgumentException("File Excel rỗng hoặc không tìm thấy dữ liệu căn hộ (cần tối thiểu 1 dòng tiêu đề và 1 dòng dữ liệu).");
        }

        var rowErrors = new List<ApartmentExcelRowErrorDto>();
        var apartmentsToCreate = new List<Apartment>();
        var fileKeysInProcess = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int totalDataRows = 0;
        int endRow = ws.Dimension.End.Row;

        for (int r = 2; r <= endRow; r++)
        {
            // Đọc các cột
            var unitNameStr = ws.Cells[r, 1].Text?.Trim();
            var blockStr = ws.Cells[r, 2].Text?.Trim();
            var floorStr = ws.Cells[r, 3].Text?.Trim();
            var bedsStr = ws.Cells[r, 4].Text?.Trim();
            var bathsStr = ws.Cells[r, 5].Text?.Trim();
            var areaStr = ws.Cells[r, 6].Text?.Trim();
            var grossAreaStr = ws.Cells[r, 7].Text?.Trim();
            var priceStr = ws.Cells[r, 8].Text?.Trim();
            var mainDoorDirStr = ws.Cells[r, 9].Text?.Trim();
            var balconyDirStr = ws.Cells[r, 10].Text?.Trim();
            var unitGroupStr = ws.Cells[r, 11].Text?.Trim();
            var saleTypeStr = ws.Cells[r, 12].Text?.Trim();
            var viewDescStr = ws.Cells[r, 13].Text?.Trim();
            var maxOccupantsStr = ws.Cells[r, 14].Text?.Trim();
            var descStr = ws.Cells[r, 15].Text?.Trim();

            // Nếu toàn bộ dòng trống thì bỏ qua
            if (string.IsNullOrWhiteSpace(unitNameStr) &&
                string.IsNullOrWhiteSpace(blockStr) &&
                string.IsNullOrWhiteSpace(floorStr) &&
                string.IsNullOrWhiteSpace(areaStr) &&
                string.IsNullOrWhiteSpace(priceStr))
            {
                continue;
            }

            totalDataRows++;
            var currentErrors = new List<string>();

            // 1. Validate Block & UnitName
            if (string.IsNullOrWhiteSpace(unitNameStr))
            {
                currentErrors.Add("Mã/Tên căn (cột 1) không được để trống.");
            }

            if (string.IsNullOrWhiteSpace(blockStr))
            {
                currentErrors.Add("Tòa/Block (cột 2) không được để trống.");
            }

            if (!string.IsNullOrWhiteSpace(unitNameStr) && !string.IsNullOrWhiteSpace(blockStr))
            {
                var combinedKey = $"{blockStr.Trim()}_{unitNameStr.Trim()}".ToUpperInvariant();
                if (fileKeysInProcess.Contains(combinedKey))
                {
                    currentErrors.Add($"Mã căn '{unitNameStr}' (Tòa: {blockStr}) bị trùng lặp trong chính file Excel.");
                }
                else
                {
                    fileKeysInProcess.Add(combinedKey);
                }

                if (existingKeySet.Contains(combinedKey))
                {
                    currentErrors.Add($"Mã căn '{unitNameStr}' (Tòa: {blockStr}) đã tồn tại trong dự án này trên hệ thống.");
                }
            }

            // 2. Validate Floor
            if (!int.TryParse(floorStr, out var floorNumber) || floorNumber <= 0)
            {
                currentErrors.Add($"Tầng (cột 3) '{floorStr}' không hợp lệ. Phải là số nguyên > 0.");
            }

            // 3. Validate Beds
            if (!int.TryParse(bedsStr, out var numberOfBedrooms) || numberOfBedrooms < 1)
            {
                currentErrors.Add($"Số phòng ngủ (cột 4) '{bedsStr}' không hợp lệ. Phải là số nguyên >= 1.");
            }

            // 4. Validate Baths
            if (!int.TryParse(bathsStr, out var numberOfBathrooms) || numberOfBathrooms < 1)
            {
                currentErrors.Add($"Số phòng vệ sinh (cột 5) '{bathsStr}' không hợp lệ. Phải là số nguyên >= 1.");
            }

            // 5. Validate Area
            if (!double.TryParse(areaStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var area) &&
                !double.TryParse(areaStr, out area))
            {
                currentErrors.Add($"Diện tích thông thủy (cột 6) '{areaStr}' không hợp lệ. Phải là số > 0.");
            }
            else if (area <= 0)
            {
                currentErrors.Add($"Diện tích thông thủy (cột 6) '{area}' phải lớn hơn 0 m².");
            }

            // 6. GrossArea (optional)
            double? grossArea = null;
            if (!string.IsNullOrWhiteSpace(grossAreaStr))
            {
                if (double.TryParse(grossAreaStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ga) ||
                    double.TryParse(grossAreaStr, out ga))
                {
                    if (ga > 0) grossArea = ga;
                    else currentErrors.Add($"Diện tích tim tường (cột 7) '{grossAreaStr}' phải > 0 nếu nhập.");
                }
                else
                {
                    currentErrors.Add($"Diện tích tim tường (cột 7) '{grossAreaStr}' không phải là định dạng số hợp lệ.");
                }
            }

            // 7. Validate Price
            if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price) &&
                !decimal.TryParse(priceStr, out price))
            {
                currentErrors.Add($"Giá bán (cột 8) '{priceStr}' không hợp lệ. Phải là số tiền > 0.");
            }
            else if (price <= 0)
            {
                currentErrors.Add($"Giá bán (cột 8) '{price:N0}' VNĐ phải lớn hơn 0.");
            }

            // 8. MainDoorDirection
            var normMainDoor = NormalizeEnum(mainDoorDirStr);
            if (!string.IsNullOrWhiteSpace(normMainDoor) && !DirectionConstants.IsValid(normMainDoor))
            {
                currentErrors.Add($"Hướng cửa chính (cột 9) '{mainDoorDirStr}' không hợp lệ. Giá trị cho phép: {string.Join(", ", DirectionConstants.All)}");
            }

            // 9. BalconyDirection
            var normBalcony = NormalizeEnum(balconyDirStr);
            if (!string.IsNullOrWhiteSpace(normBalcony) && !DirectionConstants.IsValid(normBalcony))
            {
                currentErrors.Add($"Hướng ban công (cột 10) '{balconyDirStr}' không hợp lệ. Giá trị cho phép: {string.Join(", ", DirectionConstants.All)}");
            }

            // 10. UnitGroup
            var normUnitGroup = NormalizeEnum(unitGroupStr) ?? UnitGroupConstants.Standard;
            if (!UnitGroupConstants.IsValid(normUnitGroup))
            {
                currentErrors.Add($"Phân nhóm căn (cột 11) '{unitGroupStr}' không hợp lệ. Giá trị cho phép: {string.Join(", ", UnitGroupConstants.All)}");
            }

            // 11. SaleType
            var normSaleType = NormalizeEnum(saleTypeStr) ?? SaleTypeConstants.FullOwnership;
            if (!SaleTypeConstants.IsValid(normSaleType))
            {
                currentErrors.Add($"Hình thức bán (cột 12) '{saleTypeStr}' không hợp lệ. Giá trị cho phép: {string.Join(", ", SaleTypeConstants.All)}");
            }

            // 12. MaxOccupants (optional)
            int? maxOccupants = null;
            if (!string.IsNullOrWhiteSpace(maxOccupantsStr))
            {
                if (int.TryParse(maxOccupantsStr, out var occ) && occ > 0)
                {
                    maxOccupants = occ;
                }
                else
                {
                    currentErrors.Add($"Số người ở tối đa (cột 14) '{maxOccupantsStr}' phải là số nguyên > 0.");
                }
            }

            if (currentErrors.Count > 0)
            {
                rowErrors.Add(new ApartmentExcelRowErrorDto
                {
                    Row = r,
                    UnitName = unitNameStr,
                    ErrorMessage = string.Join("; ", currentErrors)
                });
            }
            else
            {
                apartmentsToCreate.Add(new Apartment
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    UnitName = unitNameStr!,
                    BuildingBlock = blockStr,
                    FloorNumber = floorNumber,
                    NumberOfBedrooms = numberOfBedrooms,
                    NumberOfBathrooms = numberOfBathrooms,
                    Area = area,
                    GrossArea = grossArea,
                    Price = price,
                    MainDoorDirection = normMainDoor,
                    BalconyDirection = normBalcony,
                    UnitGroup = normUnitGroup,
                    SaleType = normSaleType,
                    ViewDescription = string.IsNullOrWhiteSpace(viewDescStr) ? null : viewDescStr,
                    MaxOccupants = maxOccupants,
                    Description = string.IsNullOrWhiteSpace(descStr) ? null : descStr,
                    Status = ApartmentStatusConstants.Available,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (totalDataRows == 0)
        {
            throw new ArgumentException("File Excel không chứa bất kỳ dòng dữ liệu căn hộ nào.");
        }

        // Nếu có bất kỳ lỗi nào, không lưu và trả về danh sách lỗi
        if (rowErrors.Count > 0)
        {
            return new ApartmentExcelImportResultDto
            {
                TotalRows = totalDataRows,
                SuccessCount = 0,
                FailedCount = rowErrors.Count,
                Message = $"File Excel có {rowErrors.Count}/{totalDataRows} dòng dữ liệu không hợp lệ. Vui lòng sửa lại theo bảng lỗi chi tiết.",
                Errors = rowErrors,
                Data = new List<ApartmentDto>()
            };
        }

        // Lưu vào DB
        _context.Apartments.AddRange(apartmentsToCreate);
        await _context.SaveChangesAsync(ct);

        // Đồng bộ thống kê dự án (AvailableUnits, MinPrice, MaxPrice, MinArea, MaxArea)
        await SyncProjectAggregatesAsync(projectId, ct);

        _logger.LogInformation("Import Excel thành công {Count} căn hộ vào dự án {ProjectId}",
            apartmentsToCreate.Count, projectId);

        // Map kết quả trả về
        var resultDtos = apartmentsToCreate.Select(MapToApartmentDto).ToList();

        return new ApartmentExcelImportResultDto
        {
            TotalRows = totalDataRows,
            SuccessCount = apartmentsToCreate.Count,
            FailedCount = 0,
            Message = $"Đã nhập thành công {apartmentsToCreate.Count} căn hộ từ file Excel vào dự án.",
            Errors = new List<ApartmentExcelRowErrorDto>(),
            Data = resultDtos
        };
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
