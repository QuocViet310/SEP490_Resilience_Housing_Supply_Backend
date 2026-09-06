using RHS.Application.DTOs.Milestone;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service quản lý lịch thanh toán do chủ đầu tư thỏa thuận (số đợt không cố định).
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
    /// Thiết lập / cập nhật các đợt đóng tiền do chủ đầu tư nhập (tên, tỷ lệ, mốc mở).
    /// Kiểm tra Điều 89 Luật Nhà ở 2023 (30% / 70% / 95% / giữ 5%), tổng 100%, thứ tự liên tục.
    /// </summary>
    Task<ProjectMilestonesResponseDto> ConfigureProjectMilestonesAsync(
        Guid projectId,
        Guid userId,
        ConfigureProjectMilestonesRequestDto request,
        CancellationToken ct = default);
}
