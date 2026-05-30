using BCrypt.Net;
using Microsoft.Extensions.Configuration;
using RHS.Application.DTOs.Admin;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Service để quản lý cán bộ (Staff Management)
/// Cung cấp các chức năng: tạo tài khoản, phân quyền, cập nhật thông tin, khóa/kích hoạt tài khoản
/// </summary>
public class AdminService : IAdminService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IConfiguration _configuration;

    public AdminService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _configuration = configuration;
    }

    /// <summary>
    /// Tạo tài khoản cán bộ mới với vai trò Ward Manager hoặc Verification Officer
    /// </summary>
    public async Task<StaffResponseDto> CreateStaffAsync(CreateStaffDto createStaffDto, Guid adminId)
    {
        // Kiểm tra email đã tồn tại chưa
        if (await _userRepository.EmailExistsAsync(createStaffDto.Email))
        {
            throw new InvalidOperationException($"Email {createStaffDto.Email} đã được sử dụng");
        }

        // Kiểm tra vai trò hợp lệ
        if (!RoleConstants.GetStaffRoles().Contains(createStaffDto.Role))
        {
            throw new InvalidOperationException($"Vai trò {createStaffDto.Role} không hợp lệ");
        }

        // Lấy role từ database
        var role = await _roleRepository.GetByNameAsync(createStaffDto.Role);
        if (role == null)
        {
            // Nếu không tìm thấy, tạo role mới
            role = new Role
            {
                Id = RoleConstants.GetRoleId(createStaffDto.Role),
                RoleName = createStaffDto.Role
            };
            // Thêm vào database
            await _roleRepository.CreateAsync(role);
        }

        // Tạo tài khoản cán bộ
        var staff = new User
        {
            Id = Guid.NewGuid(),
            RoleId = role.Id,
            Email = createStaffDto.Email,
            FullName = createStaffDto.FullName,
            PhoneNumber = createStaffDto.PhoneNumber,
            CitizenId = createStaffDto.CitizenId,
            DateOfBirth = createStaffDto.DateOfBirth,
            Address = createStaffDto.Address,
            PasswordHash = BCrypt.HashPassword(createStaffDto.TemporaryPassword),
            Status = "Active",
            IsEmailVerified = true, // Staff được tạo bởi Admin không cần verify
            CreatedAt = DateTime.UtcNow
        };

        var createdStaff = await _userRepository.CreateAsync(staff);

        return new StaffResponseDto
        {
            Id = createdStaff.Id,
            Email = createdStaff.Email,
            FullName = createdStaff.FullName,
            PhoneNumber = createdStaff.PhoneNumber,
            CitizenId = createdStaff.CitizenId,
            DateOfBirth = createdStaff.DateOfBirth,
            Address = createdStaff.Address,
            RoleName = role.RoleName,
            Status = createdStaff.Status,
            CreatedAt = createdStaff.CreatedAt,
            Message = $"Tài khoản cán bộ {createStaffDto.Role} đã được tạo thành công. Email: {createdStaff.Email}"
        };
    }

    /// <summary>
    /// Lấy danh sách cán bộ với phân trang và bộ lọc
    /// </summary>
    public async Task<StaffListResponseDto> GetStaffListAsync(GetStaffListDto queryDto)
    {
        // Validate pagination
        if (queryDto.PageNumber < 1)
            queryDto.PageNumber = 1;
        if (queryDto.PageSize < 1 || queryDto.PageSize > 100)
            queryDto.PageSize = 10;

        // Lấy danh sách cán bộ
        var staffList = await _userRepository.GetStaffListAsync(
            queryDto.PageNumber,
            queryDto.PageSize,
            queryDto.Role,
            queryDto.Status,
            queryDto.SearchTerm);

        // Đếm tổng số
        var totalCount = await _userRepository.GetStaffCountAsync(
            queryDto.Role,
            queryDto.Status,
            queryDto.SearchTerm);

        // Chuyển đổi thành DTO
        var items = staffList.Select(s => new StaffResponseDto
        {
            Id = s.Id,
            Email = s.Email,
            FullName = s.FullName,
            PhoneNumber = s.PhoneNumber,
            CitizenId = s.CitizenId,
            DateOfBirth = s.DateOfBirth,
            Address = s.Address,
            RoleName = s.Role.RoleName,
            Status = s.Status,
            CreatedAt = s.CreatedAt
        }).ToList();

        return new StaffListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = queryDto.PageNumber,
            PageSize = queryDto.PageSize
        };
    }

    /// <summary>
    /// Lấy thông tin chi tiết một cán bộ
    /// </summary>
    public async Task<StaffResponseDto?> GetStaffByIdAsync(Guid staffId)
    {
        var staff = await _userRepository.GetByIdAsync(staffId);
        if (staff == null)
            return null;

        return new StaffResponseDto
        {
            Id = staff.Id,
            Email = staff.Email,
            FullName = staff.FullName,
            PhoneNumber = staff.PhoneNumber,
            CitizenId = staff.CitizenId,
            DateOfBirth = staff.DateOfBirth,
            Address = staff.Address,
            RoleName = staff.Role.RoleName,
            Status = staff.Status,
            CreatedAt = staff.CreatedAt
        };
    }

    /// <summary>
    /// Cập nhật thông tin cán bộ
    /// </summary>
    public async Task<StaffResponseDto?> UpdateStaffAsync(Guid staffId, UpdateStaffDto updateStaffDto, Guid adminId)
    {
        var staff = await _userRepository.GetByIdAsync(staffId);
        if (staff == null)
            throw new InvalidOperationException($"Không tìm thấy cán bộ với ID {staffId}");

        // Cập nhật thông tin cá nhân
        if (!string.IsNullOrWhiteSpace(updateStaffDto.FullName))
            staff.FullName = updateStaffDto.FullName;

        if (!string.IsNullOrWhiteSpace(updateStaffDto.PhoneNumber))
            staff.PhoneNumber = updateStaffDto.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(updateStaffDto.CitizenId))
            staff.CitizenId = updateStaffDto.CitizenId;

        if (updateStaffDto.DateOfBirth.HasValue)
            staff.DateOfBirth = updateStaffDto.DateOfBirth;

        if (!string.IsNullOrWhiteSpace(updateStaffDto.Address))
            staff.Address = updateStaffDto.Address;

        if (!string.IsNullOrWhiteSpace(updateStaffDto.Status))
            staff.Status = updateStaffDto.Status;

        // Cập nhật vai trò nếu được thay đổi
        if (!string.IsNullOrWhiteSpace(updateStaffDto.Role) && updateStaffDto.Role != staff.Role.RoleName)
        {
            var newRole = await _roleRepository.GetByNameAsync(updateStaffDto.Role);
            if (newRole == null)
            {
                throw new InvalidOperationException($"Vai trò {updateStaffDto.Role} không tồn tại");
            }
            staff.RoleId = newRole.Id;
        }

        await _userRepository.UpdateAsync(staff);

        return new StaffResponseDto
        {
            Id = staff.Id,
            Email = staff.Email,
            FullName = staff.FullName,
            PhoneNumber = staff.PhoneNumber,
            CitizenId = staff.CitizenId,
            DateOfBirth = staff.DateOfBirth,
            Address = staff.Address,
            RoleName = staff.Role.RoleName,
            Status = staff.Status,
            CreatedAt = staff.CreatedAt,
            Message = "Thông tin cán bộ đã được cập nhật thành công"
        };
    }

    /// <summary>
    /// Phân quyền cho cán bộ (thay đổi vai trò/trạng thái)
    /// </summary>
    public async Task<StaffResponseDto?> AssignPermissionAsync(AssignPermissionDto assignPermissionDto, Guid adminId)
    {
        var staff = await _userRepository.GetByIdAsync(assignPermissionDto.StaffId);
        if (staff == null)
            throw new InvalidOperationException($"Không tìm thấy cán bộ với ID {assignPermissionDto.StaffId}");

        // Cập nhật vai trò
        var newRole = await _roleRepository.GetByNameAsync(assignPermissionDto.Role);
        if (newRole == null)
            throw new InvalidOperationException($"Vai trò {assignPermissionDto.Role} không tồn tại");

        staff.RoleId = newRole.Id;
        staff.Status = assignPermissionDto.Status;

        await _userRepository.UpdateAsync(staff);

        return new StaffResponseDto
        {
            Id = staff.Id,
            Email = staff.Email,
            FullName = staff.FullName,
            PhoneNumber = staff.PhoneNumber,
            CitizenId = staff.CitizenId,
            DateOfBirth = staff.DateOfBirth,
            Address = staff.Address,
            RoleName = staff.Role.RoleName,
            Status = staff.Status,
            CreatedAt = staff.CreatedAt,
            Message = $"Quyền đã được phân cho cán bộ: vai trò {newRole.RoleName}, trạng thái {staff.Status}"
        };
    }

    /// <summary>
    /// Xóa/khóa tài khoản cán bộ
    /// </summary>
    public async Task<bool> DeactivateStaffAsync(Guid staffId, string reason, Guid adminId)
    {
        var staff = await _userRepository.GetByIdAsync(staffId);
        if (staff == null)
            throw new InvalidOperationException($"Không tìm thấy cán bộ với ID {staffId}");

        staff.Status = "Inactive";
        staff.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(staff);
        return true;
    }

    /// <summary>
    /// Kích hoạt lại tài khoản cán bộ
    /// </summary>
    public async Task<bool> ActivateStaffAsync(Guid staffId, Guid adminId)
    {
        var staff = await _userRepository.GetByIdAsync(staffId);
        if (staff == null)
            throw new InvalidOperationException($"Không tìm thấy cán bộ với ID {staffId}");

        staff.Status = "Active";
        staff.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(staff);
        return true;
    }

    /// <summary>
    /// Đặt lại mật khẩu cho cán bộ
    /// </summary>
    public async Task<bool> ResetStaffPasswordAsync(Guid staffId, string newPassword, Guid adminId)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            throw new InvalidOperationException("Mật khẩu phải có ít nhất 8 ký tự");

        var staff = await _userRepository.GetByIdAsync(staffId);
        if (staff == null)
            throw new InvalidOperationException($"Không tìm thấy cán bộ với ID {staffId}");

        staff.PasswordHash = BCrypt.HashPassword(newPassword);
        staff.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(staff);
        return true;
    }
}
