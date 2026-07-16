using RHS.Application.DTOs.Policy;

namespace RHS.Application.Interfaces;

public interface IPolicyService
{
    Task<T> GetValueAsync<T>(string policyName, T defaultValue, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyConfigDto>> GetAllAsync(CancellationToken ct = default);
    Task<PolicyConfigDto?> GetByNameAsync(string policyName, CancellationToken ct = default);
    Task<PolicyConfigDto> UpdateValueAsync(string policyName, string newValue, Guid updatedBy, CancellationToken ct = default);
    Task EnsureDefaultsSeededAsync(Guid systemUserId, CancellationToken ct = default);
    void InvalidateCache();
}
