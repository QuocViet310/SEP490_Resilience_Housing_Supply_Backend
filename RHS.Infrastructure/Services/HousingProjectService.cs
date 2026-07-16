using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
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
            DepositAmount = request.DepositAmount,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            MinArea = request.MinArea,
            MaxArea = request.MaxArea,
            AvailableUnits = request.AvailableUnits,
            ThumbnailUrl = thumbnailUrl,
            HousingProjectStatusId = request.HousingProjectStatusId,
            IsDeleted = false,
            
            // New legal & developer fields
            DecisionNumber = request.DecisionNumber,
            ApprovalDate = request.ApprovalDate,
            IsConfirmed = request.IsConfirmed,
            ApplicationOpenDate = request.ApplicationOpenDate,
            ApplicationCloseDate = request.ApplicationCloseDate,
            PublicAnnounceAt = request.IsConfirmed ? DateTime.UtcNow : null,
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
        UpdateHousingProjectRequestDto request)
    {
        // Validate request
        ValidateHousingProjectRequest(request);

        // Check if project exists
        var existingProject = await _repository.GetByIdAsync(id);
        if (existingProject == null)
        {
            throw new InvalidOperationException($"Housing project with ID {id} not found.");
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
        existingProject.DepositAmount = request.DepositAmount;
        existingProject.MinPrice = request.MinPrice;
        existingProject.MaxPrice = request.MaxPrice;
        existingProject.MinArea = request.MinArea;
        existingProject.MaxArea = request.MaxArea;
        existingProject.AvailableUnits = request.AvailableUnits;
        existingProject.ThumbnailUrl = thumbnailUrl;
        existingProject.HousingProjectStatusId = request.HousingProjectStatusId;

        // Update legal fields
        existingProject.DecisionNumber = request.DecisionNumber;
        existingProject.ApprovalDate = request.ApprovalDate;
        existingProject.IsConfirmed = request.IsConfirmed;
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

        // MinArea > 0
        if (request.MinArea <= 0)
        {
            throw new ArgumentException("MinArea must be greater than 0.");
        }

        // MaxArea >= MinArea
        if (request.MaxArea < request.MinArea)
        {
            throw new ArgumentException("MaxArea must be greater than or equal to MinArea.");
        }

        // AvailableUnits >= 0
        if (request.AvailableUnits < 0)
        {
            throw new ArgumentException("AvailableUnits must be greater than or equal to 0.");
        }

        // IsConfirmed must be true
        if (request.IsConfirmed != true)
        {
            throw new ArgumentException("IsConfirmed must be true.");
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
            DepositAmount = project.DepositAmount,
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
            IsConfirmed = project.IsConfirmed,
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
                .ToList()
        };
    }

    public async Task<HousingProjectResponseDto> UpdateProjectStatusAsync(Guid id, string action, string? rejectReason)
    {
        var project = await _repository.GetByIdAsync(id);
        if (project == null)
        {
            throw new InvalidOperationException($"Housing project with ID {id} not found.");
        }

        // Must be PENDING to approve/reject
        if (project.HousingProjectStatus?.StatusCode != "PENDING")
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
            project.IsConfirmed = true;
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

        await _repository.UpdateAsync(project);
        
        var updatedProject = await _repository.GetByIdAsync(project.Id);
        return MapToResponseDto(updatedProject ?? project);
    }
}
