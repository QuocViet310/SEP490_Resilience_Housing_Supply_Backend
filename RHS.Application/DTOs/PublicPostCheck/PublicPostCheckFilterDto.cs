namespace RHS.Application.DTOs.PublicPostCheck;

/// <summary>
/// DTO chứa tham số lọc và phân trang cho Trang tra cứu hậu kiểm công khai (Public Portal)
/// </summary>
public class PublicPostCheckFilterDto
{
    /// <summary>
    /// Tìm theo từ khóa (Tên người dân, số CCCD, mã căn)
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Tìm chính xác số CCCD (12 số)
    /// </summary>
    public string? CitizenId { get; set; }

    /// <summary>
    /// Lọc theo dự án nhà ở xã hội
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Lọc theo Tỉnh/Thành phố dự án
    /// </summary>
    public string? Province { get; set; }

    /// <summary>
    /// Lọc theo Quận/Huyện dự án
    /// </summary>
    public string? District { get; set; }

    /// <summary>
    /// Lọc theo năm phê duyệt / nộp cọc
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Trang hiện tại (mặc định: 1)
    /// </summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>
    /// Số lượng dòng mỗi trang (mặc định: 10, tối đa: 50)
    /// </summary>
    public int PageSize { get; set; } = 10;
}
