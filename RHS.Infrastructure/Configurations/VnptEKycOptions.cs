namespace RHS.Infrastructure.Configurations;

/// <summary>
/// Strongly-typed configuration class ánh xạ từ section "VnptEKyc" trong appsettings.json.
/// Được inject qua IOptions&lt;VnptEKycOptions&gt; vào <see cref="RHS.Infrastructure.Services.VnptEKycService"/>.
/// </summary>
/// <remarks>
/// <para><b>Hướng dẫn lấy credentials VNPT eKYC:</b></para>
/// <list type="number">
///   <item>Truy cập cổng đối tác VNPT AI: <c>https://ekyc.vnpt.vn</c> hoặc <c>https://vnptai.vn</c>.</item>
///   <item>Đăng ký tài khoản doanh nghiệp (Business Account) và chờ phê duyệt.</item>
///   <item>Sau khi được phê duyệt, vào phần <b>Dashboard → API Keys</b> để lấy:
///         <list type="bullet">
///           <item><c>AccessToken</c> — Bearer token dùng cho header <c>Authorization</c>.</item>
///           <item><c>TokenId</c> — Mã định danh bảo mật, truyền qua header <c>Token-id</c>.</item>
///           <item><c>TokenKey</c> — Khóa bảo mật, truyền qua header <c>Token-key</c>.</item>
///         </list>
///   </item>
///   <item>Nếu VNPT cung cấp endpoint OAuth (<c>/oauth/token</c>), gọi endpoint đó
///         bằng client_id + client_secret để lấy access_token tự động.</item>
/// </list>
/// <para><b>Xác định Base URL:</b></para>
/// <list type="bullet">
///   <item>Production: thường là <c>https://api.idg.vnpt.vn</c>.</item>
///   <item>Sandbox/Staging: kiểm tra tài liệu hoặc Dashboard VNPT để lấy URL chính xác
///         (có thể là <c>https://api-uat.idg.vnpt.vn</c> hoặc domain riêng).</item>
///   <item>Sau khi đăng nhập Dashboard VNPT, vào phần <b>Integration Guide</b> hoặc
///         <b>Engine Specs</b> để xác nhận Base URL cho môi trường của bạn.</item>
/// </list>
/// </remarks>
public sealed class VnptEKycOptions
{
    /// <summary>Tên section trong appsettings.json.</summary>
    public const string SectionName = "VnptEKyc";

    /// <summary>
    /// Base URL của VNPT eKYC API.
    /// <para>Mặc định: <c>https://api.idg.vnpt.vn</c> (production).</para>
    /// <para>Để xác định đúng URL cho môi trường của bạn, đăng nhập vào
    /// Dashboard VNPT AI và kiểm tra phần Integration Guide.</para>
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.idg.vnpt.vn";

    /// <summary>
    /// Access Token (Bearer) cho VNPT API.
    /// Lấy từ Dashboard VNPT AI hoặc gọi endpoint OAuth <c>/oauth/token</c>.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// Token ID bảo mật — header <c>Token-id</c>.
    /// Được cấp trong phần API Keys trên Dashboard VNPT AI.
    /// </summary>
    public string TokenId { get; init; } = string.Empty;

    /// <summary>
    /// Token Key bảo mật — header <c>Token-key</c>.
    /// Được cấp trong phần API Keys trên Dashboard VNPT AI.
    /// </summary>
    public string TokenKey { get; init; } = string.Empty;

    // ── Endpoints (đường dẫn tương đối so với BaseUrl) ──────────────────

    /// <summary>Endpoint upload file để lấy hash. Mặc định: <c>/file-service/v1/addFile</c>.</summary>
    public string UploadEndpoint { get; init; } = "/file-service/v1/addFile";

    /// <summary>Endpoint OCR bóc tách thông tin giấy tờ. Mặc định: <c>/ai/v1/ocr/id</c>.</summary>
    public string OcrEndpoint { get; init; } = "/ai/v1/ocr/id";

    /// <summary>Endpoint so khớp khuôn mặt. Mặc định: <c>/ai/v1/face/compare</c>.</summary>
    public string FaceCompareEndpoint { get; init; } = "/ai/v1/face/compare";

    // ── Thresholds & limits ─────────────────────────────────────────────

    /// <summary>
    /// Ngưỡng similarity tối thiểu để tự động xác thực danh tính (0–100).
    /// Nếu kết quả face match ≥ ngưỡng này, thông tin CCCD sẽ được tự động lưu vào Profile.
    /// Mặc định: 85%.
    /// </summary>
    public double FaceMatchThreshold { get; init; } = 85.0;

    /// <summary>Timeout (giây) cho mỗi HTTP request tới VNPT API. Mặc định 30 giây.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Giới hạn kích thước file ảnh tải lên (bytes). Mặc định 5 MB.</summary>
    public long MaxFileSizeBytes { get; init; } = 5_242_880; // 5 MB

    /// <summary>
    /// Giá trị header <c>mac-address</c> yêu cầu bởi VNPT cho các API OCR và Face Compare.
    /// Mặc định: <c>TEST1</c> (dùng cho môi trường sandbox/testing).
    /// </summary>
    public string MacAddress { get; init; } = "TEST1";
}
