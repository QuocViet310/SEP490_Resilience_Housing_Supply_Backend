using RHS.Application.DTOs.HousingApplications;
using RHS.Application.DTOs.HousingApplications.Dashboard;
using RHS.Application.DTOs.HousingProjects;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service xử lý nghiệp vụ quản lý hồ sơ đăng ký nhà ở xã hội.
/// Bao gồm: tạo hồ sơ, xem chi tiết, lấy danh sách, dashboard, final list.
/// </summary>
public interface IHousingApplicationService
{
    /// <summary>
    /// Tạo hồ sơ mới với trạng thái DRAFT.
    /// Chỉ Applicant mới được tạo. Mỗi Applicant chỉ được 1 hồ sơ/dự án (trừ REJECTED/CANCELED).
    /// </summary>
    Task<CreateApplicationResponseDto> CreateApplicationAsync(
        Guid applicantId,
        CreateApplicationRequestDto request);

    /// <summary>
    /// Lấy chi tiết một hồ sơ theo ID.
    /// Bao gồm: thông tin form, danh sách tài liệu, lịch sử xét duyệt.
    /// </summary>
    Task<ApplicationDetailResponseDto> GetApplicationByIdAsync(Guid applicationId);

    /// <summary>
    /// Lấy danh sách hồ sơ của một Applicant (chỉ xem của mình).
    /// </summary>
    Task<PagedResultDto<ApplicationSummaryResponseDto>> GetMyApplicationsAsync(
        Guid applicantId,
        ApplicationFilterRequestDto filter);

    /// <summary>
    /// Lấy tất cả hồ sơ (dành cho Officer/Manager xem và xét duyệt).
    /// </summary>
    Task<PagedResultDto<ApplicationSummaryResponseDto>> GetAllApplicationsAsync(
        ApplicationFilterRequestDto filter);

    /// <summary>
    /// Dashboard cho Housing Developer (CĐT) — Task #9.
    /// Chỉ load hồ sơ thuộc dự án của CĐT đang đăng nhập.
    /// </summary>
    Task<PagedResult<HousingApplicationDashboardItemDto>> GetHousingDeveloperDashboardAsync(
        HousingApplicationDashboardQueryDto query);

    /// <summary>
    /// Dashboard cho Department Of Construction (SXD) — Task #9.
    /// Load hồ sơ PENDING_SXD_REVIEW, APPROVED, REJECTED của tất cả dự án.
    /// </summary>
    Task<PagedResult<HousingApplicationDashboardItemDto>> GetDepartmentOfConstructionDashboardAsync(
        HousingApplicationDashboardQueryDto query);

    /// <summary>
    /// Lấy danh sách chốt cuối (Final List) cho dự án (Task #10).
    /// Chỉ trả về các hồ sơ có trạng thái DEPOSIT_PAID.
    /// Dữ liệu dùng để SXD export Excel/PDF công bố trên website.
    /// </summary>
    Task<List<FinalListItemDto>> GetFinalListByProjectAsync(Guid projectId);
}

