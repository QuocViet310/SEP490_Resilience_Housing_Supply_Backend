using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Repository interface cho ApplicationDocument.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>Thêm một tài liệu mới vào DB.</summary>
    Task<ApplicationDocument> CreateAsync(ApplicationDocument document);

    /// <summary>Tìm tài liệu theo ID.</summary>
    Task<ApplicationDocument?> GetByIdAsync(Guid documentId);

    /// <summary>
    /// Lấy tất cả tài liệu của một hồ sơ.
    /// </summary>
    Task<IReadOnlyList<ApplicationDocument>> GetByApplicationIdAsync(Guid applicationId);

    /// <summary>
    /// Kiểm tra hồ sơ đã có tài liệu của loại này chưa.
    /// Dùng để enforce ràng buộc: mỗi hồ sơ chỉ được 1 tài liệu/loại.
    /// </summary>
    Task<bool> ExistsByApplicationAndTypeAsync(Guid applicationId, string documentType);

    /// <summary>Xóa tài liệu khỏi DB.</summary>
    Task DeleteAsync(ApplicationDocument document);
}
