using RHS.Application.DTOs.HousingApplications;
using RHS.Application.DTOs.HousingApplications.Dashboard;
using RHS.Application.DTOs.HousingProjects;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service xử lý nghiệp vụ quản lý hồ sơ đăng ký nhà ở xã hội.
/// Bao gồm: tạo hồ sơ, xem chi tiết, lấy danh sách.
/// </summary>
public interface IHousingApplicationService
{
    /// <summary>
    /// Tạo hồ sơ mới với trạng thái DRAFT.
    /// Chỉ Applicant mới được tạo. Mỗi Applicant chỉ được 1 hồ sơ/dự án.
    /// </summary>
    /// <param name="applicantId">ID người đăng ký (lấy từ JWT)</param>
    /// <param name="request">Dữ liệu form đăng ký</param>
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

    Task<PagedResult<HousingApplicationDashboardItemDto>> GetVerificationOfficerDashboardAsync(
        HousingApplicationDashboardQueryDto query);

    Task<PagedResult<HousingApplicationDashboardItemDto>> GetWardManagerDashboardAsync(
        HousingApplicationDashboardQueryDto query);
}
