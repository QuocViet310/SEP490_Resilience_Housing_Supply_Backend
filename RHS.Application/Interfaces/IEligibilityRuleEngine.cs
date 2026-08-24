using RHS.Application.DTOs.Eligibility;
using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Rule engine Đ29 + Đ30: Đánh giá điều kiện mua nhà ở xã hội (Thu nhập &lt; 15tr/người, Diện tích &lt; 10m²/người).
/// </summary>
public interface IEligibilityRuleEngine
{
    /// <summary>
    /// Thẩm định điều kiện theo đơn đăng ký (HousingApplication).
    /// </summary>
    Task<EligibilityResultDto> AssessAsync(HousingApplication application, CancellationToken ct = default);

    /// <summary>
    /// Lấy kết quả thẩm định mới nhất của hồ sơ.
    /// </summary>
    Task<EligibilityResultDto?> GetLatestForApplicationAsync(Guid applicationId, CancellationToken ct = default);

    /// <summary>
    /// Thẩm định trực tiếp theo bộ tham số kiểm tra (Pre-check).
    /// </summary>
    Task<EligibilityResultDto> AssessCriteriaAsync(
        string? priorityGroup,
        string? maritalStatus,
        decimal? monthlyIncome,
        decimal? spouseMonthlyIncome,
        string? housingStatus,
        decimal? averageHousingAreaPerPerson,
        int totalMembersCount,
        CancellationToken ct = default);

    /// <summary>
    /// Thẩm định trực tiếp từ thông tin Profile của công dân (User + UserHouseholdMembers).
    /// </summary>
    Task<EligibilityResultDto> AssessProfileAsync(
        User user,
        List<UserHouseholdMember> householdMembers,
        CancellationToken ct = default);
}
