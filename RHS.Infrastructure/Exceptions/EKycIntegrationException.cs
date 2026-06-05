namespace RHS.Infrastructure.Exceptions;

/// <summary>
/// Ném ra khi có lỗi khi tích hợp với FPT AI API:
/// <list type="bullet">
///   <item>FPT AI trả về HTTP status code không thành công (4xx / 5xx).</item>
///   <item>FPT AI trả về body hợp lệ nhưng <c>errorCode != 0</c>.</item>
///   <item>Request bị timeout hoặc mất kết nối mạng.</item>
///   <item>Không parse được JSON response từ FPT AI.</item>
/// </list>
/// Controller nên bắt exception này và trả về HTTP 502 hoặc 500.
/// </summary>
public sealed class EKycIntegrationException : EKycException
{
    // ── Mã lỗi nội bộ (dùng làm hằng số tránh magic string) ─────────────

    /// <summary>FPT AI trả về HTTP error status.</summary>
    public const string CodeHttpError = "EKYC_HTTP_ERROR";

    /// <summary>FPT AI trả về errorCode khác 0 trong body JSON.</summary>
    public const string CodeApiError = "EKYC_API_ERROR";

    /// <summary>Request tới FPT AI bị timeout.</summary>
    public const string CodeTimeout = "EKYC_TIMEOUT";

    /// <summary>Không parse được JSON response từ FPT AI.</summary>
    public const string CodeInvalidResponse = "EKYC_INVALID_RESPONSE";

    // ── Properties bổ sung ───────────────────────────────────────────────

    /// <summary>
    /// HTTP status code nhận được từ FPT AI (null nếu lỗi không phải HTTP-level).
    /// </summary>
    public int? HttpStatusCode { get; }

    /// <summary>
    /// Error code gốc từ FPT AI (giá trị của trường "errorCode" trong JSON response).
    /// </summary>
    public int? FptAiErrorCode { get; }

    // ── Constructors ─────────────────────────────────────────────────────

    /// <summary>Tạo exception cho lỗi HTTP-level (4xx/5xx từ FPT AI).</summary>
    public EKycIntegrationException(int httpStatusCode, string message)
        : base(CodeHttpError, message)
    {
        HttpStatusCode = httpStatusCode;
    }

    /// <summary>Tạo exception khi FPT AI trả về errorCode != 0 trong body JSON.</summary>
    public EKycIntegrationException(int fptAiErrorCode, string fptAiErrorMessage, bool isFptAiCode)
        : base(CodeApiError, $"FPT AI API lỗi (errorCode={fptAiErrorCode}): {fptAiErrorMessage}")
    {
        FptAiErrorCode = fptAiErrorCode;
        _ = isFptAiCode; // disambiguating parameter
    }

    /// <summary>Tạo exception cho lỗi timeout hoặc mất kết nối.</summary>
    public EKycIntegrationException(string errorCode, string message, Exception innerException)
        : base(errorCode, message, innerException)
    {
    }

    /// <summary>Tạo exception tổng quát với errorCode tùy chỉnh.</summary>
    public EKycIntegrationException(string errorCode, string message)
        : base(errorCode, message)
    {
    }
}
