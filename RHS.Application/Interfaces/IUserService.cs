using RHS.Application.DTOs.User;

namespace RHS.Application.Interfaces;

public interface IUserService
{
    Task<UserProfileDto?> GetProfileAsync(Guid userId);
    Task<UserProfileDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto updateProfileDto);
}
