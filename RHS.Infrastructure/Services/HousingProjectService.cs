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
        // Nếu dự án cũ đã CLOSED, REJECTED hoặc bị xóa thì vẫn cho phép tạo mới
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

        // Upload Thumbnail if provided
        var thumbnailUrl = request.ThumbnailUrl;
        if (request.ThumbnailFile != null)
        {
            thumbnailUrl = await _fileStorageService.UploadImageAsync(request.ThumbnailFile, "housing-projects");
        }

        // Create entity
        var housingProject = new HousingProject
        {
            Id = Guid.NewGuid(),
            ProjectName = request.ProjectName,
            Description = request.Description,
            Province = request.Province,
            District = request.District,
            Street = request.Street,
            Ward = request.Ward,
            LotteryDate = request.LotteryDate,
            LotteryLocation = request.LotteryLocation,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            MinArea = request.MinArea,
            MaxArea = request.MaxArea,
            AvailableUnits = request.AvailableUnits,
            ThumbnailUrl = thumbnailUrl,
            HousingProjectStatusId = statusId,
            IsDeleted = false,
            
            // New legal & developer fields — duyệt/công bố chỉ SXD set khi APPROVE
            DecisionNumber = request.DecisionNumber,
            ApprovalDate = null,
            ApplicationOpenDate = request.ApplicationOpenDate,
            ApplicationCloseDate = request.ApplicationCloseDate,
            PublicAnnounceAt = null,
            DeveloperId = developerId
        };

        // Upload/Process multiple images
        var imageUrls = new List<string>();
        if (request.ImageFiles != null && request.ImageFiles.Count > 0)
        {
            foreach (var file in request.ImageFiles)
            {
                var uploadedUrl = await _fileStorageService.UploadImageAsync(file, "housing-projects");
                imageUrls.Add(uploadedUrl);
            }
        }
        else if (request.Images != null)
        {
            imageUrls.AddRange(request.Images);
        }

        var order = 1;
        foreach (var url in imageUrls)
        {
            housingProject.ProjectImages.Add(new ProjectImage
            {
                Id = Guid.NewGuid(),
                ProjectId = housingProject.Id,
                ImageUrl = url,
                DisplayOrder = order++,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Add individual apartments if provided
        if (request.Apartments != null && request.Apartments.Count > 0)
        {
            foreach (var apt in request.Apartments)
            {
                housingProject.Apartments.Add(new Domain.Entities.Apartment
                {
                    Id          = Guid.NewGuid(),
                    ProjectId   = housingProject.Id,
                    UnitName    = apt.UnitName,
                    Area        = apt.Area,
                    Price       = apt.Price,
                    Status      = ApartmentStatusConstants.Available,
                    Description = apt.Description,
                    Model3DUrl  = apt.Model3DUrl,
                    VirtualTourUrl = apt.VirtualTourUrl,
                    CreatedAt   = DateTime.UtcNow
                });
            }

            ApplyApartmentAggregates(housingProject, housingProject.Apartments);
        }

        // Add PaymentMilestones if provided; otherwise seed mặc định 3 đợt
        if (request.Milestones != null && request.Milestones.Count > 0)
        {
            foreach (var ms in request.Milestones)
            {
                if (!CalculationTypeConstants.IsValid(ms.CalculationType))
                    throw new ArgumentException($"CalculationType không hợp lệ: {ms.CalculationType}");
                if (!TriggerEventConstants.IsValid(ms.TriggerEvent))
                    throw new ArgumentException($"TriggerEvent không hợp lệ: {ms.TriggerEvent}");

                housingProject.PaymentMilestones.Add(new PaymentMilestone
                {
                    Id              = Guid.NewGuid(),
                    ProjectId       = housingProject.Id,
                    PhaseOrder      = ms.PhaseOrder,
                    PhaseName       = ms.PhaseName,
                    CalculationType = ms.CalculationType,
                    FixedAmount     = ms.FixedAmount,
                    Percentage      = ms.Percentage,
                    TriggerEvent    = ms.TriggerEvent,
                    DueDays         = ms.DueDays,
                    Description     = ms.Description,
                    IsActive        = true,
                    CreatedAt       = DateTime.UtcNow
                });
            }
        }
        else
        {
            // Seed mặc định 2 đợt (20% và 80%)
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
                    $"Housing project status with ID {request.HousingProjectStatusId} does not exist.", ex);
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

        // Upload Thumbnail if provided
        var thumbnailUrl = request.ThumbnailUrl;
        if (request.ThumbnailFile != null)
        {
            thumbnailUrl = await _fileStorageService.UploadImageAsync(request.ThumbnailFile, "housing-projects");
        }

        // Update entity
        existingProject.ProjectName = request.ProjectName;
        existingProject.Description = request.Description;
        existingProject.Province = request.Province;
        existingProject.District = request.District;
        existingProject.Street = request.Street;
        existingProject.Ward = request.Ward;
        existingProject.LotteryDate = request.LotteryDate;
        existingProject.LotteryLocation = request.LotteryLocation;
        existingProject.MinPrice = request.MinPrice;
        existingProject.MaxPrice = request.MaxPrice;
        existingProject.MinArea = request.MinArea;
        existingProject.MaxArea = request.MaxArea;
        existingProject.AvailableUnits = request.AvailableUnits;
        existingProject.ThumbnailUrl = thumbnailUrl;
        // Giữ nguyên trạng thái PENDING, không thay đổi trạng thái qua API PUT

        // Update legal fields
        existingProject.DecisionNumber = request.DecisionNumber;
        existingProject.ApprovalDate = request.ApprovalDate;
        existingProject.ApplicationOpenDate = request.ApplicationOpenDate;
        existingProject.ApplicationCloseDate = request.ApplicationCloseDate;

        // Update images
        existingProject.ProjectImages.Clear();
        var imageUrls = new List<string>();
        if (request.ImageFiles != null && request.ImageFiles.Count > 0)
        {
            foreach (var file in request.ImageFiles)
            {
                var uploadedUrl = await _fileStorageService.UploadImageAsync(file, "housing-projects");
                imageUrls.Add(uploadedUrl);
            }
        }
        else if (request.Images != null)
        {
            imageUrls.AddRange(request.Images);
        }

        var order = 1;
        foreach (var url in imageUrls)
        {
            existingProject.ProjectImages.Add(new ProjectImage
            {
                Id = Guid.NewGuid(),
                ProjectId = existingProject.Id,
                ImageUrl = url,
                DisplayOrder = order++,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Sync apartments: keep ASSIGNED; replace AVAILABLE with request list
        if (request.Apartments != null)
        {
            var keptAssigned = existingProject.Apartments
                .Where(a => a.Status == ApartmentStatusConstants.Assigned)
                .ToList();

            existingProject.Apartments = keptAssigned;
            foreach (var apt in request.Apartments)
            {
                existingProject.Apartments.Add(new Domain.Entities.Apartment
                {
                    Id          = Guid.NewGuid(),
                    ProjectId   = existingProject.Id,
                    UnitName    = apt.UnitName,
                    Area        = apt.Area,
                    Price       = apt.Price,
                    Status      = ApartmentStatusConstants.Available,
                    Description = apt.Description,
                    Model3DUrl  = apt.Model3DUrl,
                    VirtualTourUrl = apt.VirtualTourUrl,
                    CreatedAt   = DateTime.UtcNow
                });
            }

            ApplyApartmentAggregates(existingProject, existingProject.Apartments);
        }

        // Sync PaymentMilestones (replace all nếu client gửi; không thì giữ nguyên / seed nếu chưa có)
        if (request.Milestones != null)
        {
            existingProject.PaymentMilestones.Clear();
            foreach (var ms in request.Milestones)
            {
                if (!CalculationTypeConstants.IsValid(ms.CalculationType))
                    throw new ArgumentException($"CalculationType không hợp lệ: {ms.CalculationType}");
                if (!TriggerEventConstants.IsValid(ms.TriggerEvent))
                    throw new ArgumentException($"TriggerEvent không hợp lệ: {ms.TriggerEvent}");

                existingProject.PaymentMilestones.Add(new PaymentMilestone
                {
                    Id              = Guid.NewGuid(),
                    ProjectId       = existingProject.Id,
                    PhaseOrder      = ms.PhaseOrder,
                    PhaseName       = ms.PhaseName,
                    CalculationType = ms.CalculationType,
                    FixedAmount     = ms.FixedAmount,
                    Percentage      = ms.Percentage,
                    TriggerEvent    = ms.TriggerEvent,
                    DueDays         = ms.DueDays,
                    Description     = ms.Description,
                    IsActive        = true,
                    CreatedAt       = DateTime.UtcNow
                });
            }
        }
        else if (!existingProject.PaymentMilestones.Any(m => m.IsActive))
        {
            AddDefaultPercentMilestones(existingProject, 20m);
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
                throw new InvalidOperationException(
                    $"Housing project status with ID {request.HousingProjectStatusId} does not exist.", ex);
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

    private void ValidateHousingProjectRequest(dynamic request)
    {
        // ProjectName is required
        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new ArgumentException("ProjectName is required.");
        }

        // Province is required
        if (string.IsNullOrWhiteSpace(request.Province))
        {
            throw new ArgumentException("Province is required.");
        }

        // District is required
        if (string.IsNullOrWhiteSpace(request.District))
        {
            throw new ArgumentException("District is required.");
        }

        // Street is required
        if (string.IsNullOrWhiteSpace(request.Street))
        {
            throw new ArgumentException("Street is required.");
        }

        // Ward is required
        if (string.IsNullOrWhiteSpace(request.Ward))
        {
            throw new ArgumentException("Ward is required.");
        }

        // MinPrice >= 0
        if (request.MinPrice < 0)
        {
            throw new ArgumentException("MinPrice must be greater than or equal to 0.");
        }

        // MaxPrice >= MinPrice
        if (request.MaxPrice < request.MinPrice)
        {
            throw new ArgumentException("MaxPrice must be greater than or equal to MinPrice.");
        }

        var hasApartments = request.Apartments != null && request.Apartments.Count > 0;

        // Khi có danh sách căn: Min/Max area + AvailableUnits được suy ra từ căn — bỏ qua validate cứng.
        if (!hasApartments)
        {
            if (request.MinArea <= 0)
                throw new ArgumentException("MinArea must be greater than 0.");

            if (request.MaxArea < request.MinArea)
                throw new ArgumentException("MaxArea must be greater than or equal to MinArea.");
        }
        else
        {
            foreach (var apt in request.Apartments!)
            {
                if (string.IsNullOrWhiteSpace(apt.UnitName))
                    throw new ArgumentException("Mỗi căn phải có tên (UnitName).");
                if (apt.Area <= 0)
                    throw new ArgumentException($"Căn '{apt.UnitName}': diện tích phải > 0.");
                if (apt.Price <= 0)
                    throw new ArgumentException($"Căn '{apt.UnitName}': giá phải > 0.");
            }
        }

        // AvailableUnits >= 0
        if (request.AvailableUnits < 0)
        {
            throw new ArgumentException("AvailableUnits must be greater than or equal to 0.");
        }

        // DecisionNumber cannot be blank
        if (string.IsNullOrWhiteSpace(request.DecisionNumber))
        {
            throw new ArgumentException("DecisionNumber is required and cannot be blank.");
        }

        // ApplicationOpenDate must be less than ApplicationCloseDate
        if (request.ApplicationOpenDate != null && request.ApplicationCloseDate != null)
        {
            if (request.ApplicationOpenDate >= request.ApplicationCloseDate)
            {
                throw new ArgumentException("ApplicationOpenDate must be earlier than ApplicationCloseDate.");
            }
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
                    Id          = a.Id,
                    UnitName    = a.UnitName,
                    Area        = a.Area,
                    Price       = a.Price,
                    Status      = a.Status,
                    Description = a.Description,
                    Model3DUrl  = a.Model3DUrl,
                    VirtualTourUrl = a.VirtualTourUrl
                })
                .ToList(),
            Milestones = project.PaymentMilestones
                .OrderBy(m => m.PhaseOrder)
                .Select(m => new MilestoneDto
                {
                    Id              = m.Id,
                    PhaseOrder      = m.PhaseOrder,
                    PhaseName       = m.PhaseName,
                    CalculationType = m.CalculationType,
                    FixedAmount     = m.FixedAmount,
                    Percentage      = m.Percentage,
                    TriggerEvent    = m.TriggerEvent,
                    DueDays         = m.DueDays,
                    Description     = m.Description,
                    IsActive        = m.IsActive
                })
                .ToList()
        };
    }

    private static void AddDefaultPercentMilestones(HousingProject project, decimal phase1Pct = 20m)
    {
        var p1 = Math.Clamp(phase1Pct, 1m, 30m);
        var p2 = 100m - p1;
        var now = DateTime.UtcNow;
        project.PaymentMilestones.Add(new PaymentMilestone
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            PhaseOrder = 1,
            PhaseName = "Đợt 1",
            CalculationType = CalculationTypeConstants.Percentage,
            Percentage = p1,
            TriggerEvent = TriggerEventConstants.OnContractSigned,
            DueDays = 7,
            Description = $"Đợt 1 — {p1:0.##}% giá căn sau khi ký hợp đồng mua bán nhà ở xã hội (≤ 30%)",
            IsActive = true,
            CreatedAt = now
        });
        project.PaymentMilestones.Add(new PaymentMilestone
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            PhaseOrder = 2,
            PhaseName = "Đợt 2",
            CalculationType = CalculationTypeConstants.Percentage,
            Percentage = p2,
            TriggerEvent = TriggerEventConstants.OnLotteryWon,
            DueDays = 30,
            Description = $"Đợt 2 — phần còn lại ({p2:0.##}% giá căn); đợt cuối nhận phần dư làm tròn",
            IsActive = true,
            CreatedAt = now
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
