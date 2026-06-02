using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string roleName);
    Task<Role?> GetByIdAsync(Guid roleId);
    Task<List<Role>> GetAllAsync();
    Task<Role> CreateAsync(Role role);
}
