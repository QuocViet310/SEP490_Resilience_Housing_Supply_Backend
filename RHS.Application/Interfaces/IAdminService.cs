using RHS.Application.DTOs.Admin;

namespace RHS.Application.Interfaces;

/// <summary>
/// Interface cho các dịch vụ quản lý cán bộ của Admin
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Tạo tài khoản cán bộ mới (Ward Manager hoặc Verification Officer)
    /// </summary>
    Task<StaffResponseDto> CreateStaffAsync(CreateStaffDto createStaffDto, Guid adminId);

    /// <summary>
    /// Lấy danh sách cán bộ với phân trang và bộ lọc
    /// </summary>
    Task<StaffListResponseDto> GetStaffListAsync(GetStaffListDto queryDto);

    /// <summary>
    /// Lấy thông tin chi tiết một cán bộ
    /// </summary>
    Task<StaffResponseDto?> GetStaffByIdAsync(Guid staffId);

    /// <summary>
    /// Cập nhật thông tin cán bộ
    /// </summary>
    Task<StaffResponseDto?> UpdateStaffAsync(Guid staffId, UpdateStaffDto updateStaffDto, Guid adminId);

    /// <summary>
    /// Phân quyền cho cán bộ (thay đổi vai trò/trạng thái)
    /// </summary>
    Task<StaffResponseDto?> AssignPermissionAsync(AssignPermissionDto assignPermissionDto, Guid adminId);

    /// <summary>
    /// Xóa/khóa tài khoản cán bộ
    /// </summary>
    Task<bool> DeactivateStaffAsync(Guid staffId, string reason, Guid adminId);

    /// <summary>
    /// Kích hoạt lại tài khoản cán bộ
    /// </summary>
    Task<bool> ActivateStaffAsync(Guid staffId, Guid adminId);

    /// <summary>
    /// Đặt lại mật khẩu cho cán bộ
    /// </summary>
    Task<bool> ResetStaffPasswordAsync(Guid staffId, string newPassword, Guid adminId);
}
