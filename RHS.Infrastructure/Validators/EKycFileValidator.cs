using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Exceptions;

namespace RHS.Infrastructure.Validators;

/// <summary>
/// Validator chuyên biệt cho file ảnh eKYC trước khi gửi lên FPT AI API.
/// Kiểm tra 3 điều kiện: file không rỗng, đúng định dạng ảnh, và không vượt dung lượng.
/// </summary>
/// <remarks>
/// Class này được đăng ký là Singleton qua DI và tái sử dụng giữa các request.
/// </remarks>
public sealed class EKycFileValidator
{
    // ── Danh sách MIME type được chấp nhận ───────────────────────────────
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png"
    };

    // ── Danh sách phần mở rộng hợp lệ (backup nếu Content-Type sai) ─────
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png"
    };

    // ── Magic bytes để kiểm tra nội dung file thực sự ────────────────────
    // JPEG: FF D8 FF
    private static readonly byte[] JpegMagicBytes = [0xFF, 0xD8, 0xFF];
    // PNG: 89 50 4E 47 0D 0A 1A 0A
    private static readonly byte[] PngMagicBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly long _maxFileSizeBytes;
    private readonly long _maxLivenessFileSizeBytes;

    public EKycFileValidator(IOptions<FptAiOptions> options)
    {
        _maxFileSizeBytes         = options.Value.MaxFileSizeBytes;
        _maxLivenessFileSizeBytes = options.Value.MaxLivenessFileSizeBytes;
    }

    /// <summary>
    /// Kiểm tra đầy đủ một <see cref="IFormFile"/>. Ném exception ngay tại điều kiện đầu tiên thất bại.
    /// </summary>
    /// <param name="file">File ảnh cần kiểm tra.</param>
    /// <param name="fieldName">
    /// Tên trường (ví dụ: "Image", "FaceImage") để đưa vào thông báo lỗi.
    /// </param>
    /// <exception cref="EKycValidationException">
    /// Ném ra khi file null/rỗng, sai định dạng, hoặc vượt dung lượng.
    /// </exception>
    public async Task ValidateAsync(IFormFile file, string fieldName)
    {
        ValidateNotEmpty(file, fieldName);
        ValidateSize(file, fieldName, _maxFileSizeBytes);
        ValidateContentType(file, fieldName);
        await ValidateMagicBytesAsync(file, fieldName);
    }

    /// <summary>
    /// Kiểm tra file ảnh selfie cho Liveness Detection (cho phép tối đa 10 MB).
    /// Sử dụng <see cref="FptAiOptions.MaxLivenessFileSizeBytes"/> thay vì giới hạn 5 MB thông thường.
    /// </summary>
    public async Task ValidateLivenessAsync(IFormFile file, string fieldName)
    {
        ValidateNotEmpty(file, fieldName);
        ValidateSize(file, fieldName, _maxLivenessFileSizeBytes);
        ValidateContentType(file, fieldName);
        await ValidateMagicBytesAsync(file, fieldName);
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
    private static void ValidateContentType(IFormFile file, string fieldName)
    {
        var contentType = file.ContentType;
        var extension = Path.GetExtension(file.FileName);

        bool isContentTypeValid = AllowedContentTypes.Contains(contentType);
        bool isExtensionValid   = AllowedExtensions.Contains(extension);

        if (!isContentTypeValid || !isExtensionValid)
            throw EKycValidationException.InvalidFormat(fieldName, contentType);
    }

    /// <summary>
    /// Đọc và kiểm tra magic bytes (file signature) thực sự trong file.
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
