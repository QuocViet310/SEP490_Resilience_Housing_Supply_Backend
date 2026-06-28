namespace RHS.Domain.Constants;

/// <summary>
/// Hằng số mô tả các hành động xét duyệt hồ sơ.
/// Dùng cho trường ApplicationStatusHistory.Action.
/// </summary>
public static class ReviewActionConstants
{
    /// <summary>Phê duyệt hồ sơ (chỉ Ward Manager)</summary>
    public const string Approve = "APPROVE";

    /// <summary>Từ chối hồ sơ (chỉ Ward Manager)</summary>
    public const string Reject = "REJECT";

    /// <summary>Yêu cầu bổ sung giấy tờ (VO hoặc WM)</summary>
    public const string RequestMoreDocuments = "REQUEST_MORE_DOCUMENTS";

    /// <summary>Đề xuất phê duyệt (VO → WM)</summary>
    public const string Propose = "PROPOSE";

    /// <summary>Nhận hồ sơ để thẩm định (VO nhận)</summary>
    public const string AssignOfficer = "ASSIGN_OFFICER";

    /// <summary>Người dân nộp hồ sơ</summary>
    public const string Submit = "SUBMIT";

    /// <summary>Người dân lưu bản nháp</summary>
    public const string SaveDraft = "SAVE_DRAFT";

    /// <summary>Hệ thống tự động hủy do hết hạn thanh toán</summary>
    public const string PaymentTimeout = "PAYMENT_TIMEOUT";

    /// <summary>Thanh toán đặt cọc thành công → sinh hợp đồng + SlotCode</summary>
    public const string DepositPayment = "DEPOSIT_PAYMENT";
}
