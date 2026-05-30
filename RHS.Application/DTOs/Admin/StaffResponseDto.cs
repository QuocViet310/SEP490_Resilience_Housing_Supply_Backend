namespace RHS.Application.DTOs.Admin;

/// <summary>
/// DTO trả về thông tin cán bộ sau khi tạo tài khoản
/// </summary>
public class StaffResponseDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? CitizenId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public string Message { get; set; } = "Tài khoản đã được tạo thành công";
}
