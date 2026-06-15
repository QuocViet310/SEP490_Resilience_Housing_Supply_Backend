using Microsoft.AspNetCore.Http;

namespace RHS.Application.DTOs.EKyc;

/// <summary>
/// Request gửi lên endpoint OCR để trích xuất thông tin từ ảnh CCCD.
/// </summary>
public sealed record OcrIdCardRequest
{
    /// <summary>
    /// Ảnh mặt trước hoặc mặt sau của Căn cước công dân.
    /// Phải là JPEG/PNG, dung lượng ≤ 5 MB.
    /// </summary>
    public required IFormFile Image { get; init; }
}

/// <summary>
/// Request gửi lên endpoint Face Match để so sánh khuôn mặt selfie với ảnh trên CCCD.
/// </summary>
public sealed record FaceMatchRequest
{
    /// <summary>Ảnh selfie của người dùng (chụp trực tiếp).</summary>
    public required IFormFile FaceImage { get; init; }

    /// <summary>Ảnh chân dung trích xuất từ CCCD (ảnh in trên thẻ).</summary>
    public required IFormFile IdCardImage { get; init; }
}

/// <summary>
/// Request gửi lên endpoint Liveness Detection để xác minh người thật qua video.
/// FPT AI Liveness API v3 yêu cầu VIDEO + ảnh khuôn mặt (CMND) để chống spoofing/deepfake.
/// </summary>
public sealed record LivenessDetectionRequest
{
    /// <summary>
    /// Video selfie của người dùng (quay trực tiếp từ camera).
    /// Định dạng hỗ trợ: MP4, AVI, MOV. Thời lượng khuyến nghị: 3–5 giây.
    /// Field name gửi lên FPT AI: <c>video</c>.
    /// </summary>
    public required IFormFile VideoFile { get; init; }

    /// <summary>
    /// Ảnh khuôn mặt của người dùng (chụp từ camera/selfie).
    /// Field name gửi lên FPT AI: <c>cmnd</c>.
    /// </summary>
    public required IFormFile CmndImage { get; init; }
}

