using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByGoogleIdAsync(string googleId);
    Task<User> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task<bool> EmailExistsAsync(string email);
    
    // New methods for staff management
    Task<List<User>> GetByRoleAsync(string roleName);
    Task<List<User>> GetStaffListAsync(int pageNumber, int pageSize, string? role = null, string? status = null, string? searchTerm = null);
    Task<int> GetStaffCountAsync(string? role = null, string? status = null, string? searchTerm = null);
}
