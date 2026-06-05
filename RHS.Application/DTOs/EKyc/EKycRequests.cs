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
/// Request gửi lên endpoint Liveness Detection để kiểm tra ảnh selfie
/// có phải người thật chụp trực tiếp hay không (chống spoofing).
/// </summary>
public sealed record LivenessDetectionRequest
{
    /// <summary>
    /// Ảnh selfie của người dùng (chụp trực tiếp từ camera).
    /// Phải là JPEG/PNG, dung lượng ≤ 5 MB.
    /// </summary>
    public required IFormFile FaceImage { get; init; }
}

