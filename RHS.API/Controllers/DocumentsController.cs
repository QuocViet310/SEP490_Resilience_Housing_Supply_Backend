using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Infrastructure.Data;
using RHS.Infrastructure.Exceptions;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// API quản lý tài liệu đính kèm trong hồ sơ nhà ở xã hội.
/// Chỉ chấp nhận file PDF, tối đa 10MB.
/// Mỗi hồ sơ chỉ được upload 1 tài liệu cho mỗi loại giấy tờ.
/// </summary>
[ApiController]
[Route("api/housing-applications/{applicationId:guid}/documents")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentService documentService,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _logger          = logger;
    }

    // ──────────────────────────────────────────────────────────────
    // APPLICANT: Upload tài liệu PDF
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [Applicant] Upload tài liệu PDF vào hồ sơ.
    ///
    /// Loại giấy tờ hợp lệ (trường DocumentType):
    ///   - HOUSING_CONDITION_PROOF             : Giấy xác nhận điều kiện nhà ở (bắt buộc tất cả)
    ///   - POVERTY_HOUSEHOLD_CERTIFICATE       : Giấy chứng nhận hộ nghèo/cận nghèo
    ///   - MERIT_PERSON_CERTIFICATE            : Giấy xác nhận người có công với cách mạng
    ///   - LOW_INCOME_CERTIFICATE              : Giấy xác nhận thu nhập thấp tại đô thị
    ///   - EMPLOYMENT_CERTIFICATE              : Giấy xác nhận đang làm việc tại DN/HTX/KCN
    ///   - MILITARY_SERVICE_CERTIFICATE        : Giấy xác nhận phục vụ lực lượng vũ trang
    ///   - CIVIL_SERVANT_CERTIFICATE           : Giấy xác nhận cán bộ/công chức/viên chức
    ///   - PUBLIC_HOUSING_RETURN_CERTIFICATE   : Văn bản trả lại nhà ở công vụ
    ///   - LAND_RECOVERY_DECISION              : Quyết định thu hồi đất/giải tỏa nhà ở
    ///   - INCOME_CERTIFICATE                  : Giấy xác nhận thu nhập
    ///
    /// Ràng buộc:
    ///   - Chỉ chấp nhận file .pdf (Content-Type: application/pdf)
    ///   - Kích thước tối đa 10MB
    ///   - Mỗi hồ sơ chỉ được 1 tài liệu cho mỗi loại
    ///   - Hồ sơ phải ở trạng thái DRAFT hoặc NEED_MORE_DOCUMENTS
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RoleConstants.Applicant)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadDocumentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadDocument(
        Guid applicationId,
        [FromForm] UploadDocumentRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            var result = await _documentService.UploadDocumentAsync(applicationId, userId, request);

            return CreatedAtAction(
                actionName: null,   // Không có GetById cho document — dùng FileUrl trực tiếp
                value: result);
        }
        catch (ApplicationNotFoundException ex)
        {
            return NotFound(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (InvalidDocumentFileException ex) when (ex.ErrorCode == InvalidDocumentFileException.CodeDuplicateDocumentType)
        {
            // 409 Conflict: đã có giấy tờ cùng loại
            _logger.LogWarning("Duplicate document type for application {Id}: {DocType}", applicationId, request.DocumentType);
            return Conflict(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (InvalidDocumentFileException ex)
        {
            // 400 Bad Request: file không hợp lệ
            _logger.LogWarning("Invalid document file for application {Id}: {Msg}", applicationId, ex.Message);
            return BadRequest(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (InvalidApplicationStatusTransitionException ex)
        {
            // 422: hồ sơ không ở trạng thái cho phép upload
            _logger.LogWarning("Cannot upload to application {Id}: {Msg}", applicationId, ex.Message);
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Cloudinary upload thất bại
            _logger.LogError("File upload failed for application {Id}: {Msg}", applicationId, ex.Message);
            return StatusCode(502, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading document for application {Id}.", applicationId);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi upload tài liệu." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // APPLICANT: Xóa tài liệu
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// [Applicant] Xóa tài liệu khỏi hồ sơ.
    /// Chỉ cho phép khi hồ sơ đang ở trạng thái DRAFT hoặc NEED_MORE_DOCUMENTS.
    /// </summary>
    [HttpDelete("{documentId:guid}")]
    [Authorize(Roles = RoleConstants.Applicant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteDocument(Guid applicationId, Guid documentId)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { message = "Không xác định được danh tính người dùng." });

        try
        {
            await _documentService.DeleteDocumentAsync(documentId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidApplicationStatusTransitionException ex)
        {
            _logger.LogWarning("Cannot delete document {DocId} from application {AppId}: {Msg}",
                documentId, applicationId, ex.Message);
            return UnprocessableEntity(new { errorCode = ex.ErrorCode, message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Unauthorized delete attempt on document {DocId}.", documentId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocId} from application {AppId}.",
                documentId, applicationId);
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi xóa tài liệu." });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // AI Verification Endpoints
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy kết quả xác minh AI của tài liệu đính kèm.
    /// </summary>
    [HttpGet("{documentId:guid}/verification")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVerificationResult(Guid documentId, [FromServices] AppDbContext dbContext)
    {
        var result = await dbContext.AIVerificationResults
            .FirstOrDefaultAsync(r => r.DocumentId == documentId);

        if (result == null)
            return NotFound(new { message = "Chưa có kết quả xác minh AI cho tài liệu này." });

        return Ok(new
        {
            result.VerificationId,
            result.DocumentId,
            result.ValidationResult,
            result.ExtractedFullName,
            result.ExtractedCitizenId,
            result.ExtractedAddress,
            result.ExtractedDateOfBirth,
            result.ErrorDetails,
            result.VerifiedAt
        });
    }

    /// <summary>
    /// Trigger xác minh AI thủ công cho tài liệu.
    /// </summary>
    [HttpPost("{documentId:guid}/verify")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.HousingAuthorityOfficer}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TriggerVerification(
        Guid documentId, 
        [FromServices] IDocumentVerificationService verificationService)
    {
        try
        {
            var result = await verificationService.VerifyDocumentAsync(documentId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi trigger thủ công AI Verification cho tài liệu {DocId}", documentId);
            return StatusCode(500, new { message = "Lỗi hệ thống khi xác minh tài liệu.", details = ex.Message });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Private helper
    // ──────────────────────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
    }
}
