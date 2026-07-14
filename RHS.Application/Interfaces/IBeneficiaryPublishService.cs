using RHS.Application.DTOs.Beneficiaries;

namespace RHS.Application.Interfaces;

public interface IBeneficiaryPublishService
{
    Task<IReadOnlyList<BeneficiaryListItemDto>> GetPublishedBeneficiariesAsync(Guid? projectId = null, CancellationToken ct = default);
}
