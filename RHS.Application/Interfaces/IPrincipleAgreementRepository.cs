using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Repository thao tác CRUD với bảng PrincipleAgreements.
/// </summary>
public interface IPrincipleAgreementRepository
{
    /// <summary>Lưu bản ghi hợp đồng nguyên tắc mới</summary>
    Task CreateAsync(PrincipleAgreement agreement);

    /// <summary>Tìm hợp đồng theo ID hồ sơ đăng ký (1:1 relationship)</summary>
    Task<PrincipleAgreement?> GetByApplicationIdAsync(Guid applicationId);
}
