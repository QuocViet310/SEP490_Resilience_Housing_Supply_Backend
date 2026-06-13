using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.IssueReports;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Services;

public class IssueReportService : IIssueReportService
{
    private readonly IIssueReportRepository _repository;

    public IssueReportService(IIssueReportRepository repository)
    {
        _repository = repository;
    }

    public async Task<IssueReportDetailResponseDto> CreateAsync(
        Guid userId,
        CreateIssueReportRequestDto request)
    {
        // Validate request
        ValidateCreateRequest(request);

        // Create entity
        var issueReport = new IssueReport
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            Description = request.Description,
            IssueType = request.IssueType,
            ScreenshotUrl = request.ScreenshotUrl,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };

        // Save to repository
        await _repository.CreateAsync(issueReport);

        // Get the created report with user info
        var createdReport = await _repository.GetByIdAsync(issueReport.Id);
        if (createdReport == null)
        {
            throw new InvalidOperationException($"Failed to retrieve created issue report with ID {issueReport.Id}.");
        }

        return MapToDetailResponseDto(createdReport);
    }

    public async Task<IssueReportDetailResponseDto> GetByIdAsync(Guid id)
    {
        var report = await _repository.GetByIdAsync(id);
        if (report == null)
        {
            throw new InvalidOperationException($"Issue report with ID {id} not found.");
        }

        return MapToDetailResponseDto(report);
    }

    public async Task<PagedResultDto<IssueReportListItemDto>> GetMyReportsAsync(
        Guid userId,
        int pageIndex,
        int pageSize)
    {
        // Validate pagination
        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Max(pageSize, 1);
        pageSize = Math.Min(pageSize, 100);

        // Get paged results
        var result = await _repository.GetByUserIdAsync(userId, pageIndex, pageSize);

        // Map to list item DTOs
        var items = result.Items
            .Select(x => MapToListItemDto(x))
            .ToList();

        return new PagedResultDto<IssueReportListItemDto>
        {
            PageIndex = result.PageIndex,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            Items = items
        };
    }

    public async Task<PagedResultDto<IssueReportListItemDto>> GetAllReportsAsync(
        int pageIndex,
        int pageSize,
        string? search = null,
        string? status = null,
        string? issueType = null)
    {
        // Validate pagination
        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Max(pageSize, 1);
        pageSize = Math.Min(pageSize, 100);

        // Get paged results
        var result = await _repository.GetPagedAsync(pageIndex, pageSize, search, status, issueType);

        // Map to list item DTOs
        var items = result.Items
            .Select(x => MapToListItemDto(x))
            .ToList();

        return new PagedResultDto<IssueReportListItemDto>
        {
            PageIndex = result.PageIndex,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            Items = items
        };
    }

    public async Task<IssueReportDetailResponseDto> UpdateStatusAsync(
        Guid id,
        UpdateIssueReportStatusRequestDto request)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            throw new ArgumentException("Status is required.");
        }

        // Get existing report
        var report = await _repository.GetByIdAsync(id);
        if (report == null)
        {
            throw new InvalidOperationException($"Issue report with ID {id} not found.");
        }

        // Update status
        report.Status = request.Status;

        // If status is "Resolved", set ResolvedAt
        if (request.Status == "Resolved")
        {
            report.ResolvedAt = DateTime.UtcNow;
        }

        // Save to repository
        await _repository.UpdateAsync(report);

        // Get updated report
        var updatedReport = await _repository.GetByIdAsync(id);
        if (updatedReport == null)
        {
            throw new InvalidOperationException($"Failed to retrieve updated issue report with ID {id}.");
        }

        return MapToDetailResponseDto(updatedReport);
    }

    private void ValidateCreateRequest(CreateIssueReportRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IssueType))
        {
            throw new ArgumentException("IssueType is required.");
        }

        if (request.Title.Length > 255)
        {
            throw new ArgumentException("Title must not exceed 255 characters.");
        }

        if (request.IssueType.Length > 50)
        {
            throw new ArgumentException("IssueType must not exceed 50 characters.");
        }

        if (!string.IsNullOrWhiteSpace(request.ScreenshotUrl) && request.ScreenshotUrl.Length > 1000)
        {
            throw new ArgumentException("ScreenshotUrl must not exceed 1000 characters.");
        }
    }

    private IssueReportDetailResponseDto MapToDetailResponseDto(IssueReport report)
    {
        return new IssueReportDetailResponseDto
        {
            Id = report.Id,
            Title = report.Title,
            Description = report.Description,
            IssueType = report.IssueType,
            Status = report.Status,
            ScreenshotUrl = report.ScreenshotUrl,
            CreatedAt = report.CreatedAt,
            ResolvedAt = report.ResolvedAt,
            ReporterName = report.User?.FullName ?? "Unknown",
            ReporterId = report.UserId
        };
    }

    private IssueReportListItemDto MapToListItemDto(IssueReport report)
    {
        return new IssueReportListItemDto
        {
            Id = report.Id,
            Title = report.Title,
            IssueType = report.IssueType,
            Status = report.Status,
            CreatedAt = report.CreatedAt,
            ReporterName = report.User?.FullName ?? "Unknown"
        };
    }
}
