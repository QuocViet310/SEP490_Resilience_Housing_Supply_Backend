using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO cho CĐT gửi danh sách hồ sơ đã duyệt lên Sở Xây dựng (Task #7).
/// Payload chứa danh sách ApplicationIds cần chuyển trạng thái REVIEWING → PENDING_SXD_REVIEW.
/// </summary>
public class SubmitToDepartmentRequestDto
{
    /// <summary>
    /// Danh sách ID hồ sơ cần gửi lên Sở Xây dựng.
    /// Tất cả hồ sơ phải đang ở trạng thái REVIEWING và thuộc dự án của CĐT.
    /// </summary>
    [Required(ErrorMessage = "Danh sách hồ sơ là bắt buộc.")]
    [MinLength(1, ErrorMessage = "Phải có ít nhất 1 hồ sơ trong danh sách.")]
    public List<Guid> ApplicationIds { get; set; } = new();
}
