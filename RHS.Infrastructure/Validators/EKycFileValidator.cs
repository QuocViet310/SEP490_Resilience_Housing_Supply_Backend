using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Exceptions;

namespace RHS.Infrastructure.Validators;

/// <summary>
/// Validator chuyên biệt cho file ảnh eKYC trước khi gửi lên VNPT eKYC API.
/// - Ảnh (OCR, FaceMatch): kiểm tra MIME type, extension, magic bytes.
/// </summary>
/// <remarks>
/// Class này được đăng ký là Singleton qua DI và tái sử dụng giữa các request.
/// </remarks>
public sealed class EKycFileValidator
{
    // ── Danh sách MIME type ảnh được chấp nhận ───────────────────────────
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png"
    };

    // ── Danh sách phần mở rộng ảnh hợp lệ ───────────────────────────────
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png"
    };

    // ── Danh sách MIME type video được chấp nhận (Liveness Detection) ────
    private static readonly HashSet<string> AllowedVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4",
        "video/avi",
        "video/x-msvideo",    // avi alternative
        "video/quicktime",    // .mov
        "video/x-ms-wmv"      // .wmv
    };

    // ── Danh sách phần mở rộng video hợp lệ ─────────────────────────────
    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".avi",
        ".mov",
        ".wmv"
    };

    // ── Magic bytes để kiểm tra nội dung file ảnh ────────────────────────
    // JPEG: FF D8 FF
    private static readonly byte[] JpegMagicBytes = [0xFF, 0xD8, 0xFF];
    // PNG: 89 50 4E 47 0D 0A 1A 0A
    private static readonly byte[] PngMagicBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly long _maxFileSizeBytes;
    private readonly long _maxVideoFileSizeBytes;

    public EKycFileValidator(IOptions<VnptEKycOptions> options)
    {
        _maxFileSizeBytes       = options.Value.MaxFileSizeBytes;
        _maxVideoFileSizeBytes  = options.Value.MaxFileSizeBytes; // VNPT không hỗ trợ Liveness video qua REST API
    }

    /// <summary>
    /// Kiểm tra đầy đủ một <see cref="IFormFile"/> ảnh (JPEG/PNG).
    /// Ném exception ngay tại điều kiện đầu tiên thất bại.
    /// </summary>
    public async Task ValidateAsync(IFormFile file, string fieldName)
    {
        ValidateNotEmpty(file, fieldName);
        ValidateSize(file, fieldName, _maxFileSizeBytes);
        ValidateContentType(file, fieldName, AllowedImageContentTypes, AllowedImageExtensions);
        await ValidateMagicBytesAsync(file, fieldName);
    }

    /// <summary>
    /// Kiểm tra file VIDEO (mp4/avi/mov) cho Liveness Detection API.
    /// Giới hạn 10 MB (lớn hơn ảnh thông thường).
    /// Chỉ kiểm tra MIME type và extension (không check magic bytes vì cấu trúc video phức tạp).
    /// </summary>
    public Task ValidateVideoAsync(IFormFile file, string fieldName)
    {
        ValidateNotEmpty(file, fieldName);
        ValidateSize(file, fieldName, _maxVideoFileSizeBytes);
        ValidateContentType(file, fieldName, AllowedVideoContentTypes, AllowedVideoExtensions);
        return Task.CompletedTask;
    }

    // ── Private validation steps ─────────────────────────────────────────

    /// <summary>Kiểm tra file không null và có nội dung.</summary>
    private static void ValidateNotEmpty(IFormFile file, string fieldName)
    {
        if (file is null || file.Length == 0)
            throw EKycValidationException.EmptyFile(fieldName);
    }

    /// <summary>Kiểm tra dung lượng file không vượt quá giới hạn cho trước.</summary>
    private static void ValidateSize(IFormFile file, string fieldName, long maxSizeBytes)
    {
        if (file.Length > maxSizeBytes)
            throw EKycValidationException.FileTooLarge(fieldName, file.Length, maxSizeBytes);
    }

    /// <summary>
    /// Kiểm tra Content-Type header VÀ phần mở rộng file.
    /// Cần cả hai hợp lệ để tránh tấn công giả mạo MIME type.
    /// </summary>
    private static void ValidateContentType(
        IFormFile file,
        string fieldName,
        HashSet<string> allowedContentTypes,
        HashSet<string> allowedExtensions)
    {
        var contentType = file.ContentType;
        var extension   = Path.GetExtension(file.FileName);

        bool isContentTypeValid = allowedContentTypes.Contains(contentType);
        bool isExtensionValid   = allowedExtensions.Contains(extension);

        if (!isContentTypeValid || !isExtensionValid)
            throw EKycValidationException.InvalidFormat(fieldName, contentType);
    }

    /// <summary>
    /// Đọc và kiểm tra magic bytes (file signature) thực sự trong file ảnh.
    /// Ngăn chặn trường hợp đổi tên file để vượt kiểm tra Content-Type.
    /// </summary>
    private static async Task ValidateMagicBytesAsync(IFormFile file, string fieldName)
    {
        // Đọc tối đa 8 byte đầu tiên (đủ cho cả JPEG và PNG signature)
        var buffer = new byte[8];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));

        if (IsJpeg(buffer, bytesRead) || IsPng(buffer, bytesRead))
            return;

        throw EKycValidationException.InvalidFormat(
            fieldName,
            $"{file.ContentType} (nội dung file không khớp với định dạng ảnh hợp lệ)");
    }

    private static bool IsJpeg(byte[] buffer, int bytesRead)
    {
        if (bytesRead < JpegMagicBytes.Length) return false;
        return buffer.Take(JpegMagicBytes.Length).SequenceEqual(JpegMagicBytes);
    }

    private static bool IsPng(byte[] buffer, int bytesRead)
    {
        if (bytesRead < PngMagicBytes.Length) return false;
        return buffer.Take(PngMagicBytes.Length).SequenceEqual(PngMagicBytes);
    }
}
