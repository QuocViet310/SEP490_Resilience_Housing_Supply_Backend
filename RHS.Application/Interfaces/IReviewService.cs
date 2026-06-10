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
    /// VO xét duyệt hồ sơ: APPROVE (UNDER_REVIEW → APPROVED)
    /// hoặc REJECT (UNDER_REVIEW → REJECTED).
    /// Ghi log: Action = APPROVE hoặc REJECT.
    /// Note bắt buộc khi REJECT.
    /// </summary>
    Task<ReviewResponseDto> VerificationOfficerReviewAsync(
        Guid applicationId,
        Guid officerId,
        VerificationOfficerReviewRequestDto request);

    // ── Ward Manager (WM) ─────────────────────────────────────────

    /// <summary>
    /// WM xét duyệt hồ sơ:
    ///   APPROVE   → UNDER_REVIEW → APPROVED
    ///   REJECT    → UNDER_REVIEW → REJECTED
    ///   REQUEST_MORE_DOCUMENTS → UNDER_REVIEW → NEED_MORE_DOCUMENTS
    /// Ghi log: Action tương ứng.
    /// Note bắt buộc khi REJECT hoặc REQUEST_MORE_DOCUMENTS.
    /// </summary>
    Task<ReviewResponseDto> WardManagerReviewAsync(
        Guid applicationId,
        Guid managerId,
        WardManagerReviewRequestDto request);

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
