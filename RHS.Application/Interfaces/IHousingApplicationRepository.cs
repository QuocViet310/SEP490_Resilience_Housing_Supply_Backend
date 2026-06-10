using RHS.Application.DTOs.HousingApplications;
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
}
