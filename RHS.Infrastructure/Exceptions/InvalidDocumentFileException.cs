namespace RHS.Infrastructure.Exceptions;

/// <summary>
/// Ném ra khi file upload không hợp lệ trong luồng nộp giấy tờ hồ sơ.
/// Bao gồm: sai định dạng (không phải PDF), vượt kích thước, hoặc file rỗng.
/// Controller nên bắt exception này và trả về HTTP 400 (Bad Request).
/// </summary>
public sealed class InvalidDocumentFileException : HousingApplicationException
{
    // ── Mã lỗi nội bộ ────────────────────────────────────────────

    /// <summary>File không phải định dạng PDF.</summary>
    public const string CodeNotPdf = "DOC_NOT_PDF";

    /// <summary>File vượt quá kích thước tối đa cho phép.</summary>
    public const string CodeFileTooLarge = "DOC_FILE_TOO_LARGE";

    /// <summary>File rỗng hoặc null.</summary>
    public const string CodeEmptyFile = "DOC_EMPTY_FILE";

    /// <summary>Loại giấy tờ không hợp lệ (không thuộc danh sách cho phép).</summary>
    public const string CodeInvalidDocumentType = "DOC_INVALID_TYPE";

    /// <summary>
    /// Hồ sơ đã có giấy tờ cùng loại này.
    /// Applicant chỉ được upload 1 trong 2 loại giấy tờ chính.
    /// </summary>
    public const string CodeDuplicateDocumentType = "DOC_DUPLICATE_TYPE";

    // ── Properties ────────────────────────────────────────────────

    /// <summary>Tên file gốc gây lỗi (để log).</summary>
    public string? FileName { get; }

    // ── Factory methods ──────────────────────────────────────────

    /// <summary>File không phải PDF.</summary>
    public static InvalidDocumentFileException NotPdf(string fileName) =>
        new(CodeNotPdf, $"File '{fileName}' không hợp lệ. Chỉ chấp nhận định dạng PDF (.pdf).", fileName);

    /// <summary>File vượt quá kích thước tối đa.</summary>
    public static InvalidDocumentFileException TooLarge(string fileName, long maxSizeMb) =>
        new(CodeFileTooLarge,
            $"File '{fileName}' vượt quá kích thước tối đa {maxSizeMb}MB.", fileName);

    /// <summary>File rỗng hoặc không có dữ liệu.</summary>
    public static InvalidDocumentFileException EmptyFile(string fileName) =>
        new(CodeEmptyFile, $"File '{fileName}' rỗng hoặc không có dữ liệu.", fileName);

    /// <summary>Loại giấy tờ không thuộc danh sách cho phép.</summary>
    public static InvalidDocumentFileException InvalidType(string documentType) =>
        new(CodeInvalidDocumentType,
            $"Loại giấy tờ '{documentType}' không hợp lệ. " +
            "Chỉ chấp nhận: HOUSING_CONDITION_PROOF hoặc POVERTY_HOUSEHOLD_CERTIFICATE.",
            null);

    /// <summary>
    /// Hồ sơ đã có giấy tờ cùng loại.
    /// Thông báo nhắc người dùng rằng họ chỉ được chọn 1 trong 2 loại.
    /// </summary>
    public static InvalidDocumentFileException DuplicateType(string documentType) =>
        new(CodeDuplicateDocumentType,
            $"Hồ sơ đã có giấy tờ loại '{documentType}'. " +
            "Bạn chỉ được upload một loại giấy tờ chứng minh duy nhất. " +
            "Vui lòng xóa giấy tờ cũ trước khi upload mới.",
            null);

    // ── Private constructor ───────────────────────────────────────

    private InvalidDocumentFileException(string errorCode, string message, string? fileName)
        : base(errorCode, message)
    {
        FileName = fileName;
    }
}
