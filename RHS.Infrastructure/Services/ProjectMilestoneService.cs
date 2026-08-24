using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Milestone;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class ProjectMilestoneService : IProjectMilestoneService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProjectMilestoneService> _logger;

    public ProjectMilestoneService(
        AppDbContext context,
        ILogger<ProjectMilestoneService> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task<ProjectMilestonesResponseDto> GetProjectMilestonesAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var project = await _context.HousingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        var milestones = await _context.PaymentMilestones
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && m.IsActive)
            .OrderBy(m => m.PhaseOrder)
            .ToListAsync(ct);

        var dtoList = milestones.Select(MapToMilestoneDto).ToList();
        var totalPct = dtoList.Sum(m => m.Percentage ?? 0);

        return new ProjectMilestonesResponseDto
        {
            ProjectId          = project.Id,
            ProjectName        = project.ProjectName,
            TotalMilestones    = dtoList.Count,
            TotalPercentage    = totalPct,
            IsFullyConfigured  = dtoList.Count >= 3 && dtoList.Count <= 6 && Math.Abs(totalPct - 100m) < 0.001m,
            Milestones         = dtoList
        };
    }

    public async Task<ProjectMilestonesResponseDto> ConfigureProjectMilestonesAsync(
        Guid projectId,
        Guid userId,
        ConfigureProjectMilestonesRequestDto request,
        CancellationToken ct = default)
    {
        var project = await _context.HousingProjects
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (project == null)
            throw new KeyNotFoundException($"Không tìm thấy dự án với ID: {projectId}");

        // 1. Validate Developer Access
        await ValidateDeveloperAccessAsync(project, userId, ct);

        // 2. Validate Milestones Count (3 - 6 đợt)
        if (request.Milestones == null || request.Milestones.Count < 3 || request.Milestones.Count > 6)
        {
            throw new ArgumentException(
                $"Chủ đầu tư chỉ được cấu hình từ 3 đến 6 đợt đóng tiền theo quy định của dự án NOXH. Số đợt gửi lên: {request.Milestones?.Count ?? 0}.");
        }

        var sortedItems = request.Milestones.OrderBy(m => m.PhaseOrder).ToList();

        // 3. Validate PhaseOrder sequence (1..N)
        for (int i = 0; i < sortedItems.Count; i++)
        {
            var expectedOrder = i + 1;
            if (sortedItems[i].PhaseOrder != expectedOrder)
            {
                throw new ArgumentException(
                    $"Thứ tự các đợt thanh toán phải liên tục từ 1 đến {sortedItems.Count}. Đợt thứ {expectedOrder} đang có PhaseOrder={sortedItems[i].PhaseOrder}.");
            }
        }

        // 4. Validate Individual Percentages & TriggerEvents
        decimal totalPercentage = 0m;
        for (int i = 0; i < sortedItems.Count; i++)
        {
            var item = sortedItems[i];

            if (string.IsNullOrWhiteSpace(item.PhaseName))
                throw new ArgumentException($"Đợt {item.PhaseOrder} phải có tên đợt thanh toán rõ ràng.");

            if (!item.Percentage.HasValue || item.Percentage.Value <= 0m || item.Percentage.Value > 100m)
            {
                throw new ArgumentException(
                    $"Đợt {item.PhaseOrder} ({item.PhaseName}) có tỷ lệ phần trăm ({item.Percentage}%) không hợp lệ. Tỷ lệ mỗi đợt phải > 0% và <= 100%.");
            }

            if (!TriggerEventConstants.IsValid(item.TriggerEvent))
            {
                throw new ArgumentException(
                    $"Đợt {item.PhaseOrder} có sự kiện kích hoạt '{item.TriggerEvent}' không hợp lệ. Cho phép: {string.Join(", ", TriggerEventConstants.All)}");
            }

            if (item.DueDays <= 0)
            {
                throw new ArgumentException($"Đợt {item.PhaseOrder} có thời hạn thanh toán (DueDays={item.DueDays}) không hợp lệ. Phải lớn hơn 0 ngày.");
            }

            totalPercentage += item.Percentage.Value;
        }

        // 5. Validate Total Percentage == 100%
        if (Math.Abs(totalPercentage - 100.0m) > 0.001m)
        {
            throw new ArgumentException(
                $"Tổng tỷ lệ phần trăm thanh toán của các đợt phải chính xác bằng 100% (Hiện tại tổng = {totalPercentage:F2}%). Vui lòng cân đối lại tỷ lệ giữa các đợt.");
        }

        // 6. Validate Phase 1 (First payment / Deposit ratio)
        var firstPhase = sortedItems[0];
        var p1Val = firstPhase.Percentage.GetValueOrDefault();
        if (p1Val > 30.0m)
        {
            throw new ArgumentException(
                $"Tỷ lệ thanh toán Đợt 1 ({p1Val}%) vượt quá mức trần quy định cho NOXH (tối đa 30% giá trị hợp đồng).");
        }

        // 7. Check if project already has actual PAID installments (lock structural changes)
        var hasPaidInstallments = await _context.PaymentInstallments
            .AsNoTracking()
            .Include(i => i.Milestone)
            .AnyAsync(i => i.Milestone.ProjectId == projectId && i.Status == InstallmentStatusConstants.Paid, ct);

        if (hasPaidInstallments)
        {
            throw new InvalidOperationException(
                "Dự án đã có cư dân thanh toán tiền theo lịch thu hiện tại. Không được phép cấu hình lại cấu trúc các đợt để đảm bảo tính toàn vẹn sổ sách tài chính.");
        }

        // 8. Replace / Update Milestones
        var existingMilestones = await _context.PaymentMilestones
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        _context.PaymentMilestones.RemoveRange(existingMilestones);

        var now = DateTime.UtcNow;
        var newMilestones = sortedItems.Select(item => new PaymentMilestone
        {
            Id              = Guid.NewGuid(),
            ProjectId       = projectId,
            PhaseOrder      = item.PhaseOrder,
            PhaseName       = item.PhaseName.Trim(),
            CalculationType = string.IsNullOrWhiteSpace(item.CalculationType) ? CalculationTypeConstants.Percentage : item.CalculationType.Trim().ToUpperInvariant(),
            FixedAmount     = item.FixedAmount,
            Percentage      = item.Percentage,
            TriggerEvent    = item.TriggerEvent.Trim().ToUpperInvariant(),
            DueDays         = item.DueDays,
            Description     = item.Description?.Trim(),
            IsActive        = true,
            CreatedAt       = now,
            UpdatedAt       = now
        }).ToList();

        _context.PaymentMilestones.AddRange(newMilestones);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cấu hình thành công {Count} đợt thanh toán cho dự án {ProjectId} bởi user {UserId}.",
            newMilestones.Count, projectId, userId);

        return await GetProjectMilestonesAsync(projectId, ct);
    }

    // ─────────────────────────────────────────────────────────────
    // Helper Methods
    // ─────────────────────────────────────────────────────────────

    private static MilestoneDto MapToMilestoneDto(PaymentMilestone m)
    {
        return new MilestoneDto
        {
            Id                = m.Id,
            ProjectId         = m.ProjectId,
            PhaseOrder        = m.PhaseOrder,
            PhaseName         = m.PhaseName,
            CalculationType   = m.CalculationType,
            FixedAmount       = m.FixedAmount,
            Percentage        = m.Percentage,
            TriggerEvent      = m.TriggerEvent,
            TriggerEventLabel = TriggerEventConstants.GetDisplayName(m.TriggerEvent),
            DueDays           = m.DueDays,
            Description       = m.Description,
            IsActive          = m.IsActive,
            CreatedAt         = m.CreatedAt,
            UpdatedAt         = m.UpdatedAt
        };
    }

    private async Task ValidateDeveloperAccessAsync(HousingProject project, Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return;

        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
            return;

        var role = user.Role?.RoleName ?? string.Empty;
        if (role == RoleConstants.HousingDeveloper)
        {
            if (project.DeveloperId.HasValue && project.DeveloperId.Value != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền quản lý lịch thanh toán của dự án này.");
            }
        }
    }
}
