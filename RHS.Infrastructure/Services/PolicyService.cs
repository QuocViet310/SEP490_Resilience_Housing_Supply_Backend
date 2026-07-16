using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Policy;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class PolicyService : IPolicyService
{
    private const string CacheKeyAll = "policy:all";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PolicyService> _logger;

    public PolicyService(AppDbContext db, IMemoryCache cache, ILogger<PolicyService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<T> GetValueAsync<T>(string policyName, T defaultValue, CancellationToken ct = default)
    {
        var all = await GetAllCachedAsync(ct);
        var item = all.FirstOrDefault(p =>
            p.PolicyName.Equals(policyName, StringComparison.OrdinalIgnoreCase) && p.IsActive);

        if (item is null || string.IsNullOrWhiteSpace(item.PolicyValue))
            return defaultValue;

        try
        {
            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType == typeof(string))
                return (T)(object)item.PolicyValue;

            if (targetType == typeof(bool))
            {
                if (bool.TryParse(item.PolicyValue, out var b))
                    return (T)(object)b;
                if (item.PolicyValue is "1" or "yes" or "YES")
                    return (T)(object)true;
                return (T)(object)false;
            }

            if (targetType == typeof(int))
                return (T)(object)int.Parse(item.PolicyValue, CultureInfo.InvariantCulture);

            if (targetType == typeof(decimal))
                return (T)(object)decimal.Parse(item.PolicyValue, CultureInfo.InvariantCulture);

            if (targetType == typeof(double))
                return (T)(object)double.Parse(item.PolicyValue, CultureInfo.InvariantCulture);

            return (T)Convert.ChangeType(item.PolicyValue, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse policy {Key}={Value}, using default.", policyName, item.PolicyValue);
            return defaultValue;
        }
    }

    public async Task<IReadOnlyList<PolicyConfigDto>> GetAllAsync(CancellationToken ct = default)
        => await GetAllCachedAsync(ct);

    public async Task<PolicyConfigDto?> GetByNameAsync(string policyName, CancellationToken ct = default)
    {
        var all = await GetAllCachedAsync(ct);
        return all.FirstOrDefault(p => p.PolicyName.Equals(policyName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PolicyConfigDto> UpdateValueAsync(
        string policyName,
        string newValue,
        Guid updatedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newValue))
            throw new ArgumentException("PolicyValue không được để trống.");

        var entity = await _db.PolicyConfigs
            .FirstOrDefaultAsync(p => p.PolicyName == policyName, ct)
            ?? throw new InvalidOperationException($"Không tìm thấy policy '{policyName}'.");

        var oldValue = entity.PolicyValue;
        entity.PolicyValue = newValue.Trim();
        entity.UpdatedBy = updatedBy;
        entity.EffectiveDate = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        InvalidateCache();

        _logger.LogInformation(
            "Policy {Key} updated by {UserId}: {Old} → {New}",
            policyName, updatedBy, oldValue, entity.PolicyValue);

        return Map(entity);
    }

    public async Task EnsureDefaultsSeededAsync(Guid systemUserId, CancellationToken ct = default)
    {
        var existing = await _db.PolicyConfigs.Select(p => p.PolicyName).ToListAsync(ct);
        var toAdd = new List<PolicyConfig>();

        foreach (var (key, value, category, description) in PolicyKeys.Defaults)
        {
            if (existing.Contains(key))
                continue;

            toAdd.Add(new PolicyConfig
            {
                PolicyId = Guid.NewGuid(),
                PolicyName = key,
                PolicyValue = value,
                Category = category,
                Description = description,
                IsActive = true,
                EffectiveDate = DateTime.UtcNow,
                UpdatedBy = systemUserId
            });
        }

        if (toAdd.Count == 0)
            return;

        // UpdatedBy FK requires a real user — fall back to any admin/system user if needed
        var userExists = await _db.Users.AnyAsync(u => u.Id == systemUserId, ct);
        if (!userExists)
        {
            var anyUser = await _db.Users.Select(u => u.Id).FirstOrDefaultAsync(ct);
            if (anyUser == Guid.Empty)
            {
                _logger.LogWarning("Cannot seed PolicyConfig: no users in database yet.");
                return;
            }

            foreach (var p in toAdd)
                p.UpdatedBy = anyUser;
        }

        _db.PolicyConfigs.AddRange(toAdd);
        await _db.SaveChangesAsync(ct);
        InvalidateCache();
        _logger.LogInformation("Seeded {Count} PolicyConfig defaults.", toAdd.Count);
    }

    public void InvalidateCache() => _cache.Remove(CacheKeyAll);

    private async Task<IReadOnlyList<PolicyConfigDto>> GetAllCachedAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKeyAll, out IReadOnlyList<PolicyConfigDto>? cached) && cached is not null)
            return cached;

        var entities = await _db.PolicyConfigs
            .AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.PolicyName)
            .ToListAsync(ct);

        var list = entities.Select(Map).ToList();
        _cache.Set(CacheKeyAll, (IReadOnlyList<PolicyConfigDto>)list, CacheTtl);
        return list;
    }

    private static PolicyConfigDto Map(PolicyConfig p) => new()
    {
        PolicyId = p.PolicyId,
        PolicyName = p.PolicyName,
        PolicyValue = p.PolicyValue,
        Category = p.Category,
        Description = p.Description,
        IsActive = p.IsActive,
        EffectiveDate = p.EffectiveDate,
        UpdatedBy = p.UpdatedBy
    };
}
