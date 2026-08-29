using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Admin;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(AppDbContext db, ILogger<AuditLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<AuditLogListResponseDto> GetAuditLogsAsync(AuditLogQueryDto queryDto, CancellationToken ct = default)
    {
        var query = _db.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryDto.EntityName))
        {
            var entityLower = queryDto.EntityName.Trim().ToLower();
            query = query.Where(a => a.EntityName.ToLower().Contains(entityLower));
        }

        if (!string.IsNullOrWhiteSpace(queryDto.Action))
        {
            var actionUpper = queryDto.Action.Trim().ToUpper();
            query = query.Where(a => a.Action.ToUpper() == actionUpper);
        }

        if (queryDto.UserId.HasValue && queryDto.UserId.Value != Guid.Empty)
        {
            query = query.Where(a => a.UserId == queryDto.UserId.Value);
        }

        if (queryDto.FromDate.HasValue)
        {
            query = query.Where(a => a.ActionTime >= queryDto.FromDate.Value);
        }

        if (queryDto.ToDate.HasValue)
        {
            query = query.Where(a => a.ActionTime <= queryDto.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryDto.SearchKey))
        {
            var keyLower = queryDto.SearchKey.Trim().ToLower();
            query = query.Where(a =>
                a.EntityName.ToLower().Contains(keyLower)
                || a.Action.ToLower().Contains(keyLower)
                || a.IpAddress.ToLower().Contains(keyLower)
                || (a.User != null && (a.User.FullName.ToLower().Contains(keyLower) || a.User.Email.ToLower().Contains(keyLower)))
                || (a.OldValues != null && a.OldValues.ToLower().Contains(keyLower))
                || (a.NewValues != null && a.NewValues.ToLower().Contains(keyLower)));
        }

        int totalCount = await query.CountAsync(ct);
        int page = queryDto.Page > 0 ? queryDto.Page : 1;
        int pageSize = queryDto.PageSize > 0 ? queryDto.PageSize : 20;

        var items = await query
            .OrderByDescending(a => a.ActionTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogResponseDto
            {
                AuditId = a.AuditId,
                UserId = a.UserId,
                UserFullName = a.User != null ? a.User.FullName : null,
                UserEmail = a.User != null ? a.User.Email : null,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                IpAddress = a.IpAddress,
                ActionTime = a.ActionTime
            })
            .ToListAsync(ct);

        return new AuditLogListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AuditLogDetailDto?> GetAuditLogByIdAsync(Guid auditId, CancellationToken ct = default)
    {
        var log = await _db.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .ThenInclude(u => u!.Role)
            .FirstOrDefaultAsync(a => a.AuditId == auditId, ct);

        if (log is null)
            return null;

        return new AuditLogDetailDto
        {
            AuditId = log.AuditId,
            UserId = log.UserId,
            UserFullName = log.User?.FullName,
            UserEmail = log.User?.Email,
            UserRole = log.User?.Role?.RoleName,
            Action = log.Action,
            EntityName = log.EntityName,
            EntityId = log.EntityId,
            OldValues = log.OldValues,
            NewValues = log.NewValues,
            IpAddress = log.IpAddress,
            ActionTime = log.ActionTime
        };
    }

    public async Task LogActionAsync(
        Guid? userId,
        string action,
        string entityName,
        Guid entityId,
        object? oldValues,
        object? newValues,
        string ipAddress,
        CancellationToken ct = default)
    {
        try
        {
            var auditLog = new AuditLog
            {
                AuditId = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
                ActionTime = DateTime.UtcNow
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write manual audit log for entity {EntityName} ({EntityId})", entityName, entityId);
        }
    }
}
