namespace RHS.Application.DTOs.EKyc;

// ─────────────────────────────────────────────────────────────────────────────
//  OCR Response
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Kết quả trả về sau khi gọi API OCR của FPT AI.
/// Chứa thông tin đã được trích xuất từ ảnh Căn cước công dân.
/// </summary>
public sealed record OcrIdCardResponse
{
    /// <summary>Mã lỗi từ FPT AI (0 = thành công).</summary>
    public int ErrorCode { get; init; }

    /// <summary>Mô tả lỗi từ FPT AI.</summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>Thông tin CCCD đã được trích xuất (null nếu xảy ra lỗi).</summary>
    public OcrIdCardData? Data { get; init; }
}

/// <summary>
/// Chi tiết thông tin được trích xuất từ Căn cước công dân.
/// Ánh xạ 1-1 với phần tử đầu tiên trong mảng "data" từ FPT AI API.
/// </summary>
public sealed record OcrIdCardData
{
    /// <summary>Số CCCD / CMND.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Họ và tên chủ thẻ.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Ngày sinh (định dạng dd/MM/yyyy).</summary>
    public string Dob { get; init; } = string.Empty;

    /// <summary>Giới tính (Nam / Nữ).</summary>
    public string Sex { get; init; } = string.Empty;

    /// <summary>Quốc tịch.</summary>
    public string Nationality { get; init; } = string.Empty;

    /// <summary>Quê quán.</summary>
    public string Home { get; init; } = string.Empty;

    /// <summary>Nơi đăng ký hộ khẩu thường trú.</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>Ngày hết hạn (Date of Expiry), định dạng dd/MM/yyyy.</summary>
    public string Doe { get; init; } = string.Empty;

    /// <summary>Ngày cấp (Issue Date), định dạng dd/MM/yyyy.</summary>
    public string IssueDate { get; init; } = string.Empty;

    /// <summary>Nơi cấp.</summary>
    public string IssueLoc { get; init; } = string.Empty;

    /// <summary>
    /// Loại mặt thẻ được nhận diện:
    /// "new_front" (mặt trước CCCD gắn chip),
    /// "new_back"  (mặt sau CCCD gắn chip),
    /// "old_front" (mặt trước CMND cũ).
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Điểm tin cậy tổng thể của kết quả OCR (0.0 – 1.0).</summary>
    public double OverallScore { get; init; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Face Match Response
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Kết quả trả về sau khi gọi API Face Match của FPT AI.
/// </summary>
public sealed record FaceMatchResponse
{
    /// <summary>HTTP status code phản hồi từ FPT AI (ví dụ "200").</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Hai ảnh có khớp nhau hay không (ngưỡng ≥ 80% similarity).</summary>
    public bool IsMatch { get; init; }

    /// <summary>Độ tương đồng khuôn mặt (0.0 – 100.0). Ngưỡng thường dùng ≥ 80.</summary>
    public double Similarity { get; init; }

    /// <summary>
    /// Cả 2 ảnh upload đều là ảnh CCCD hay không.
    /// <c>true</c> = cả 2 ảnh đều là CCCD, <c>false</c> = ít nhất 1 ảnh không phải CCCD.
    /// (Tương ứng field <c>isBothImgIDCard</c> trong FPT AI response.)
    /// </summary>
    public bool IsBothImgIdCard { get; init; }

    /// <summary>Thông điệp từ FPT AI (ví dụ "request successful.").</summary>
    public string FptMessage { get; init; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
//  Liveness Detection Response
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Kết quả trả về sau khi gọi API Liveness Detection của FPT AI.
/// Xác định ảnh selfie có phải người thật chụp trực tiếp hay đang bị giả mạo.
/// </summary>
public sealed record LivenessDetectionResponse
{
    /// <summary>HTTP status code phản hồi từ FPT AI (ví dụ "200").</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Ảnh selfie có phải người thật hay không.
    /// <c>true</c> = hợp lệ (liveness passed), <c>false</c> = phát hiện giả mạo (spoofing).
    /// </summary>
    public bool IsLive { get; init; }

    /// <summary>
    /// Điểm tin cậy liveness (0.0 – 1.0).
    /// Giá trị càng cao càng chắc chắn là người thật. Ngưỡng khuyến nghị ≥ 0.70.
    /// </summary>
    public double LivenessScore { get; init; }

    /// <summary>Thông điệp mô tả kết quả từ FPT AI.</summary>
    public string Message { get; init; } = string.Empty;
}
