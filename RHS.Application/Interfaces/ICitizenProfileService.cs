using RHS.Application.DTOs.CitizenProfile;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service quản lý Hồ sơ cá nhân toàn diện của công dân (Citizen Profile):
/// - Định danh eKYC, Hôn nhân, Thu nhập, Nhà ở
/// - Quản lý nhân khẩu hộ gia đình tái sử dụng
/// - Quản lý Kho tài liệu cá nhân tái sử dụng (Personal Document Vault)
/// - Trích xuất dữ liệu kế thừa cho Đơn đăng ký NOXH (Pre-fill)
/// </summary>
public interface ICitizenProfileService
{
    // ── Full Profile & Prefill ───────────────────────────────────
    Task<CitizenFullProfileDto?> GetFullProfileAsync(Guid userId, CancellationToken ct = default);
    Task<CitizenFullProfileDto> UpdateCitizenProfileAsync(Guid userId, UpdateCitizenProfileDto dto, CancellationToken ct = default);
    Task<ApplicationPrefillResponseDto> GetApplicationPrefillAsync(Guid userId, CancellationToken ct = default);

    // ── User Household Members ───────────────────────────────────
    Task<List<UserHouseholdMemberResponseDto>> GetHouseholdMembersAsync(Guid userId, CancellationToken ct = default);
    Task<UserHouseholdMemberResponseDto> AddHouseholdMemberAsync(Guid userId, UserHouseholdMemberRequestDto dto, CancellationToken ct = default);
    Task<UserHouseholdMemberResponseDto> UpdateHouseholdMemberAsync(Guid userId, Guid memberId, UserHouseholdMemberRequestDto dto, CancellationToken ct = default);
    Task<bool> DeleteHouseholdMemberAsync(Guid userId, Guid memberId, CancellationToken ct = default);

    // ── User Document Vault ──────────────────────────────────────
    Task<List<UserDocumentResponseDto>> GetDocumentsAsync(Guid userId, CancellationToken ct = default);
    Task<UserDocumentResponseDto> UploadDocumentAsync(Guid userId, UploadUserDocumentRequestDto dto, CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(Guid userId, Guid documentId, CancellationToken ct = default);
}
