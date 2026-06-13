using RHS.Application.DTOs.HousingApplications;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service xử lý nghiệp vụ upload và quản lý tài liệu trong hồ sơ nhà ở xã hội.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Upload một tài liệu PDF vào hồ sơ.
    /// Áp dụng các ràng buộc nghiệp vụ:
    ///   - Chỉ chấp nhận định dạng PDF.
    ///   - DocumentType phải là HOUSING_CONDITION_PROOF hoặc POVERTY_HOUSEHOLD_CERTIFICATE.
    ///   - Mỗi hồ sơ chỉ được có 1 tài liệu duy nhất cho mỗi loại (không được upload đè).
    /// </summary>
    /// <param name="applicationId">ID hồ sơ cần upload vào</param>
    /// <param name="uploadedByUserId">ID người upload (lấy từ JWT)</param>
    /// <param name="request">DTO chứa DocumentType và file PDF</param>
    Task<UploadDocumentResponseDto> UploadDocumentAsync(
        Guid applicationId,
        Guid uploadedByUserId,
        UploadDocumentRequestDto request);

    /// <summary>
    /// Xóa một tài liệu khỏi hồ sơ.
    /// Chỉ cho phép khi hồ sơ đang ở trạng thái DRAFT hoặc NEED_MORE_DOCUMENTS.
    /// </summary>
    Task DeleteDocumentAsync(Guid documentId, Guid requestedByUserId);
}
