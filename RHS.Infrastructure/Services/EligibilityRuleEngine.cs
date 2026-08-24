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
/// Rule engine Đ29 + Đ30: Đánh giá tự động điều kiện hưởng chính sách NOXH.
/// Hỗ trợ tất cả nhóm đối tượng theo Điều 76 Luật Nhà ở 2023 &amp; Nghị định 100/2024/NĐ-CP:
///   - Thu nhập: &lt; 15 triệu/người/tháng (Độc thân &lt;= 15 triệu/tháng; Vợ+Chồng &lt;= 30 triệu/tháng)
///   - Diện tích: Chưa có nhà hoặc diện tích bình quân &lt; 10m²/người
///   - Đối tượng: Hộ nghèo/cận nghèo, người có công, thu nhập thấp, công nhân, LLVT...
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
        var result = await EvaluateRulesAsync(
            priorityGroup:               application.PriorityGroup,
            maritalStatus:               application.MaritalStatus,
            monthlyIncome:               application.MonthlyIncome,
            spouseMonthlyIncome:         application.SpouseMonthlyIncome,
            housingStatus:               application.HousingStatus,
            averageHousingAreaPerPerson: application.AverageHousingAreaPerPerson,
            totalMembersCount:           application.HouseholdMembersCount,
            ct:                          ct);

        result.ApplicationId = application.ApplicationId;

        // Lưu bản ghi lịch sử thẩm định
        var assessment = new EligibilityAssessment
        {
            AssessmentId   = Guid.NewGuid(),
            UserId         = application.ApplicantId,
            ApplicationId  = application.ApplicationId,
            Eligible       = result.Eligible,
            EstimatedScore = result.EstimatedScore,
            ReasonsJson    = JsonSerializer.Serialize(result.Reasons),
            AssessmentDate = DateTime.UtcNow
        };

        _db.EligibilityAssessments.Add(assessment);

        var tracked = await _db.HousingApplications
            .FirstOrDefaultAsync(a => a.ApplicationId == application.ApplicationId, ct);

        if (tracked != null)
        {
            tracked.LatestAssessmentId = assessment.AssessmentId;
            tracked.PriorityScore      = result.EstimatedScore;
            await _db.SaveChangesAsync(ct);
        }

        application.LatestAssessmentId = assessment.AssessmentId;
        application.PriorityScore      = result.EstimatedScore;
        result.AssessmentId            = assessment.AssessmentId;

        _logger.LogInformation(
            "Eligibility assessed for App {AppId}: Eligible={Eligible}, Score={Score}, Group={Group}",
            application.ApplicationId, result.Eligible, result.EstimatedScore, application.PriorityGroup);

        return result;
    }

    public async Task<EligibilityResultDto> AssessCriteriaAsync(
        string? priorityGroup,
        string? maritalStatus,
        decimal? monthlyIncome,
        decimal? spouseMonthlyIncome,
        string? housingStatus,
        decimal? averageHousingAreaPerPerson,
        int totalMembersCount,
        CancellationToken ct = default)
    {
        return await EvaluateRulesAsync(
            priorityGroup,
            maritalStatus,
            monthlyIncome,
            spouseMonthlyIncome,
            housingStatus,
            averageHousingAreaPerPerson,
            totalMembersCount,
            ct);
    }

    public async Task<EligibilityResultDto> AssessProfileAsync(
        User user,
        List<UserHouseholdMember> householdMembers,
        CancellationToken ct = default)
    {
        var totalMembers = 1 + (user.MaritalStatus == MaritalStatusConstants.Married ? 1 : 0) + householdMembers.Count;

        var result = await EvaluateRulesAsync(
            priorityGroup:               user.PriorityGroup,
            maritalStatus:               user.MaritalStatus,
            monthlyIncome:               user.MonthlyIncome,
            spouseMonthlyIncome:         user.SpouseMonthlyIncome,
            housingStatus:               user.HousingStatus,
            averageHousingAreaPerPerson: user.AverageHousingAreaPerPerson,
            totalMembersCount:           totalMembers,
            ct:                          ct);

        result.AssessmentId = Guid.NewGuid();
        return result;
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
            AssessmentId   = latest.AssessmentId,
            ApplicationId  = latest.ApplicationId,
            Eligible       = latest.Eligible,
            EstimatedScore = latest.EstimatedScore,
            Reasons        = reasons,
            AssessmentDate = latest.AssessmentDate
        };
    }

    // ─────────────────────────────────────────────────────────────
    // Core Rules Evaluation Engine
    // ─────────────────────────────────────────────────────────────

    private async Task<EligibilityResultDto> EvaluateRulesAsync(
        string? priorityGroup,
        string? maritalStatus,
        decimal? monthlyIncome,
        decimal? spouseMonthlyIncome,
        string? housingStatus,
        decimal? averageHousingAreaPerPerson,
        int totalMembersCount,
        CancellationToken ct)
    {
        var reasons = new List<string>();
        var score = 100m;
        var eligible = true;

        bool priorityGroupCheckPassed = false;
        bool incomeCheckPassed = false;
        bool housingAreaCheckPassed = false;

        decimal? calculatedIncome = null;
        decimal? maxAllowedIncome = null;
        decimal? maxAreaAllowed = null;

        var normPriorityGroup = priorityGroup?.Trim().ToUpperInvariant();
        var normMaritalStatus = maritalStatus?.Trim().ToUpperInvariant();
        var normHousingStatus = housingStatus?.Trim().ToUpperInvariant();

        // ── Bước 1: Kiểm tra đối tượng thụ hưởng (Điều 76 Luật Nhà ở 2023) ──
        if (string.IsNullOrWhiteSpace(normPriorityGroup) || !PriorityGroupConstants.IsValid(normPriorityGroup))
        {
            eligible = false;
            score -= 40;
            priorityGroupCheckPassed = false;
            reasons.Add(
                $"Đối tượng '{priorityGroup}' không hợp lệ. " +
                "Người nộp đơn phải thuộc một trong các nhóm đối tượng thụ hưởng theo Điều 76 Luật Nhà ở 2023.");
        }
        else
        {
            priorityGroupCheckPassed = true;
            var label = PriorityGroupConstants.Labels.TryGetValue(normPriorityGroup, out var l) ? l : normPriorityGroup;

            if (PriorityGroupConstants.IsPovertyGroup(normPriorityGroup))
            {
                reasons.Add($"Đối tượng thụ hưởng: {label} — áp dụng chuẩn nghèo theo quy định (Đ30.3), không xét trần thu nhập 15/30 triệu.");
            }
            else if (normPriorityGroup == PriorityGroupConstants.MeritPerson)
            {
                reasons.Add($"Đối tượng thụ hưởng: {label} — hưởng chính sách ưu đãi Người có công theo Pháp lệnh (Đ76.1), không xét trần thu nhập.");
            }
            else
            {
                reasons.Add($"Đối tượng thụ hưởng: {label} — áp dụng trần thu nhập theo Điều 30 Luật Nhà ở & Nghị định 100/2024/NĐ-CP.");
            }
        }

        // ── Bước 2: Kiểm tra trần thu nhập (Điều 30: < 15 triệu/người/tháng) ──
        var maxIncomeSingle = await _policyService.GetValueAsync(PolicyKeys.IncomeSingleMaxVnd, 15_000_000m, ct);
        var maxIncomeMarried = await _policyService.GetValueAsync(PolicyKeys.IncomeMarriedMaxVnd, 30_000_000m, ct);

        if (!PriorityGroupConstants.RequiresIncomeCheck(normPriorityGroup))
        {
            // Nhóm không cần xét trần thu nhập (Hộ nghèo, Người có công)
            incomeCheckPassed = true;
            calculatedIncome = monthlyIncome;
            maxAllowedIncome = null;
        }
        else
        {
            var isMarried = normMaritalStatus == MaritalStatusConstants.Married;
            maxAllowedIncome = isMarried ? maxIncomeMarried : maxIncomeSingle;

            if (isMarried)
            {
                var p1 = monthlyIncome.GetValueOrDefault(0m);
                var p2 = spouseMonthlyIncome.GetValueOrDefault(0m);
                calculatedIncome = p1 + p2;

                if (calculatedIncome.Value > maxIncomeMarried)
                {
                    eligible = false;
                    score -= 30;
                    incomeCheckPassed = false;
                    reasons.Add(
                        $"Tổng thu nhập của 2 vợ chồng ({calculatedIncome.Value:N0} đ/tháng) vượt trần {maxIncomeMarried:N0} đ/tháng (Đ30.1.a - bình quân tối đa 15 triệu/người). Không đủ điều kiện.");
                }
                else
                {
                    incomeCheckPassed = true;
                    reasons.Add(
                        $"Đủ điều kiện thu nhập: Tổng thu nhập 2 vợ chồng ({calculatedIncome.Value:N0} đ/tháng) ≤ trần quy định {maxIncomeMarried:N0} đ/tháng (bình quân {(calculatedIncome.Value / 2):N0} đ/người/tháng < 15 triệu/người).");
                }
            }
            else
            {
                calculatedIncome = monthlyIncome.GetValueOrDefault(0m);

                if (monthlyIncome.HasValue)
                {
                    if (calculatedIncome.Value > maxIncomeSingle)
                    {
                        eligible = false;
                        score -= 30;
                        incomeCheckPassed = false;
                        reasons.Add(
                            $"Thu nhập cá nhân ({calculatedIncome.Value:N0} đ/tháng) vượt trần quy định cho người độc thân {maxIncomeSingle:N0} đ/tháng (Đ30.1.a). Không đủ điều kiện.");
                    }
                    else
                    {
                        incomeCheckPassed = true;
                        reasons.Add(
                            $"Đủ điều kiện thu nhập: Thu nhập cá nhân ({calculatedIncome.Value:N0} đ/tháng) ≤ trần quy định {maxIncomeSingle:N0} đ/tháng.");
                    }
                }
                else
                {
                    incomeCheckPassed = false;
                    reasons.Add("Chưa khai báo thu nhập cá nhân hàng tháng. Giấy xác nhận thu nhập bắt buộc phải được cung cấp khi nộp đơn.");
                }
            }
        }

        // ── Bước 3: Kiểm tra điều kiện nhà ở (Điều 29: Chưa có nhà hoặc diện tích < 10m²/người) ──
        var maxArea = await _policyService.GetValueAsync(PolicyKeys.MaxAreaPerPersonM2, 10m, ct);
        maxAreaAllowed = maxArea;

        if (normHousingStatus == HousingStatusConstants.NoHouse)
        {
            housingAreaCheckPassed = true;
            reasons.Add("Đủ điều kiện nhà ở: Hiện chưa có nhà ở thuộc sở hữu của mình (Điều 29.1).");
        }
        else if (normHousingStatus == HousingStatusConstants.SmallHouse)
        {
            var area = averageHousingAreaPerPerson;

            if (!area.HasValue)
            {
                eligible = false;
                score -= 40;
                housingAreaCheckPassed = false;
                reasons.Add("Thiếu thông tin diện tích nhà ở bình quân đầu người khi khai báo nhà ở chật chội (SMALL_HOUSE).");
            }
            else if (area.Value >= maxArea)
            {
                eligible = false;
                score -= 40;
                housingAreaCheckPassed = false;
                reasons.Add(
                    $"Diện tích bình quân {area.Value:0.##} m²/người ≥ {maxArea:0.##} m²/người — vượt mức chuẩn nhà ở chật chội (quy định NOXH yêu cầu dưới {maxArea:0.##} m²/người theo Điều 29.2). Không đủ điều kiện.");
            }
            else
            {
                housingAreaCheckPassed = true;
                reasons.Add(
                    $"Đủ điều kiện nhà ở: Diện tích nhà ở bình quân {area.Value:0.##} m²/người < chuẩn chật chội {maxArea:0.##} m²/người (Điều 29.2).");
            }
        }
        else
        {
            eligible = false;
            score -= 50;
            housingAreaCheckPassed = false;
            reasons.Add($"Thực trạng nhà ở '{housingStatus}' không hợp lệ (Chỉ chấp nhận NO_HOUSE hoặc SMALL_HOUSE).");
        }

        if (score < 0) score = 0;

        var summary = eligible
            ? $"Hồ sơ ĐẠT ĐIỀU KIỆN mua Nhà ở Xã hội (Điểm ưu tiên ước tính: {score:0.#}/100)."
            : "Hồ sơ CHƯA ĐỦ ĐIỀU KIỆN mua Nhà ở Xã hội do vi phạm tiêu chuẩn về thu nhập, diện tích nhà ở hoặc đối tượng thụ hưởng.";

        return new EligibilityResultDto
        {
            AssessmentId             = Guid.NewGuid(),
            Eligible                 = eligible,
            EstimatedScore           = score,
            PriorityGroupCheckPassed = priorityGroupCheckPassed,
            IncomeCheckPassed        = incomeCheckPassed,
            HousingAreaCheckPassed   = housingAreaCheckPassed,
            TotalHouseholdIncome     = calculatedIncome,
            MaxAllowedIncome         = maxAllowedIncome,
            CalculatedAverageArea    = averageHousingAreaPerPerson,
            MaxAllowedAreaPerPerson  = maxAreaAllowed,
            SummaryMessage           = summary,
            Reasons                  = reasons,
            AssessmentDate           = DateTime.UtcNow
        };
    }
}
