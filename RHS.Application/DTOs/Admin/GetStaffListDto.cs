namespace RHS.Application.DTOs.Admin;

/// <summary>
/// DTO để lấy danh sách cán bộ với phân trang
/// </summary>
public class GetStaffListDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Role { get; set; } // Lọc theo vai trò (Department Of Construction, Housing Developer)
    public string? Status { get; set; } // Lọc theo trạng thái (Active, Inactive, Suspended)
    public string? SearchTerm { get; set; } // Tìm kiếm theo email hoặc tên
}

/// <summary>
/// DTO để trả về danh sách cán bộ
/// </summary>
public class StaffListResponseDto
{
    public List<StaffResponseDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}
