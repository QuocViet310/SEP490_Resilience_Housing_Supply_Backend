namespace RHS.Infrastructure.Exceptions;

/// <summary>
/// Ném ra khi có lỗi khi tích hợp với eKYC Provider API (VNPT eKYC):
/// <list type="bullet">
///   <item>Provider trả về HTTP status code không thành công (4xx / 5xx).</item>
///   <item>Provider trả về body hợp lệ nhưng <c>errorCode != 0</c>.</item>
///   <item>Request bị timeout hoặc mất kết nối mạng.</item>
///   <item>Không parse được JSON response.</item>
///   <item>AccessToken hết hạn (HTTP 401) — cần lấy token mới từ Dashboard.</item>
/// </list>
/// Controller nên bắt exception này và trả về HTTP 502 hoặc 500.
/// </summary>
public sealed class EKycIntegrationException : EKycException
{
    // ── Mã lỗi nội bộ (dùng làm hằng số tránh magic string) ─────────────

    /// <summary>eKYC Provider trả về HTTP error status.</summary>
    public const string CodeHttpError = "EKYC_HTTP_ERROR";

    /// <summary>eKYC Provider trả về errorCode khác 0 trong body JSON.</summary>
    public const string CodeApiError = "EKYC_API_ERROR";

    /// <summary>Request tới eKYC Provider bị timeout.</summary>
    public const string CodeTimeout = "EKYC_TIMEOUT";

    /// <summary>Không parse được JSON response từ eKYC Provider.</summary>
    public const string CodeInvalidResponse = "EKYC_INVALID_RESPONSE";

    /// <summary>
    /// AccessToken đã hết hạn (VNPT: 8 tiếng).
    /// Admin cần vào Dashboard VNPT lấy token mới và cập nhật cấu hình.
    /// </summary>
    public const string CodeTokenExpired = "EKYC_TOKEN_EXPIRED";

    // ── Properties bổ sung ───────────────────────────────────────────────

    /// <summary>
    /// HTTP status code nhận được từ eKYC Provider (null nếu lỗi không phải HTTP-level).
    /// </summary>
    public int? HttpStatusCode { get; }

    /// <summary>
    /// Error code gốc từ eKYC Provider (giá trị của trường "errorCode" trong JSON response).
    /// </summary>
    public int? ProviderErrorCode { get; }

    // ── Constructors ─────────────────────────────────────────────────────

    /// <summary>Tạo exception cho lỗi HTTP-level (4xx/5xx từ eKYC Provider).</summary>
    public EKycIntegrationException(int httpStatusCode, string message)
        : base(CodeHttpError, message)
    {
        HttpStatusCode = httpStatusCode;
    }

    /// <summary>Tạo exception khi eKYC Provider trả về errorCode != 0 trong body JSON.</summary>
    public EKycIntegrationException(int providerErrorCode, string providerErrorMessage, bool isProviderCode)
        : base(CodeApiError, $"eKYC API lỗi (errorCode={providerErrorCode}): {providerErrorMessage}")
    {
        ProviderErrorCode = providerErrorCode;
        _ = isProviderCode; // disambiguating parameter
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
