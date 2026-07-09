using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO cho Applicant tự hủy hồ sơ (Task #11).
/// </summary>
public class CancelApplicationRequestDto
{
    /// <summary>
    /// Lý do hủy hồ sơ. Bắt buộc phải nhập.
    /// </summary>
    [Required(ErrorMessage = "Lý do hủy hồ sơ là bắt buộc.")]
    [MaxLength(1000, ErrorMessage = "Lý do hủy không được quá 1000 ký tự.")]
    public string CancelReason { get; set; } = string.Empty;
}
