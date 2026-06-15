namespace RHS.Infrastructure.Configurations;

/// <summary>
/// Strongly-typed configuration class ánh xạ từ section "FptAi" trong appsettings.json.
/// Được inject qua IOptions&lt;FptAiOptions&gt; vào <see cref="RHS.Infrastructure.Services.FptEKycService"/>.
/// </summary>
public sealed class FptAiOptions
{
    /// <summary>Tên section trong appsettings.json.</summary>
    public const string SectionName = "FptAi";

    /// <summary>API Key cấp bởi FPT AI Console (https://console.fpt.ai).</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Endpoint OCR nhận dạng Căn cước công dân.</summary>
    public string OcrEndpoint { get; init; } = "https://api.fpt.ai/vision/idr/vnm";

    /// <summary>Endpoint so khớp khuôn mặt (selfie vs CCCD photo).</summary>
    public string FaceMatchEndpoint { get; init; } = "https://api.fpt.ai/vision/faceapi/facematch";

    /// <summary>Endpoint kiểm tra liveness — phát hiện ảnh selfie giả mạo (spoofing).</summary>
    public string LivenessEndpoint { get; init; } = "https://api.fpt.ai/dmp/checkface/v1";

    /// <summary>Timeout (giây) cho mỗi HTTP request tới FPT AI. Mặc định 30 giây.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Giới hạn kích thước file tải lên cho OCR và Face Match (bytes). Mặc định 5 MB.</summary>
    public long MaxFileSizeBytes { get; init; } = 5_242_880; // 5 MB

    /// <summary>
    /// Giới hạn kích thước ảnh selfie cho Liveness Detection (bytes). Mặc định 10 MB.
    /// Lớn hơn OCR/FaceMatch vì ảnh selfie thường có độ phân giải cao hơn.
    /// </summary>
    public long MaxLivenessFileSizeBytes { get; init; } = 10_485_760; // 10 MB
}

