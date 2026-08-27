using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.Apartment;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.Milestone;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Services;

public class HousingProjectService : IHousingProjectService
{
    private readonly IHousingProjectRepository _repository;
    private readonly IFileStorageService _fileStorageService;

    public HousingProjectService(
        IHousingProjectRepository repository,
        IFileStorageService fileStorageService)
    {
        _repository = repository;
        _fileStorageService = fileStorageService;
    }

    public async Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request,
        Guid? currentUserId = null,
        string? currentUserRole = null)
    {
        // Validate request
        if (request.PageIndex < 1)
            request.PageIndex = 1;

        if (request.PageSize < 1)
            request.PageSize = 12;

        if (request.PageSize > 100)
            request.PageSize = 100;

        // Call repository to get paginated results
        return await _repository.GetHousingProjectsAsync(request, currentUserId, currentUserRole);
    }

    public async Task<HousingProjectResponseDto> CreateHousingProjectAsync(
        CreateHousingProjectRequestDto request,
        Guid? developerId = null)
    {
        // Validate request
        ValidateHousingProjectRequest(request);

        // Kiểm tra dự án trùng tên đang hoạt động (PENDING / UPCOMING / OPEN / FULL)
        var existingActive = await _repository.GetActiveProjectByNameAsync(request.ProjectName, developerId);
        if (existingActive != null)
        {
            var statusName = existingActive.HousingProjectStatus?.StatusName 
                ?? existingActive.HousingProjectStatus?.StatusCode 
                ?? "Đang hoạt động";
            throw new ArgumentException(
                $"Dự án với tên '{request.ProjectName.Trim()}' hiện đang hoạt động trên hệ thống (trạng thái: {statusName}). " +
                "Nếu bạn muốn tạo đợt mới, vui lòng đóng đợt cũ hoặc đặt tên phân biệt (ví dụ: thêm '- Đợt 2').");
        }

        // Tạo dự án luôn PENDING — CĐT/client không được chọn status (SXD mới duyệt → UPCOMING).
        var pendingStatus = await _repository.GetStatusByCodeAsync("PENDING");
        if (pendingStatus == null)
        {
            throw new InvalidOperationException("Không tìm thấy trạng thái PENDING trên hệ thống.");
        }
        var statusId = pendingStatus.Id;

        // Create entity
        var housingProject = new HousingProject
        {
            Id = Guid.NewGuid(),
            ProjectName = request.ProjectName.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Province = request.Province.Trim(),
            District = request.District.Trim(),
            Street = request.Street.Trim(),
            Ward = request.Ward.Trim(),
            LotteryDate = null,
            LotteryLocation = null,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            MinArea = request.MinArea,
            MaxArea = request.MaxArea,
            AvailableUnits = request.AvailableUnits,
            ThumbnailUrl = request.ThumbnailUrl?.Trim(),
            HousingProjectStatusId = statusId,
            IsDeleted = false,
            
            DecisionNumber = request.DecisionNumber.Trim(),
            ApprovalDate = null,
            ApplicationOpenDate = request.ApplicationOpenDate,
            ApplicationCloseDate = request.ApplicationCloseDate,
            PublicAnnounceAt = null,
            DeveloperId = developerId
        };

        // Add Project Images from URL list
        if (request.Images != null && request.Images.Count > 0)
        {
            var order = 1;
            foreach (var url in request.Images.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                housingProject.ProjectImages.Add(new ProjectImage
                {
                    Id = Guid.NewGuid(),
                    ProjectId = housingProject.Id,
                    ImageUrl = url.Trim(),
                    DisplayOrder = order++,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Cấu hình 3 đến 6 đợt đóng tiền (nếu CĐT gửi lên) hoặc Seed mặc định 5 đợt chuẩn theo tiến độ thi công NOXH
        if (request.Milestones != null && request.Milestones.Count > 0)
        {
            ValidateAndBuildPaymentMilestones(housingProject.Id, request.Milestones, housingProject.PaymentMilestones);
        }
        else
        {
            // Seed mặc định 5 đợt chuẩn (Đợt 1: 20%, Đợt 2: 20%, Đợt 3: 20%, Đợt 4: 35%, Đợt 5: 5%)
            AddDefaultPercentMilestones(housingProject, 20m);
        }

        // Save to repository
        try
        {
            await _repository.CreateAsync(housingProject);
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException?.Message.Contains("FK_") == true ||
                ex.InnerException?.Message.Contains("FOREIGN KEY") == true)
            {
                throw new InvalidOperationException(
                    "Không thể lưu dự án do lỗi khóa ngoại dữ liệu.", ex);
            }
            throw;
        }

        // Load from database to include status
        var createdProject = await _repository.GetByIdAsync(housingProject.Id);
        if (createdProject == null)
        {
            throw new InvalidOperationException($"Failed to retrieve created housing project with ID {housingProject.Id}.");
        }

        // Return mapped response
        return MapToResponseDto(createdProject);
    }

    public async Task<HousingProjectResponseDto> UpdateHousingProjectAsync(
        Guid id,
        UpdateHousingProjectRequestDto request,
        Guid? claimDeveloperId = null)
    {
        // Validate request
        ValidateHousingProjectRequest(request);

        // Check if project exists
        var existingProject = await _repository.GetByIdAsync(id);
        if (existingProject == null)
        {
            throw new InvalidOperationException($"Housing project with ID {id} not found.");
        }

        // Chỉ cho phép chỉnh sửa khi dự án ở trạng thái PENDING
        var currentStatusCode = existingProject.HousingProjectStatus?.StatusCode?.Trim().ToUpperInvariant();
        if (currentStatusCode != "PENDING")
        {
            throw new ArgumentException(
                $"Không thể chỉnh sửa dự án. Dự án chỉ có thể được chỉnh sửa khi đang ở trạng thái Chờ duyệt (PENDING). Trạng thái hiện tại: {existingProject.HousingProjectStatus?.StatusName ?? currentStatusCode ?? "Không xác định"}.");
        }

        // Self-heal: dự án cũ thiếu DeveloperId → gắn CĐT đang sửa (nếu đang login role CĐT)
        if (!existingProject.DeveloperId.HasValue
            && claimDeveloperId.HasValue
            && claimDeveloperId.Value != Guid.Empty)
        {
            existingProject.DeveloperId = claimDeveloperId;
        }

        // Kiểm tra trùng tên với dự án khác đang hoạt động
        var effectiveDevId = existingProject.DeveloperId ?? claimDeveloperId;
        var existingActive = await _repository.GetActiveProjectByNameAsync(request.ProjectName, effectiveDevId, existingProject.Id);
        if (existingActive != null)
        {
            throw new ArgumentException(
                $"Tên dự án '{request.ProjectName.Trim()}' đã trùng với một dự án khác đang hoạt động trên hệ thống.");
        }

        // Update entity
        existingProject.ProjectName = request.ProjectName.Trim();
        existingProject.Description = request.Description?.Trim() ?? string.Empty;
        existingProject.Province = request.Province.Trim();
        existingProject.District = request.District.Trim();
        existingProject.Street = request.Street.Trim();
        existingProject.Ward = request.Ward.Trim();
        existingProject.MinPrice = request.MinPrice;
        existingProject.MaxPrice = request.MaxPrice;
        existingProject.MinArea = request.MinArea;
        existingProject.MaxArea = request.MaxArea;
        existingProject.AvailableUnits = request.AvailableUnits;
        existingProject.ThumbnailUrl = request.ThumbnailUrl?.Trim();
        existingProject.UpdatedAt = DateTime.UtcNow;
        // Giữ nguyên trạng thái PENDING, không thay đổi trạng thái qua API PUT

        // Update legal fields
        existingProject.DecisionNumber = request.DecisionNumber.Trim();
        existingProject.ApplicationOpenDate = request.ApplicationOpenDate;
        existingProject.ApplicationCloseDate = request.ApplicationCloseDate;

        // Update images
        if (request.Images != null)
        {
            existingProject.ProjectImages.Clear();
            var order = 1;
            foreach (var url in request.Images.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                existingProject.ProjectImages.Add(new ProjectImage
                {
                    Id = Guid.NewGuid(),
                    ProjectId = existingProject.Id,
                    ImageUrl = url.Trim(),
                    DisplayOrder = order++,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Sync PaymentMilestones (nếu client gửi danh sách đợt)
        if (request.Milestones != null && request.Milestones.Count > 0)
        {
            ValidateAndBuildPaymentMilestones(existingProject.Id, request.Milestones, existingProject.PaymentMilestones);
        }

        // Save to repository
        try
        {
            await _repository.UpdateAsync(existingProject);
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException?.Message.Contains("FK_") == true ||
                ex.InnerException?.Message.Contains("FOREIGN KEY") == true)
            {
                throw new InvalidOperationException("Không thể cập nhật dự án do lỗi khóa ngoại dữ liệu.", ex);
            }
            throw;
        }

        // Load from database to include latest status
        var updatedProject = await _repository.GetByIdAsync(id);
        if (updatedProject == null)
        {
            throw new InvalidOperationException($"Failed to retrieve updated housing project with ID {id}.");
        }

        // Return mapped response
        return MapToResponseDto(updatedProject);
    }

    public async Task DeleteHousingProjectAsync(Guid id)
    {
        // Check if project exists
        var existingProject = await _repository.GetByIdAsync(id);
        if (existingProject == null)
        {
            throw new InvalidOperationException($"Housing project with ID {id} not found.");
        }

        // Soft delete
        await _repository.SoftDeleteAsync(existingProject);
    }

    public async Task<HousingProjectResponseDto> GetHousingProjectByIdAsync(Guid id)
    {
        // Get project
        var project = await _repository.GetByIdAsync(id);
        if (project == null)
        {
            throw new InvalidOperationException($"Housing project with ID {id} not found.");
        }

        // Return mapped response
        return MapToResponseDto(project);
    }

    private static void ValidateHousingProjectRequest(dynamic request)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectName))
            throw new ArgumentException("Tên dự án là bắt buộc (ProjectName).");

        if (string.IsNullOrWhiteSpace(request.Province))
            throw new ArgumentException("Tỉnh/Thành phố là bắt buộc (Province).");

        if (string.IsNullOrWhiteSpace(request.District))
            throw new ArgumentException("Quận/Huyện là bắt buộc (District).");

        if (string.IsNullOrWhiteSpace(request.Ward))
            throw new ArgumentException("Phường/Xã là bắt buộc (Ward).");

        if (string.IsNullOrWhiteSpace(request.Street))
            throw new ArgumentException("Đường/Phố là bắt buộc (Street).");

        if (string.IsNullOrWhiteSpace(request.DecisionNumber))
            throw new ArgumentException("Số quyết định phê duyệt là bắt buộc (DecisionNumber).");

        if (request.MinPrice < 0)
            throw new ArgumentException("Giá bán tối thiểu không được âm.");

        if (request.MaxPrice < request.MinPrice)
            throw new ArgumentException("Giá bán tối đa phải lớn hơn hoặc bằng giá tối thiểu.");

        if (request.MinArea < 0)
            throw new ArgumentException("Diện tích tối thiểu không được âm.");

        if (request.MaxArea < request.MinArea)
            throw new ArgumentException("Diện tích tối đa phải lớn hơn hoặc bằng diện tích tối thiểu.");

        if (request.AvailableUnits < 0)
            throw new ArgumentException("Số lượng căn hộ không được âm.");

        if (request.ApplicationOpenDate != null && request.ApplicationCloseDate != null)
        {
            if (request.ApplicationOpenDate >= request.ApplicationCloseDate)
            {
                throw new ArgumentException("Thời gian mở nhận hồ sơ phải diễn ra trước thời gian đóng nhận hồ sơ.");
            }
        }
    }

    private static void ValidateAndBuildPaymentMilestones(
        Guid projectId,
        List<RHS.Application.DTOs.Milestone.MilestoneSetupItemDto> milestoneDtos,
        ICollection<PaymentMilestone> targetCollection)
    {
        if (milestoneDtos.Count < 3 || milestoneDtos.Count > 6)
        {
            throw new ArgumentException($"Dự án NOXH phải có từ 3 đến 6 đợt đóng tiền (Hiện tại có {milestoneDtos.Count} đợt).");
        }

        var sortedItems = milestoneDtos.OrderBy(m => m.PhaseOrder).ToList();

        for (int i = 0; i < sortedItems.Count; i++)
        {
            var expectedOrder = i + 1;
            if (sortedItems[i].PhaseOrder != expectedOrder)
            {
                throw new ArgumentException($"Thứ tự các đợt thanh toán phải liên tục từ 1 đến {sortedItems.Count} (Đợt thứ {i + 1} đang có PhaseOrder = {sortedItems[i].PhaseOrder}).");
            }
        }

        decimal totalPercentage = 0m;
        var now = DateTime.UtcNow;

        foreach (var item in sortedItems)
        {
            if (string.IsNullOrWhiteSpace(item.PhaseName))
                throw new ArgumentException($"Tên đợt thanh toán thứ {item.PhaseOrder} không được để trống.");

            if (!CalculationTypeConstants.IsValid(item.CalculationType))
                throw new ArgumentException($"Hình thức tính tiền của Đợt {item.PhaseOrder} '{item.CalculationType}' không hợp lệ.");

            if (!TriggerEventConstants.IsValid(item.TriggerEvent))
                throw new ArgumentException($"Sự kiện kích hoạt của Đợt {item.PhaseOrder} '{item.TriggerEvent}' không hợp lệ.");

            if (item.DueDays <= 0)
                throw new ArgumentException($"Thời hạn thanh toán của Đợt {item.PhaseOrder} phải lớn hơn 0 ngày.");

            if (!item.Percentage.HasValue || item.Percentage.Value <= 0 || item.Percentage.Value > 100)
                throw new ArgumentException($"Tỷ lệ phần trăm Đợt {item.PhaseOrder} ({item.Percentage}%) không hợp lệ. Phải lớn hơn 0% và không quá 100%.");

            totalPercentage += item.Percentage.Value;
        }

        if (Math.Abs(totalPercentage - 100.0m) > 0.001m)
        {
            throw new ArgumentException($"Tổng tỷ lệ phần trăm thanh toán của các đợt phải chính xác bằng 100% (Hiện tại tổng = {totalPercentage:F2}%).");
        }

        var p1 = sortedItems[0].Percentage.GetValueOrDefault();
        if (p1 > 30.0m)
        {
            throw new ArgumentException($"Tỷ lệ thanh toán Đợt 1 ({p1}%) vượt quá mức trần quy định cho NOXH (tối đa 30% giá trị hợp đồng).");
        }

        targetCollection.Clear();
        foreach (var item in sortedItems)
        {
            targetCollection.Add(new PaymentMilestone
            {
                Id              = Guid.NewGuid(),
                ProjectId       = projectId,
                PhaseOrder      = item.PhaseOrder,
                PhaseName       = item.PhaseName.Trim(),
                CalculationType = item.CalculationType.Trim().ToUpperInvariant(),
                FixedAmount     = item.FixedAmount,
                Percentage      = item.Percentage,
                TriggerEvent    = item.TriggerEvent.Trim().ToUpperInvariant(),
                DueDays         = item.DueDays,
                Description     = item.Description?.Trim(),
                IsActive        = true,
                CreatedAt       = now
            });
        }
    }

    private HousingProjectResponseDto MapToResponseDto(HousingProject project)
    {
        return new HousingProjectResponseDto
        {
            Id = project.Id,
            ProjectName = project.ProjectName,
            Description = project.Description,
            Province = project.Province,
            District = project.District,
            Street = project.Street,
            Ward = project.Ward,
            LotteryDate = project.LotteryDate,
            LotteryLocation = project.LotteryLocation,
            MinPrice = project.MinPrice,
            MaxPrice = project.MaxPrice,
            MinArea = project.MinArea,
            MaxArea = project.MaxArea,
            AvailableUnits = project.AvailableUnits,
            ThumbnailUrl = project.ThumbnailUrl,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            Status = project.HousingProjectStatus?.StatusName,
            DecisionNumber = project.DecisionNumber,
            ApprovalDate = project.ApprovalDate,
            ApplicationOpenDate = project.ApplicationOpenDate,
            ApplicationCloseDate = project.ApplicationCloseDate,
            RejectReason = project.RejectReason,
            PublicAnnounceAt = project.PublicAnnounceAt,
            Images = project.ProjectImages
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new ProjectImageResponseDto
                {
                    Id = x.Id,
                    ImageUrl = x.ImageUrl,
                    DisplayOrder = x.DisplayOrder
                })
                .ToList(),
            Apartments = project.Apartments
                .OrderBy(a => a.UnitName)
                .Select(a => new ApartmentDto
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
                    ApartmentTypeId        = a.ApartmentTypeId,
                    ApartmentType          = a.ApartmentType?.TypeCode ?? string.Empty,
                    ApartmentTypeLabel     = a.ApartmentType?.TypeName ?? string.Empty,
                    Description            = a.Description,
                    Model3DUrl             = a.Model3DUrl,
                    VirtualTourUrl         = a.VirtualTourUrl,
                    CreatedAt              = a.CreatedAt,
                    UpdatedAt              = a.UpdatedAt
                })
                .ToList(),
            Milestones = project.PaymentMilestones
                .OrderBy(m => m.PhaseOrder)
                .Select(m => new MilestoneDto
                {
                    Id                = m.Id,
                    ProjectId         = m.ProjectId,
                    PhaseOrder        = m.PhaseOrder,
                    PhaseName         = m.PhaseName,
                    CalculationType   = m.CalculationType,
                    FixedAmount       = m.FixedAmount,
                    Percentage        = m.Percentage,
                    TriggerEvent      = m.TriggerEvent,
                    TriggerEventLabel = TriggerEventConstants.GetDisplayName(m.TriggerEvent),
                    DueDays           = m.DueDays,
                    Description       = m.Description,
                    IsActive          = m.IsActive,
                    CreatedAt         = m.CreatedAt,
                    UpdatedAt         = m.UpdatedAt
                })
                .ToList()
        };
    }

    private static void AddDefaultPercentMilestones(HousingProject project, decimal phase1Pct = 20m)
    {
        var p1 = Math.Clamp(phase1Pct, 10m, 30m);
        var now = DateTime.UtcNow;

        // Seed 5 đợt chuẩn theo tiến độ thi công NOXH:
        // Đợt 1 (Cọc/Ký HĐ): 20%, Đợt 2 (Xây thô): 20%, Đợt 3 (Cất nóc): 20%, Đợt 4 (Bàn giao): 35%, Đợt 5 (Sổ hồng): 5%
        project.PaymentMilestones.Add(new PaymentMilestone
        {
            Id              = Guid.NewGuid(),
            ProjectId       = project.Id,
            PhaseOrder      = 1,
            PhaseName       = "Đợt 1 — Đặt cọc & Ký Hợp đồng",
            CalculationType = CalculationTypeConstants.Percentage,
            Percentage      = p1,
            TriggerEvent    = TriggerEventConstants.OnContractSigned,
            DueDays         = 15,
            Description     = $"Đợt 1 — {p1:0.##}% giá trị căn hộ khi ký Hợp đồng mua bán chính thức",
            IsActive        = true,
            CreatedAt       = now
        });

        project.PaymentMilestones.Add(new PaymentMilestone
        {
            Id              = Guid.NewGuid(),
            ProjectId       = project.Id,
            PhaseOrder      = 2,
            PhaseName       = "Đợt 2 — Hoàn thành sàn thô",
            CalculationType = CalculationTypeConstants.Percentage,
            Percentage      = 20m,
            TriggerEvent    = TriggerEventConstants.ConstructionRoughFloor,
            DueDays         = 30,
            Description     = "Đợt 2 — 20% giá trị căn hộ khi hoàn thành phần khung bê tông cốt thép",
            IsActive        = true,
            CreatedAt       = now
        });

        project.PaymentMilestones.Add(new PaymentMilestone
        {
            Id              = Guid.NewGuid(),
            ProjectId       = project.Id,
            PhaseOrder      = 3,
            PhaseName       = "Đợt 3 — Cất nóc tòa nhà",
            CalculationType = CalculationTypeConstants.Percentage,
            Percentage      = 20m,
            TriggerEvent    = TriggerEventConstants.RoofingCompleted,
            DueDays         = 30,
            Description     = "Đợt 3 — 20% giá trị căn hộ khi hoàn thành cất nóc toàn bộ công trình",
            IsActive        = true,
            CreatedAt       = now
        });

        var p4 = 100m - (p1 + 20m + 20m + 5m); // 35% nếu p1=20
        project.PaymentMilestones.Add(new PaymentMilestone
        {
            Id              = Guid.NewGuid(),
            ProjectId       = project.Id,
            PhaseOrder      = 4,
            PhaseName       = "Đợt 4 — Bàn giao nhà & Chìa khóa",
            CalculationType = CalculationTypeConstants.Percentage,
            Percentage      = p4,
            TriggerEvent    = TriggerEventConstants.Handover,
            DueDays         = 30,
            Description     = $"Đợt 4 — {p4:0.##}% giá trị căn hộ khi nhận bàn giao thực tế và chìa khóa nhà",
            IsActive        = true,
            CreatedAt       = now
        });

        project.PaymentMilestones.Add(new PaymentMilestone
        {
            Id              = Guid.NewGuid(),
            ProjectId       = project.Id,
            PhaseOrder      = 5,
            PhaseName       = "Đợt 5 — Nhận Giấy chứng nhận (Sổ hồng)",
            CalculationType = CalculationTypeConstants.Percentage,
            Percentage      = 5m,
            TriggerEvent    = TriggerEventConstants.RedBookIssued,
            DueDays         = 30,
            Description     = "Đợt 5 — 5% giá trị còn lại khi cơ quan nhà nước bàn giao Giấy chứng nhận quyền sở hữu",
            IsActive        = true,
            CreatedAt       = now
        });
    }

    /// <summary>Đồng bộ AvailableUnits / khoảng giá-diện tích từ danh sách căn.</summary>
    private static void ApplyApartmentAggregates(
        HousingProject project,
        IEnumerable<Domain.Entities.Apartment> apartments)
    {
        var list = apartments.ToList();
        if (list.Count == 0) return;

        project.AvailableUnits = list.Count(a =>
            string.Equals(a.Status, ApartmentStatusConstants.Available, StringComparison.OrdinalIgnoreCase));
        project.MinArea = list.Min(a => a.Area);
        project.MaxArea = list.Max(a => a.Area);
        project.MinPrice = list.Min(a => a.Price);
        project.MaxPrice = list.Max(a => a.Price);
    }

    public async Task<HousingProjectResponseDto> UpdateProjectStatusAsync(Guid id, string action, string? rejectReason)
    {
        var project = await _repository.GetByIdAsync(id);
        if (project == null)
        {
            throw new InvalidOperationException($"Housing project with ID {id} not found.");
        }

        // Must be PENDING to approve/reject
        if (!string.Equals(project.HousingProjectStatus?.StatusCode, "PENDING", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Chỉ dự án có trạng thái PENDING mới có thể phê duyệt hoặc từ chối.");
        }

        if (action.Equals("APPROVE", StringComparison.OrdinalIgnoreCase))
        {
            var upcomingStatus = await _repository.GetStatusByCodeAsync("UPCOMING");
            if (upcomingStatus == null)
            {
                throw new InvalidOperationException("Không tìm thấy trạng thái UPCOMING trên hệ thống.");
            }
            project.HousingProjectStatusId = upcomingStatus.Id;
            project.HousingProjectStatus = upcomingStatus;
            project.RejectReason = null;
            project.ApprovalDate = DateTime.UtcNow;
            project.PublicAnnounceAt ??= DateTime.UtcNow;
        }
        else if (action.Equals("REJECT", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(rejectReason))
            {
                throw new ArgumentException("Lý do từ chối (RejectReason) là bắt buộc khi từ chối dự án.");
            }
            var rejectedStatus = await _repository.GetStatusByCodeAsync("REJECTED");
            if (rejectedStatus == null)
            {
                throw new InvalidOperationException("Không tìm thấy trạng thái REJECTED trên hệ thống.");
            }
            project.HousingProjectStatusId = rejectedStatus.Id;
            project.HousingProjectStatus = rejectedStatus;
            project.RejectReason = rejectReason.Trim();
            project.ApprovalDate = DateTime.UtcNow;
        }
        else
        {
            throw new ArgumentException("Hành động không hợp lệ. Chỉ chấp nhận APPROVE hoặc REJECT.");
        }

        await _repository.UpdateStatusOnlyAsync(project);
        
        var updatedProject = await _repository.GetByIdAsync(project.Id);
        return MapToResponseDto(updatedProject ?? project);
    }

    private static readonly HashSet<string> LifecycleStatusCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "UPCOMING",
        "OPEN",
        "CLOSED",
        "FULL",
    };

    public async Task<HousingProjectResponseDto> ChangeLifecycleStatusAsync(
        Guid id,
        string statusCode,
        string? note = null)
    {
        if (string.IsNullOrWhiteSpace(statusCode))
        {
            throw new ArgumentException("StatusCode là bắt buộc.");
        }

        var normalized = statusCode.Trim().ToUpperInvariant();
        if (!LifecycleStatusCodes.Contains(normalized))
        {
            throw new ArgumentException(
                "StatusCode không hợp lệ. Chỉ chấp nhận: UPCOMING, OPEN, CLOSED, FULL.");
        }

        var project = await _repository.GetByIdAsync(id);
        if (project == null || project.IsDeleted)
        {
            throw new InvalidOperationException($"Housing project with ID {id} not found.");
        }

        var currentCode = project.HousingProjectStatus?.StatusCode?.ToUpperInvariant();
        if (currentCode is "PENDING" or "REJECTED")
        {
            throw new ArgumentException(
                $"Không thể đổi nhanh từ trạng thái {currentCode}. " +
                "Dự án PENDING cần SXD APPROVE/REJECT; dự án REJECTED không mở lại bằng API này.");
        }

        if (string.Equals(currentCode, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return MapToResponseDto(project);
        }

        var targetStatus = await _repository.GetStatusByCodeAsync(normalized);
        if (targetStatus == null)
        {
            throw new InvalidOperationException($"Không tìm thấy trạng thái {normalized} trên hệ thống.");
        }

        var now = DateTime.UtcNow;
        project.HousingProjectStatusId = targetStatus.Id;
        project.HousingProjectStatus = targetStatus;
        project.UpdatedAt = now;

        if (normalized == "OPEN")
        {
            project.PublicAnnounceAt ??= now;
            // Demo/vận hành: nếu chưa có ngày mở ĐK thì ghi nhận thời điểm mở
            project.ApplicationOpenDate ??= now;
        }
        else if (normalized == "CLOSED" || normalized == "FULL")
        {
            // Ghi nhận đã qua thời điểm đóng nếu chưa có
            project.ApplicationCloseDate ??= now;
        }

        // Note hiện chưa có cột riêng — giữ tham số để mở rộng audit sau
        _ = note;

        await _repository.UpdateStatusOnlyAsync(project);

        var updated = await _repository.GetByIdAsync(project.Id);
        return MapToResponseDto(updated ?? project);
    }
}
