using RHS.Application.DTOs.Eligibility;
using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

public interface IEligibilityRuleEngine
{
    Task<EligibilityResultDto> AssessAsync(HousingApplication application, CancellationToken ct = default);
    Task<EligibilityResultDto?> GetLatestForApplicationAsync(Guid applicationId, CancellationToken ct = default);
}
