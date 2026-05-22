using RHS.Application.DTOs.User;
using RHS.Application.Interfaces;

namespace RHS.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return null;
        }

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            CitizenId = user.CitizenId,
            DateOfBirth = user.DateOfBirth,
            Address = user.Address,
            Role = user.Role?.RoleName ?? "Applicant",
            IsEmailVerified = user.IsEmailVerified,
            ProfileImageUrl = user.ProfileImageUrl,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<UserProfileDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto updateProfileDto)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return null;
        }

        // Update user information (CitizenId is NOT updatable - it's an identity field)
        user.FullName = updateProfileDto.FullName;
        user.PhoneNumber = updateProfileDto.PhoneNumber;
        user.DateOfBirth = updateProfileDto.DateOfBirth;
        user.Address = updateProfileDto.Address;

        await _userRepository.UpdateAsync(user);

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            CitizenId = user.CitizenId,
            DateOfBirth = user.DateOfBirth,
            Address = user.Address,
            Role = user.Role?.RoleName ?? "Applicant",
            IsEmailVerified = user.IsEmailVerified,
            ProfileImageUrl = user.ProfileImageUrl,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}
