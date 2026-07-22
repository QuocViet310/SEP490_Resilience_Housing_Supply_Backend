using RHS.Application.DTOs.HousingProjects;
using RHS.Application.DTOs.PublicPostCheck;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service interface quản lý tra cứu hậu kiểm công khai cho Public Portal
/// </summary>
public interface IPublicPostCheckService
{
    /// <summary>
    /// Lấy danh sách hậu kiểm công khai các đối tượng đã giao dịch/trúng nhà ở xã hội (phân trang + lọc)
    /// </summary>
    Task<PagedResultDto<PublicPostCheckListItemDto>> GetPublicPostCheckListAsync(
        PublicPostCheckFilterDto filter,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy thông tin chi tiết hậu kiểm công khai của một hồ sơ
    /// </summary>
    Task<PublicPostCheckDetailDto?> GetPublicPostCheckDetailAsync(
        Guid applicationId,
        CancellationToken ct = default);

    /// <summary>
    /// Tra cứu xác minh thông tin sở hữu NOXH theo số CCCD (exact search)
    /// </summary>
    Task<PublicCitizenVerificationResultDto> VerifyCitizenOwnershipAsync(
        string citizenId,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy số liệu thống kê công khai phục vụ dashboard/widgets
    /// </summary>
    Task<PublicPostCheckStatsDto> GetPublicPostCheckStatsAsync(
        CancellationToken ct = default);
}
