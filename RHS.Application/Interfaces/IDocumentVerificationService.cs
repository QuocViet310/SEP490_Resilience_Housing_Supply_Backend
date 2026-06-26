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
}
