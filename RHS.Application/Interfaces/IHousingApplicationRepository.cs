using RHS.Application.DTOs.HousingApplications;
using RHS.Application.DTOs.HousingApplications.Dashboard;
using RHS.Application.DTOs.HousingProjects;
using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Repository interface cho HousingApplication.
/// Tách biệt data access khỏi business logic theo Clean Architecture.
/// </summary>
public interface IHousingApplicationRepository
{
    /// <summary>Tạo hồ sơ mới trong DB.</summary>
    Task<HousingApplication> CreateAsync(HousingApplication application);

    /// <summary>
    /// Lấy hồ sơ theo ID, bao gồm đầy đủ navigation properties
    /// (Applicant, Officer, Project, Documents, StatusHistories).
    /// </summary>
    Task<HousingApplication?> GetByIdWithDetailsAsync(Guid applicationId);

    /// <summary>
    /// Lấy danh sách hồ sơ của một Applicant (có phân trang + lọc).
    /// </summary>
    Task<PagedResultDto<ApplicationSummaryResponseDto>> GetByApplicantAsync(
        Guid applicantId,
        ApplicationFilterRequestDto filter);

    /// <summary>
    /// Lấy tất cả hồ sơ (dành cho Officer/Manager, có phân trang + lọc).
    /// </summary>
    Task<PagedResultDto<ApplicationSummaryResponseDto>> GetAllAsync(
        ApplicationFilterRequestDto filter);

    /// <summary>
    /// Cập nhật entity hồ sơ (trạng thái, thông tin).
    /// </summary>
    Task UpdateAsync(HousingApplication application);

    /// <summary>
    /// Kiểm tra Applicant đã có hồ sơ cho dự án này chưa.
    /// Quy định: mỗi người chỉ được 1 hồ sơ/dự án (trừ REJECTED/CANCELED).
    /// </summary>
    Task<bool> ExistsByApplicantAndProjectAsync(Guid applicantId, Guid projectId);

    /// <summary>
    /// Kiểm tra Applicant đã có hồ sơ đang hoạt động hoặc đã được phê duyệt ở dự án khác hay chưa.
    /// Trạng thái hoạt động bao gồm: SUBMITTED, REVIEWING, NEED_MORE_DOCUMENTS, PENDING_SXD_REVIEW, APPROVED, DEPOSIT_PAID.
    /// </summary>
    Task<bool> HasActiveApplicationAsync(Guid applicantId);

    /// <summary>
    /// Kiểm tra một số CCCD đã tồn tại trong hồ sơ KHÁC của cùng dự án hay chưa.
    /// Exclude các hồ sơ REJECTED/CANCELED để giải phóng CCCD.
    /// </summary>
    Task<bool> CitizenIdExistsInProjectAsync(string citizenId, Guid projectId, Guid excludeApplicationId);

    /// <summary>
    /// Lấy danh sách hồ sơ theo nhiều IDs (dùng cho batch operations như Task #7).
    /// </summary>
    Task<List<HousingApplication>> GetByIdsAsync(IEnumerable<Guid> ids);

    // ── Dashboard ────────────────────────────────────────────────────

    Task<PagedResult<HousingApplicationDashboardItemDto>> GetHousingDeveloperDashboardAsync(
        HousingApplicationDashboardQueryDto query);

    Task<PagedResult<HousingApplicationDashboardItemDto>> GetDepartmentOfConstructionDashboardAsync(
        HousingApplicationDashboardQueryDto query);

    // ── Final List ───────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách chốt cuối (DEPOSIT_PAID) cho dự án (Task #10).
    /// </summary>
    Task<List<FinalListItemDto>> GetFinalListByProjectAsync(Guid projectId);
}

