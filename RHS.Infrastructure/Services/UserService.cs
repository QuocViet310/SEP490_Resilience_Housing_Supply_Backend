using Microsoft.AspNetCore.Http;
using RHS.Application.DTOs.User;
using RHS.Application.Interfaces;
using BCrypt.Net;

namespace RHS.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public UserService(
        IUserRepository userRepository, 
        IFileStorageService fileStorageService,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _userRepository = userRepository;
        _fileStorageService = fileStorageService;
        _refreshTokenRepository = refreshTokenRepository;
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

        // Update user information
        user.FullName = updateProfileDto.FullName;
        user.PhoneNumber = updateProfileDto.PhoneNumber;
        user.DateOfBirth = updateProfileDto.DateOfBirth;
        user.Address = updateProfileDto.Address;

        // CitizenId can only be set once (via eKyc), not overwritten afterwards
        if (string.IsNullOrEmpty(user.CitizenId) && !string.IsNullOrEmpty(updateProfileDto.CitizenId))
        {
            user.CitizenId = updateProfileDto.CitizenId;
        }

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

    public async Task<UserProfileDto?> UploadProfileImageAsync(Guid userId, IFormFile image)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return null;
        }

        // Delete old image if exists
        if (!string.IsNullOrEmpty(user.ProfileImageUrl))
        {
            await _fileStorageService.DeleteImageAsync(user.ProfileImageUrl);
        }

        // Upload new image
        var imageUrl = await _fileStorageService.UploadImageAsync(image, "profiles");
        user.ProfileImageUrl = imageUrl;

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

    public async Task<bool> DeleteProfileImageAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null || string.IsNullOrEmpty(user.ProfileImageUrl))
        {
            return false;
        }

        // Delete image file
        await _fileStorageService.DeleteImageAsync(user.ProfileImageUrl);

        // Update user record
        user.ProfileImageUrl = null;
        await _userRepository.UpdateAsync(user);

        return true;
    }

    public async Task<bool> DeleteAccountAsync(Guid userId, string password, string? reason)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            return false;
        }

        // Verify password for security
        if (user.PasswordHash != null && !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return false;
        }

        // For Google-only accounts, skip password verification
        if (user.GoogleId != null && user.PasswordHash == null)
        {
            // Allow deletion without password for Google accounts
        }

        // Delete profile image if exists
        if (!string.IsNullOrEmpty(user.ProfileImageUrl))
        {
            await _fileStorageService.DeleteImageAsync(user.ProfileImageUrl);
        }

        // Revoke all refresh tokens
        await _refreshTokenRepository.RevokeAllUserTokensAsync(userId);

        // Soft delete: Set status to Inactive instead of hard delete
        user.Status = "Deleted";
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // TODO: Log deletion reason for audit
        // await _auditLogService.LogAccountDeletion(userId, reason);

        return true;
    }
}
