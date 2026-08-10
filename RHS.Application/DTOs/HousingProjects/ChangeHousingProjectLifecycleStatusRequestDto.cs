using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.HousingProjects;

/// <summary>
/// Đổi nhanh trạng thái vòng đời dự án (mở/đóng nhận hồ sơ) — dùng vận hành / demo.
/// Không thay cho luồng SXD APPROVE/REJECT từ PENDING.
/// </summary>
public class ChangeHousingProjectLifecycleStatusRequestDto
{
    /// <summary>
    /// Mã trạng thái đích: UPCOMING | OPEN | CLOSED | FULL
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string StatusCode { get; set; } = string.Empty;

    /// <summary>
    /// Ghi chú tùy chọn (ví dụ lý do đóng sớm khi demo).
    /// </summary>
    [MaxLength(500)]
    public string? Note { get; set; }
}
