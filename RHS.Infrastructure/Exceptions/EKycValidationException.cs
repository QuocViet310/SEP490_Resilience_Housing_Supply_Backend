namespace RHS.Infrastructure.Exceptions;

/// <summary>
/// Ném ra khi file ảnh do người dùng tải lên không vượt qua bước validation:
/// <list type="bullet">
///   <item>File null hoặc rỗng (0 byte).</item>
///   <item>Định dạng file không được hỗ trợ (chỉ chấp nhận JPEG và PNG).</item>
///   <item>Dung lượng file vượt quá giới hạn cho phép (mặc định 5 MB).</item>
/// </list>
/// Controller nên bắt exception này và trả về HTTP 400 Bad Request.
/// </summary>
public sealed class EKycValidationException : EKycException
{
    // ── Mã lỗi nội bộ ────────────────────────────────────────────────────

    /// <summary>File null hoặc không có nội dung.</summary>
    public const string CodeEmptyFile = "EKYC_EMPTY_FILE";

    /// <summary>Định dạng MIME type không được hỗ trợ.</summary>
    public const string CodeInvalidFormat = "EKYC_INVALID_FORMAT";

    /// <summary>Dung lượng file vượt quá giới hạn cho phép.</summary>
    public const string CodeFileTooLarge = "EKYC_FILE_TOO_LARGE";

    // ── Properties bổ sung ───────────────────────────────────────────────

    /// <summary>Tên tham số (field) bị lỗi, ví dụ: "Image", "FaceImage", "IdCardImage".</summary>
    public string FieldName { get; }

    // ── Constructors ─────────────────────────────────────────────────────

    /// <summary>Tạo validation exception với errorCode và tên field cụ thể.</summary>
    /// <param name="errorCode">Một trong các hằng số <c>Code*</c> của class này.</param>
    /// <param name="fieldName">Tên field bị lỗi (dùng để trả về lỗi có ngữ cảnh cho client).</param>
    /// <param name="message">Thông điệp mô tả lỗi.</param>
    public EKycValidationException(string errorCode, string fieldName, string message)
        : base(errorCode, message)
    {
        FieldName = fieldName;
    }

    // ── Factory methods (giúp code ở nơi khác ngắn gọn hơn) ─────────────

    /// <summary>Tạo exception cho trường hợp file null/rỗng.</summary>
    public static EKycValidationException EmptyFile(string fieldName)
        => new(CodeEmptyFile, fieldName, $"Trường '{fieldName}': file không được để trống.");

    /// <summary>Tạo exception cho trường hợp định dạng file không hợp lệ.</summary>
    public static EKycValidationException InvalidFormat(string fieldName, string actualContentType)
        => new(CodeInvalidFormat, fieldName,
            $"Trường '{fieldName}': định dạng '{actualContentType}' không được hỗ trợ. Chỉ chấp nhận JPEG và PNG.");

    /// <summary>Tạo exception cho trường hợp file quá lớn.</summary>
    public static EKycValidationException FileTooLarge(string fieldName, long actualBytes, long maxBytes)
        => new(CodeFileTooLarge, fieldName,
            $"Trường '{fieldName}': dung lượng {actualBytes / 1024.0 / 1024.0:F2} MB vượt quá giới hạn {maxBytes / 1024.0 / 1024.0:F0} MB.");
}
