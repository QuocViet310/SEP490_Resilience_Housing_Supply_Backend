using System;
using System.Threading;
using System.Threading.Tasks;
using RHS.Application.DTOs.Admin;

namespace RHS.Application.Interfaces;

public interface IAuditLogService
{
    Task<AuditLogListResponseDto> GetAuditLogsAsync(AuditLogQueryDto queryDto, CancellationToken ct = default);
    Task<AuditLogDetailDto?> GetAuditLogByIdAsync(Guid auditId, CancellationToken ct = default);
    Task LogActionAsync(Guid? userId, string action, string entityName, Guid entityId, object? oldValues, object? newValues, string ipAddress, CancellationToken ct = default);
}
