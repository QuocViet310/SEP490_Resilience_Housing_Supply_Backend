using RHS.Application.DTOs.HousingProjects;
using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

public interface IIssueReportRepository
{
    Task<IssueReport> CreateAsync(IssueReport entity);

    Task<IssueReport?> GetByIdAsync(Guid id);

    Task<PagedResultDto<IssueReport>> GetPagedAsync(
        int pageIndex,
        int pageSize,
        string? search = null,
        string? status = null,
        string? issueType = null);

    Task<PagedResultDto<IssueReport>> GetByUserIdAsync(
        Guid userId,
        int pageIndex,
        int pageSize);

    Task UpdateAsync(IssueReport entity);
}
