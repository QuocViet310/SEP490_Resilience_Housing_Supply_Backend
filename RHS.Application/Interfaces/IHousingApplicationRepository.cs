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
    /// Quy định: mỗi người chỉ được 1 hồ sơ/dự án.
    /// </summary>
    Task<bool> ExistsByApplicantAndProjectAsync(Guid applicantId, Guid projectId);

    /// <summary>
    /// Kiểm tra một số CCCD đã tồn tại trong hồ sơ KHÁC của cùng dự án hay chưa.
    /// Được gọi khi Applicant Submit để đảm bảo mỗi CCCD chỉ có một hồ sơ trong một dự án,
    /// bất kể người dùng sử dụng tài khoản nào.
    /// </summary>
    /// <param name="citizenId">Số CCCD cần kiểm tra.</param>
    /// <param name="projectId">Dự án cần kiểm tra.</param>
    /// <param name="excludeApplicationId">
    /// ID hồ sơ hiện tại — sẽ bị loại trừ khỏi tìm kiếm để tránh tự block chính mình.
    /// </param>
    /// <returns><c>true</c> nếu CCCD đã tồn tại trong hồ sơ khác của dự án đó.</returns>
    Task<bool> CitizenIdExistsInProjectAsync(string citizenId, Guid projectId, Guid excludeApplicationId);

    Task<PagedResult<HousingApplicationDashboardItemDto>> GetVerificationOfficerDashboardAsync(
        HousingApplicationDashboardQueryDto query);

    Task<PagedResult<HousingApplicationDashboardItemDto>> GetWardManagerDashboardAsync(
        HousingApplicationDashboardQueryDto query);
}
