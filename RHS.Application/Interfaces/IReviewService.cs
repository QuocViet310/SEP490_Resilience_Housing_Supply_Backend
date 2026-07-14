using RHS.Application.DTOs.HousingApplications;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service xử lý nghiệp vụ xét duyệt hồ sơ nhà ở xã hội.
/// Bao gồm luồng của CĐT (Housing Developer) và SXD (Department Of Construction).
/// Mọi hành động đều được ghi vào ReviewHistory (ApplicationStatusHistory).
/// </summary>
public interface IReviewService
{
    // ── Applicant ──────────────────────────────────────────────────

    /// <summary>
    /// Applicant nộp hồ sơ chính thức: DRAFT → SUBMITTED.
    /// Hoặc nộp lại từ NEED_MORE_DOCUMENTS → SUBMITTED.
    /// Kiểm tra: phải có ít nhất 1 tài liệu trước khi nộp.
    /// Ghi log: Action = SUBMIT.
    /// </summary>
    Task<ReviewResponseDto> SubmitApplicationAsync(
        Guid applicationId,
        Guid applicantId);

    /// <summary>
    /// Applicant tự hủy hồ sơ (Task #11).
    /// Điều kiện: Hồ sơ KHÔNG nằm ở trạng thái đóng (DEPOSIT_PAID, REJECTED, CANCELED, EXPIRED).
    /// APPROVED không giữ suất vật lý — hủy không hoàn AvailableUnits (suất chỉ trừ khi bốc thăm trúng).
    /// Giải phóng CCCD cho người dân nộp hồ sơ khác.
    /// Ghi log: Action = CANCEL.
    /// </summary>
    Task<ReviewResponseDto> CancelApplicationAsync(
        Guid applicationId,
        Guid applicantId,
        CancelApplicationRequestDto request);

    // ── Housing Developer (CĐT) ──────────────────────────────────

    /// <summary>
    /// CĐT nhận hồ sơ để thẩm định: SUBMITTED → REVIEWING.
    /// Ghi log: Action = ASSIGN_OFFICER.
    /// </summary>
    Task<ReviewResponseDto> AssignOfficerAsync(
        Guid applicationId,
        Guid officerId);

    /// <summary>
    /// CĐT xét duyệt hồ sơ (Task #6).
    /// Actions: REQUEST_MORE_DOCUMENTS, REJECT.
    /// Điều kiện: Hồ sơ đang ở REVIEWING.
    /// </summary>
    Task<ReviewResponseDto> HousingDeveloperReviewAsync(
        Guid applicationId,
        Guid developerId,
        HousingDeveloperReviewRequestDto request);

    /// <summary>
    /// CĐT gửi danh sách hồ sơ đã duyệt lên Sở Xây dựng (Task #7).
    /// Batch chuyển trạng thái REVIEWING → PENDING_SXD_REVIEW.
    /// Từ thời điểm này, CĐT không có quyền ghi lên các hồ sơ này nữa.
    /// Gửi thông báo cho tất cả user SXD.
    /// Ghi log: Action = SUBMIT_TO_DEPARTMENT.
    /// </summary>
    Task<List<ReviewResponseDto>> SubmitToDepartmentAsync(
        Guid developerId,
        SubmitToDepartmentRequestDto request);

    // ── Department Of Construction (SXD) ──────────────────────────

    /// <summary>
    /// SXD hậu kiểm và phê duyệt/từ chối cuối cùng trên TỪNG hồ sơ (Task #8).
    /// Điều kiện: Hồ sơ đang ở PENDING_SXD_REVIEW.
    /// Actions: APPROVE, REJECT.
    /// </summary>
    Task<ReviewResponseDto> DepartmentOfConstructionReviewAsync(
        Guid applicationId,
        Guid sxdUserId,
        DepartmentOfConstructionReviewRequestDto request);
}

