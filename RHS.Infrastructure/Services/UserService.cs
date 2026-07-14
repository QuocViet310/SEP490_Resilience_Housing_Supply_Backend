using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.User;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;
using BCrypt.Net;

namespace RHS.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly AppDbContext _db;

    public UserService(
        IUserRepository userRepository,
        IFileStorageService fileStorageService,
        IRefreshTokenRepository refreshTokenRepository,
        AppDbContext db)
    {
        _userRepository = userRepository;
        _fileStorageService = fileStorageService;
        _refreshTokenRepository = refreshTokenRepository;
        _db = db;
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

        // Đã xác minh eKYC (có CCCD): chỉ cho đổi số điện thoại.
        // Họ tên / ngày sinh / địa chỉ / CCCD chỉ cập nhật qua luồng xác minh danh tính.
        var hasEkyc = !string.IsNullOrWhiteSpace(user.CitizenId);
        if (hasEkyc)
        {
            user.PhoneNumber = updateProfileDto.PhoneNumber;
        }
        else
        {
            // Lần đầu điền từ eKYC OCR
            if (!string.IsNullOrWhiteSpace(updateProfileDto.FullName))
                user.FullName = updateProfileDto.FullName.Trim();

            user.PhoneNumber = updateProfileDto.PhoneNumber;
            user.DateOfBirth = updateProfileDto.DateOfBirth;
            user.Address = updateProfileDto.Address;

            if (!string.IsNullOrWhiteSpace(updateProfileDto.CitizenId))
            {
                var newCitizenId = updateProfileDto.CitizenId.Trim();
                var taken = await _userRepository.CitizenIdExistsAsync(newCitizenId, excludeUserId: userId);
                if (taken)
                {
                    throw new InvalidOperationException(
                        "Số CCCD này đã được xác thực bởi tài khoản đang hoạt động khác.");
                }
                user.CitizenId = newCitizenId;
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
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

        // Soft delete: giải phóng CCCD + email để eKYC/đăng ký lại trên tài khoản Active mới.
        // Email unique index → đổi sang alias không trùng.
        var originalEmail = user.Email;
        user.Status = "Deleted";
        user.CitizenId = null;
        user.GoogleId = null;
        user.Email = $"deleted+{userId:N}@{SanitizeEmailDomain(originalEmail)}";
        user.UpdatedAt = DateTime.UtcNow;

        // Hủy hồ sơ còn mở (không đụng DEPOSIT_PAID / APPROVED đã hỗ trợ — Đ38.1.đ vẫn dựa CitizenId trên hồ sơ)
        var openStatuses = new[]
        {
            ApplicationStatusConstants.Draft,
            ApplicationStatusConstants.Submitted,
            ApplicationStatusConstants.Reviewing,
            ApplicationStatusConstants.NeedMoreDocuments,
            ApplicationStatusConstants.PendingSxdReview,
            ApplicationStatusConstants.Approved
        };

        var openApps = await _db.HousingApplications
            .Where(a => a.ApplicantId == userId && openStatuses.Contains(a.ApplicationStatus))
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var app in openApps)
        {
            var oldStatus = app.ApplicationStatus;
            app.ApplicationStatus = ApplicationStatusConstants.Canceled;
            app.UpdatedAt = now;
            _db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                HistoryId = Guid.NewGuid(),
                ApplicationId = app.ApplicationId,
                ChangedBy = userId,
                Action = ReviewActionConstants.Cancel,
                OldStatus = oldStatus,
                NewStatus = ApplicationStatusConstants.Canceled,
                Note = "Tự động hủy do người dùng xóa tài khoản.",
                ChangedAt = now
            });
        }

        await _userRepository.UpdateAsync(user);
        await _db.SaveChangesAsync();

        return true;
    }

    private static string SanitizeEmailDomain(string email)
    {
        var at = email.LastIndexOf('@');
        return at >= 0 && at < email.Length - 1
            ? email[(at + 1)..]
            : "deleted.local";
    }
}
