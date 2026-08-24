using RHS.Application.DTOs.Milestone;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service quản lý cấu hình các đợt thanh toán (3 - 6 đợt) cho dự án NOXH.
/// </summary>
public interface IProjectMilestoneService
{
    /// <summary>
    /// Lấy danh sách các đợt thanh toán đã cấu hình của dự án.
    /// </summary>
    Task<ProjectMilestonesResponseDto> GetProjectMilestonesAsync(
        Guid projectId,
        CancellationToken ct = default);

    /// <summary>
    /// Thiết lập / Cập nhật trọn gói 3 đến 6 đợt đóng tiền cho dự án (CĐT / Admin).
    /// Thực thi kiểm tra toàn bộ các validation nghiệp vụ (3-6 đợt, tổng 100%, tỷ lệ đợt 1, đợt sổ hồng, thứ tự liên tục...).
    /// </summary>
    Task<ProjectMilestonesResponseDto> ConfigureProjectMilestonesAsync(
        Guid projectId,
        Guid userId,
        ConfigureProjectMilestonesRequestDto request,
        CancellationToken ct = default);
}
