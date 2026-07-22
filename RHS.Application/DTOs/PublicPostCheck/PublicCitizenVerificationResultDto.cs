namespace RHS.Application.DTOs.PublicPostCheck;

/// <summary>
/// DTO kết quả tra cứu nhanh theo CCCD chính xác cho công dân / cơ quan giám sát
/// </summary>
public class PublicCitizenVerificationResultDto
{
    /// <summary>
    /// Tìm thấy thông tin đã mua NOXH hay chưa
    /// </summary>
    public bool IsFound { get; set; }

    /// <summary>
    /// Số CCCD đã tra cứu
    /// </summary>
    public string SearchedCitizenId { get; set; } = string.Empty;

    /// <summary>
    /// Số lượng suất nhà ở xã hội đã được phân bổ
    /// </summary>
    public int TotalHousingAllocated { get; set; }

    /// <summary>
    /// Thông điệp xác minh pháp lý
    /// </summary>
    public string VerificationMessage { get; set; } = string.Empty;

    /// <summary>
    /// Danh sách các căn NOXH đã được phân bổ thành công
    /// </summary>
    public List<PublicPostCheckListItemDto> HousingAllocations { get; set; } = new();
}
