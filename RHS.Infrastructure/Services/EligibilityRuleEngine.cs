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
/// Rule engine Đ29 + Đ30: đánh giá điều kiện hưởng chính sách NOXH.
/// Hỗ trợ tất cả nhóm đối tượng theo Điều 76 Luật Nhà ở 2023:
///   - Hộ nghèo/cận nghèo: dùng chuẩn nghèo, không xét trần thu nhập (Đ30.3)
///   - Người có công: chỉ xét điều kiện nhà ở (Đ29)
///   - Thu nhập thấp, công nhân, quân nhân, cán bộ, trả nhà CV, bị thu hồi đất:
///     xét trần thu nhập (Đ30.1/Đ30.2) + điều kiện nhà ở (Đ29)
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

        // ── Bước 1: Kiểm tra đối tượng (Đ76) ──────────────────────
        if (!PriorityGroupConstants.IsValid(application.PriorityGroup))
        {
            eligible = false;
            score -= 40;
            reasons.Add(
                $"Đối tượng '{application.PriorityGroup}' không hợp lệ. " +
                "Phải thuộc một trong các nhóm theo Điều 76 Luật Nhà ở 2023.");
        }
        else
        {
            var label = PriorityGroupConstants.Labels.TryGetValue(application.PriorityGroup!, out var l)
                ? l
                : application.PriorityGroup;

            if (PriorityGroupConstants.IsPovertyGroup(application.PriorityGroup))
            {
                reasons.Add(
                    $"Đối tượng thụ hưởng: {label} — áp dụng điều kiện chuẩn nghèo (Đ30.3), " +
                    "không xét trần thu nhập 15/30 triệu.");
            }
            else if (application.PriorityGroup == PriorityGroupConstants.MeritPerson)
            {
                reasons.Add(
                    $"Đối tượng thụ hưởng: {label} — " +
                    "được hỗ trợ cải thiện nhà ở theo Pháp lệnh Ưu đãi người có công (khoản 1 Đ76).");
            }
            else
            {
                reasons.Add($"Đối tượng thụ hưởng: {label} — áp dụng trần thu nhập theo Đ30.");
            }
        }

        // ── Bước 2: Kiểm tra trần thu nhập (Đ30) ──────────────────
        // Chỉ áp dụng cho các nhóm cần xét thu nhập (không áp dụng hộ nghèo + người có công)
        if (PriorityGroupConstants.RequiresIncomeCheck(application.PriorityGroup))
        {
            // Đ30.1: cá nhân ≤ 15 triệu/tháng; Đ30.2: hộ gia đình ≤ 30 triệu/tháng
            var maxIncomeSingle = await _policyService.GetValueAsync(
                PolicyKeys.IncomeSingleMaxVnd, 15_000_000m, ct);
            var maxIncomeHousehold = await _policyService.GetValueAsync(
                PolicyKeys.IncomeMarriedMaxVnd, 30_000_000m, ct);

            if (application.MonthlyIncome.HasValue)
            {
                // Dùng trần hộ gia đình nếu có vợ/chồng hoặc thành viên hộ, 
                // ngược lại dùng trần cá nhân
                var hasHousehold = application.MaritalStatus == "MARRIED"
                    || (application.HouseholdMembers != null && application.HouseholdMembers.Count > 0);
                var maxIncome = hasHousehold ? maxIncomeHousehold : maxIncomeSingle;
                var incomeLabel = hasHousehold ? "hộ gia đình" : "cá nhân";

                if (application.MonthlyIncome.Value > maxIncome)
                {
                    eligible = false;
                    score -= 30;
                    reasons.Add(
                        $"Thu nhập {application.MonthlyIncome.Value:N0} đ/tháng ({incomeLabel}) " +
                        $"vượt trần {maxIncome:N0} đ/tháng (Đ30). Không đủ điều kiện.");
                }
                else
                {
                    reasons.Add(
                        $"Đủ điều kiện thu nhập: {application.MonthlyIncome.Value:N0} đ/tháng ({incomeLabel}) " +
                        $"≤ {maxIncome:N0} đ/tháng (Đ30).");
                }
            }
            else
            {
                // Thu nhập chưa khai — cảnh báo nhưng không chặn 
                // (giấy xác nhận thu nhập sẽ bắt buộc khi upload)
                reasons.Add("Chưa khai thu nhập hàng tháng. Giấy xác nhận thu nhập sẽ được kiểm tra khi nộp hồ sơ.");
            }
        }

        // ── Bước 3: Kiểm tra điều kiện nhà ở (Đ29) ────────────────
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
