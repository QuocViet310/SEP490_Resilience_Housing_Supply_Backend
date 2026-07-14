using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Eligibility;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Rule engine Đ29 + đối tượng hộ nghèo/cận nghèo đô thị (Đ30.3).
/// Không kiểm tra trần thu nhập 15/30 triệu — đối tượng này dùng chuẩn nghèo.
/// </summary>
public class EligibilityRuleEngine : IEligibilityRuleEngine
{
    private readonly AppDbContext _db;
    private readonly IPolicyService _policyService;
    private readonly ILogger<EligibilityRuleEngine> _logger;

    public EligibilityRuleEngine(
        AppDbContext db,
        IPolicyService policyService,
        ILogger<EligibilityRuleEngine> logger)
    {
        _db = db;
        _policyService = policyService;
        _logger = logger;
    }

    public async Task<EligibilityResultDto> AssessAsync(
        HousingApplication application,
        CancellationToken ct = default)
    {
        var reasons = new List<string>();
        var score = 100m;
        var eligible = true;

        // ── Đối tượng: hộ nghèo / cận nghèo đô thị ────────────────
        if (!PriorityGroupConstants.IsUrbanPoorOrNearPoor(application.PriorityGroup))
        {
            eligible = false;
            score -= 40;
            reasons.Add(
                "Đối tượng phải là hộ nghèo đô thị hoặc hộ cận nghèo đô thị (khoản 2–4 Điều 76 Luật Nhà ở).");
        }
        else
        {
            var label = PriorityGroupConstants.Labels.TryGetValue(application.PriorityGroup!, out var l)
                ? l
                : application.PriorityGroup;
            reasons.Add($"Đối tượng thụ hưởng: {label} — áp dụng điều kiện chuẩn nghèo (Đ30.3), không xét trần thu nhập 15/30 triệu.");
        }

        // ── Đ29: Điều kiện về nhà ở ───────────────────────────────
        if (application.HousingStatus == HousingStatusConstants.NoHouse)
        {
            reasons.Add("Đủ điều kiện nhà ở: chưa có nhà thuộc sở hữu (Đ29.1).");
        }
        else if (application.HousingStatus == HousingStatusConstants.SmallHouse)
        {
            var maxArea = await _policyService.GetValueAsync(PolicyKeys.MaxAreaPerPersonM2, 15m, ct);
            var area = application.AverageHousingAreaPerPerson;

            if (!area.HasValue)
            {
                eligible = false;
                score -= 40;
                reasons.Add("Thiếu diện tích nhà ở bình quân đầu người khi khai SMALL_HOUSE (Đ29.2).");
            }
            else if (area.Value >= maxArea)
            {
                eligible = false;
                score -= 40;
                reasons.Add($"Diện tích bình quân {area.Value:0.##} m²/người ≥ {maxArea} m² — không đủ điều kiện Đ29.2.");
            }
            else
            {
                reasons.Add($"Đủ điều kiện nhà ở: diện tích bình quân {area.Value:0.##} m²/người < {maxArea} m² (Đ29.2).");
            }
        }
        else
        {
            eligible = false;
            score -= 50;
            reasons.Add($"Thực trạng nhà ở '{application.HousingStatus}' không hợp lệ.");
        }

        if (score < 0) score = 0;

        var assessment = new EligibilityAssessment
        {
            AssessmentId = Guid.NewGuid(),
            UserId = application.ApplicantId,
            ApplicationId = application.ApplicationId,
            Eligible = eligible,
            EstimatedScore = score,
            ReasonsJson = JsonSerializer.Serialize(reasons),
            AssessmentDate = DateTime.UtcNow
        };

        _db.EligibilityAssessments.Add(assessment);
        application.LatestAssessmentId = assessment.AssessmentId;
        application.PriorityScore = score;
        _db.HousingApplications.Update(application);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Eligibility assessed for App {AppId}: Eligible={Eligible}, Score={Score}, Object={Object}",
            application.ApplicationId, eligible, score, application.PriorityGroup);

        return new EligibilityResultDto
        {
            AssessmentId = assessment.AssessmentId,
            ApplicationId = application.ApplicationId,
            Eligible = eligible,
            EstimatedScore = score,
            Reasons = reasons,
            AssessmentDate = assessment.AssessmentDate
        };
    }

    public async Task<EligibilityResultDto?> GetLatestForApplicationAsync(
        Guid applicationId,
        CancellationToken ct = default)
    {
        var latest = await _db.EligibilityAssessments
            .AsNoTracking()
            .Where(a => a.ApplicationId == applicationId)
            .OrderByDescending(a => a.AssessmentDate)
            .FirstOrDefaultAsync(ct);

        if (latest is null) return null;

        var reasons = string.IsNullOrWhiteSpace(latest.ReasonsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(latest.ReasonsJson) ?? new List<string>();

        return new EligibilityResultDto
        {
            AssessmentId = latest.AssessmentId,
            ApplicationId = latest.ApplicationId,
            Eligible = latest.Eligible,
            EstimatedScore = latest.EstimatedScore,
            Reasons = reasons,
            AssessmentDate = latest.AssessmentDate
        };
    }
}
