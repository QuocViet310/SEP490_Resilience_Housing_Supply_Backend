using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Repository interface cho ApplicationStatusHistory (Review History).
/// Mọi hành động xét duyệt của VO/WM đều được persist qua interface này.
/// </summary>
public interface IReviewHistoryRepository
{
    /// <summary>
    /// Ghi một bản ghi lịch sử xét duyệt vào DB.
    /// Được gọi sau mỗi hành động review thành công.
    /// </summary>
    Task<ApplicationStatusHistory> CreateAsync(ApplicationStatusHistory history);

    /// <summary>
    /// Lấy toàn bộ lịch sử xét duyệt của một hồ sơ, sắp xếp mới nhất trước.
    /// </summary>
    Task<IReadOnlyList<ApplicationStatusHistory>> GetByApplicationIdAsync(Guid applicationId);
}
