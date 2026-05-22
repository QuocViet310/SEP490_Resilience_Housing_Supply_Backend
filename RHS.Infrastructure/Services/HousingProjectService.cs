using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.HousingProjects;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Services;

public class HousingProjectService : IHousingProjectService
{
    private readonly IHousingProjectRepository _repository;

    public HousingProjectService(IHousingProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<HousingProjectResponseDto>> GetHousingProjectsAsync(
        HousingProjectFilterRequestDto request)
    {
        // Validate request
        if (request.PageIndex < 1)
            request.PageIndex = 1;

        if (request.PageSize < 1)
            request.PageSize = 12;

        if (request.PageSize > 100)
            request.PageSize = 100;

        // Call repository to get paginated results
        return await _repository.GetHousingProjectsAsync(request);
    }

    public async Task<HousingProjectResponseDto> CreateHousingProjectAsync(
        CreateHousingProjectRequestDto request)
    {
        // Validate request
        ValidateHousingProjectRequest(request);

        // Create entity
        var housingProject = new HousingProject
        {
            Id = Guid.NewGuid(),
            ProjectName = request.ProjectName,
            Description = request.Description,
            Province = request.Province,
            District = request.District,
            Address = request.Address,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            MinArea = request.MinArea,
            MaxArea = request.MaxArea,
            AvailableUnits = request.AvailableUnits,
            ThumbnailUrl = request.ThumbnailUrl,
            HousingProjectStatusId = request.HousingProjectStatusId,
            IsDeleted = false
        };

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

        // Update entity
        existingProject.ProjectName = request.ProjectName;
        existingProject.Description = request.Description;
        existingProject.Province = request.Province;
        existingProject.District = request.District;
        existingProject.Address = request.Address;
        existingProject.MinPrice = request.MinPrice;
        existingProject.MaxPrice = request.MaxPrice;
        existingProject.MinArea = request.MinArea;
        existingProject.MaxArea = request.MaxArea;
        existingProject.AvailableUnits = request.AvailableUnits;
        existingProject.ThumbnailUrl = request.ThumbnailUrl;
        existingProject.HousingProjectStatusId = request.HousingProjectStatusId;

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

        // Address is required
        if (string.IsNullOrWhiteSpace(request.Address))
        {
            throw new ArgumentException("Address is required.");
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
            Address = project.Address,
            MinPrice = project.MinPrice,
            MaxPrice = project.MaxPrice,
            MinArea = project.MinArea,
            MaxArea = project.MaxArea,
            AvailableUnits = project.AvailableUnits,
            ThumbnailUrl = project.ThumbnailUrl,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            Status = project.HousingProjectStatus?.StatusName
        };
    }
}
