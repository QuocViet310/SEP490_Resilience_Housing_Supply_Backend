using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.IssueReports;

namespace RHS.Application.Interfaces;

public interface IIssueReportService
{
    Task<IssueReportDetailResponseDto> CreateAsync(Guid userId, CreateIssueReportRequestDto request);

    Task<IssueReportDetailResponseDto> GetByIdAsync(Guid id);

    Task<PagedResultDto<IssueReportListItemDto>> GetMyReportsAsync(
        Guid userId,
        int pageIndex,
        int pageSize);

    Task<PagedResultDto<IssueReportListItemDto>> GetAllReportsAsync(
        int pageIndex,
        int pageSize,
        string? search = null,
        string? status = null,
        string? issueType = null);

    Task<IssueReportDetailResponseDto> UpdateStatusAsync(Guid id, UpdateIssueReportStatusRequestDto request);
}
