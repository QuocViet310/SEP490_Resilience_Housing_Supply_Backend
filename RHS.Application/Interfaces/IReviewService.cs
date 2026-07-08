using RHS.Application.DTOs.HousingApplications;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service xử lý nghiệp vụ xét duyệt hồ sơ nhà ở xã hội.
/// Bao gồm luồng của Verification Officer và Ward Manager.
/// Mọi hành động đều được ghi vào ReviewHistory (ApplicationStatusHistory).
/// </summary>
public interface IReviewService
{
    // ── Verification Officer (VO) ─────────────────────────────────

    /// <summary>
    /// VO nhận hồ sơ để thẩm định: SUBMITTED → UNDER_REVIEW.
    /// Ghi log: Action = ASSIGN_OFFICER.
    /// </summary>
    Task<ReviewResponseDto> AssignOfficerAsync(
        Guid applicationId,
        Guid officerId);

    /// <summary>
    /// CĐT xét duyệt hồ sơ.
    /// </summary>
    Task<ReviewResponseDto> HousingDeveloperReviewAsync(
        Guid applicationId,
        Guid developerId,
        HousingDeveloperReviewRequestDto request);

    // ── Department Of Construction (SXD) ──────────────────────────

    /// <summary>
    /// SXD xét duyệt hồ sơ.
    /// </summary>
    Task<ReviewResponseDto> DepartmentOfConstructionReviewAsync(
        Guid applicationId,
        Guid sxdUserId,
        DepartmentOfConstructionReviewRequestDto request);

    // ── Applicant submit ──────────────────────────────────────────

    /// <summary>
    /// Applicant nộp hồ sơ chính thức: DRAFT → SUBMITTED.
    /// Kiểm tra: phải có ít nhất 1 tài liệu trước khi nộp.
    /// Ghi log: Action = SUBMIT.
    /// </summary>
    Task<ReviewResponseDto> SubmitApplicationAsync(
        Guid applicationId,
        Guid applicantId);
}
