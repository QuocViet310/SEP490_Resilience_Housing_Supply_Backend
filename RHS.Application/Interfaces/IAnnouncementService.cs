using Microsoft.AspNetCore.Http;
using RHS.Application.DTOs.Announcement;

namespace RHS.Application.Interfaces;

public interface IAnnouncementService
{
    /// <summary>Tạo mới announcement (SXD/Admin).</summary>
    Task<AnnouncementDto> CreateAsync(Guid createdBy, CreateAnnouncementRequestDto request, CancellationToken ct = default);

    /// <summary>Cập nhật announcement (SXD/Admin, chỉ owner hoặc Admin).</summary>
    Task<AnnouncementDto> UpdateAsync(Guid announcementId, Guid userId, UpdateAnnouncementRequestDto request, CancellationToken ct = default);

    /// <summary>Soft delete announcement.</summary>
    Task DeleteAsync(Guid announcementId, Guid userId, CancellationToken ct = default);

    /// <summary>Lấy chi tiết theo ID (public nếu Published).</summary>
    Task<AnnouncementDto?> GetByIdAsync(Guid announcementId, CancellationToken ct = default);

    /// <summary>Danh sách public (Published, chưa xóa) có phân trang + filter.</summary>
    Task<PagedAnnouncementResultDto> GetPublishedAsync(
        int page, int pageSize,
        string? type = null, string? search = null,
        CancellationToken ct = default);

    /// <summary>Danh sách quản lý (tất cả status) cho SXD/Admin.</summary>
    Task<PagedAnnouncementResultDto> GetAllForManagementAsync(
        int page, int pageSize,
        string? type = null, string? status = null, string? search = null,
        CancellationToken ct = default);

    /// <summary>Upload file đính kèm cho announcement.</summary>
    Task<AnnouncementAttachmentDto> AddAttachmentAsync(Guid announcementId, Guid userId, IFormFile file, CancellationToken ct = default);

    /// <summary>Xóa file đính kèm.</summary>
    Task DeleteAttachmentAsync(Guid announcementId, Guid attachmentId, Guid userId, CancellationToken ct = default);
}
