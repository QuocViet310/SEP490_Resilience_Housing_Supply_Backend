using System;
using System.Threading;
using System.Threading.Tasks;
using RHS.Application.DTOs.DocumentVerification;

namespace RHS.Application.Interfaces;

public interface IDocumentVerificationService
{
    /// <summary>
    /// Gửi file PDF của tài liệu lên Gemini API để phân tích và so khớp với thông tin profile của User.
    /// </summary>
    /// <param name="documentId">ID của ApplicationDocument</param>
    /// <param name="cancellationToken">Token hủy</param>
    Task<DocumentVerificationResultDto> VerifyDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// [Chủ đầu tư] Trigger AI kiểm tra toàn bộ giấy tờ trong hồ sơ xem đúng Form mẫu (Mẫu xác nhận đối tượng, thu nhập, nhà ở) 
    /// và liệt kê xem hồ sơ đã nộp ĐỦ hay THIẾU giấy tờ nào theo nhóm Đối tượng ưu tiên của người dân.
    /// </summary>
    Task<ApplicationAuditResultDto> AuditApplicationDocumentsAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default);
}
