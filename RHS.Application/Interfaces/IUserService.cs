using Microsoft.AspNetCore.Http;
using RHS.Application.DTOs.User;

namespace RHS.Application.Interfaces;

public interface IUserService
{
    Task<UserProfileDto?> GetProfileAsync(Guid userId);
    Task<UserProfileDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto updateProfileDto);
    Task<UserProfileDto?> UploadProfileImageAsync(Guid userId, IFormFile image);
    Task<bool> DeleteProfileImageAsync(Guid userId);
    Task<bool> DeleteAccountAsync(Guid userId, string password, string? reason);
}
