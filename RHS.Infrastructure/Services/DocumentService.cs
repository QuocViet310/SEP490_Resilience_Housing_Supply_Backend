using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.HousingApplications;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Exceptions;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Service xử lý nghiệp vụ upload và quản lý tài liệu PDF
/// trong hồ sơ đăng ký nhà ở xã hội.
/// </summary>
public class DocumentService : IDocumentService
{
    // Kích thước tối đa: 10MB
    private const long MaxPdfSizeBytes = 10 * 1024 * 1024;
    private const string CloudinaryFolder = "housing-docs";

    // Các trạng thái cho phép upload/xóa tài liệu
    private static readonly HashSet<string> EditableStatuses = new()
    {
        ApplicationStatusConstants.Draft,
        ApplicationStatusConstants.NeedMoreDocuments
    };

    private readonly IDocumentRepository _documentRepo;
    private readonly IHousingApplicationRepository _applicationRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documentRepo,
        IHousingApplicationRepository applicationRepo,
        IFileStorageService fileStorage,
        ILogger<DocumentService> logger)
    {
        _documentRepo    = documentRepo;
        _applicationRepo = applicationRepo;
        _fileStorage     = fileStorage;
        _logger          = logger;
    }

    // ─────────────────────────────────────────────────────────────
    // Upload Document
    // ─────────────────────────────────────────────────────────────

    public async Task<UploadDocumentResponseDto> UploadDocumentAsync(
        Guid applicationId,
        Guid uploadedByUserId,
        UploadDocumentRequestDto request)
    {
        _logger.LogInformation(
            "User {UserId} uploading document type '{DocType}' for application {AppId}.",
            uploadedByUserId, request.DocumentType, applicationId);

        // ── 1. Validate: File không được rỗng ──────────────────────
        if (request.File == null || request.File.Length == 0)
            throw InvalidDocumentFileException.EmptyFile(request.File?.FileName ?? "unknown");

        // ── 2. Validate: Chỉ chấp nhận PDF ────────────────────────
        if (!_fileStorage.IsValidPdfFile(request.File, MaxPdfSizeBytes))
        {
            var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            if (ext != ".pdf")
                throw InvalidDocumentFileException.NotPdf(request.File.FileName);

            if (!request.File.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                throw InvalidDocumentFileException.NotPdf(request.File.FileName);

            // Vượt kích thước
            throw InvalidDocumentFileException.TooLarge(
                request.File.FileName, MaxPdfSizeBytes / (1024 * 1024));
        }

        // ── 3. Validate: DocumentType phải thuộc danh sách cho phép ─
        if (!DocumentTypeConstants.IsAllowedApplicantType(request.DocumentType))
            throw InvalidDocumentFileException.InvalidType(request.DocumentType);

        // ── 4. Tải hồ sơ và kiểm tra trạng thái ──────────────────
        var application = await _applicationRepo.GetByIdWithDetailsAsync(applicationId);
        if (application is null)
            throw new ApplicationNotFoundException(applicationId);

        if (!EditableStatuses.Contains(application.ApplicationStatus))
        {
            throw new InvalidApplicationStatusTransitionException(
                application.ApplicationStatus,
                "UPLOAD_DOCUMENT",
                $"Không thể upload tài liệu khi hồ sơ đang ở trạng thái '{application.ApplicationStatus}'. " +
                $"Chỉ được upload khi hồ sơ ở trạng thái DRAFT hoặc NEED_MORE_DOCUMENTS.");
        }

        // ── 5. Kiểm tra trùng loại giấy tờ (1 loại/hồ sơ) ────────
        var duplicateExists = await _documentRepo.ExistsByApplicationAndTypeAsync(
            applicationId, request.DocumentType);

        if (duplicateExists)
        {
            _logger.LogWarning(
                "Application {AppId} already has document type '{DocType}'.",
                applicationId, request.DocumentType);
            throw InvalidDocumentFileException.DuplicateType(request.DocumentType);
        }

        // ── 6. Upload PDF lên Cloudinary ──────────────────────────
        string fileUrl;
        try
        {
            fileUrl = await _fileStorage.UploadPdfAsync(request.File, CloudinaryFolder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to upload PDF for application {AppId}.", applicationId);
            throw new InvalidOperationException(
                "Upload file thất bại. Vui lòng thử lại sau.", ex);
        }

        // ── 7. Lưu metadata vào DB ────────────────────────────────
        var now = DateTime.UtcNow;
        var document = new ApplicationDocument
        {
            DocumentId         = Guid.NewGuid(),
            ApplicationId      = applicationId,
            UploadedBy         = uploadedByUserId,
            DocumentType       = request.DocumentType,
            FileName           = request.File.FileName,
            FileUrl            = fileUrl,
            FileSizeBytes      = request.File.Length,
            UploadedAt         = now,
            VerificationStatus = "PENDING"
        };

        await _documentRepo.CreateAsync(document);

        _logger.LogInformation(
            "Document {DocumentId} (type={DocType}) uploaded successfully for application {AppId}.",
            document.DocumentId, document.DocumentType, applicationId);

        return new UploadDocumentResponseDto
        {
            DocumentId    = document.DocumentId,
            DocumentType  = document.DocumentType,
            FileName      = document.FileName,
            FileUrl       = document.FileUrl,
            FileSizeBytes = document.FileSizeBytes,
            UploadedAt    = document.UploadedAt,
            Message       = $"Upload thành công giấy tờ loại '{request.DocumentType}'."
        };
    }

    // ─────────────────────────────────────────────────────────────
    // Delete Document
    // ─────────────────────────────────────────────────────────────

    public async Task DeleteDocumentAsync(Guid documentId, Guid requestedByUserId)
    {
        _logger.LogInformation(
            "User {UserId} requesting delete of document {DocumentId}.",
            requestedByUserId, documentId);

        // 1. Tìm tài liệu
        var document = await _documentRepo.GetByIdAsync(documentId);
        if (document is null)
            throw new KeyNotFoundException($"Không tìm thấy tài liệu với ID '{documentId}'.");

        // 2. Kiểm tra trạng thái hồ sơ (chỉ được xóa khi DRAFT/NEED_MORE_DOCUMENTS)
        var appStatus = document.HousingApplication.ApplicationStatus;
        if (!EditableStatuses.Contains(appStatus))
        {
            throw new InvalidApplicationStatusTransitionException(
                appStatus,
                "DELETE_DOCUMENT",
                $"Không thể xóa tài liệu khi hồ sơ đang ở trạng thái '{appStatus}'.");
        }

        // 3. Kiểm tra quyền: chỉ người upload hoặc owner mới được xóa
        if (document.UploadedBy != requestedByUserId &&
            document.HousingApplication.ApplicantId != requestedByUserId)
        {
            throw new UnauthorizedAccessException(
                "Bạn không có quyền xóa tài liệu này.");
        }

        // 4. Xóa khỏi DB (file trên Cloudinary giữ lại để audit)
        await _documentRepo.DeleteAsync(document);

        _logger.LogInformation(
            "Document {DocumentId} deleted from application {AppId}.",
            documentId, document.ApplicationId);
    }
}
