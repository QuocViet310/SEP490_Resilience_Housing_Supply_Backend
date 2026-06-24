namespace RHS.Domain.Constants;

/// <summary>
/// Hằng số mô tả các hành động xét duyệt hồ sơ.
/// Dùng cho trường ApplicationStatusHistory.Action.
/// </summary>
public static class ReviewActionConstants
{
    /// <summary>Phê duyệt hồ sơ</summary>
    public const string Approve = "APPROVE";

    /// <summary>Từ chối hồ sơ</summary>
    public const string Reject = "REJECT";

    /// <summary>Yêu cầu bổ sung giấy tờ (chỉ Ward Manager)</summary>
    public const string RequestMoreDocuments = "REQUEST_MORE_DOCUMENTS";

    /// <summary>Nhận hồ sơ để thẩm định (Verification Officer nhận)</summary>
    public const string AssignOfficer = "ASSIGN_OFFICER";

    /// <summary>Người dân nộp hồ sơ</summary>
    public const string Submit = "SUBMIT";

    /// <summary>Người dân lưu bản nháp</summary>
    public const string SaveDraft = "SAVE_DRAFT";

    /// <summary>Hệ thống tự động hủy do hết hạn thanh toán</summary>
    public const string PaymentTimeout = "PAYMENT_TIMEOUT";
}
