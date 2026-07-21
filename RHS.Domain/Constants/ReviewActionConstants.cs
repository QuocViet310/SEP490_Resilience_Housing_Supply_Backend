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

    /// <summary>Người dân tự hủy hồ sơ</summary>
    public const string Cancel = "CANCEL";

    /// <summary>CĐT gửi danh sách hồ sơ lên Sở Xây dựng (batch)</summary>
    public const string SubmitToDepartment = "SUBMIT_TO_DEPARTMENT";

    /// <summary>Hệ thống tự động phê duyệt sau 20 ngày (Tacit Approval)</summary>
    public const string TacitApproval = "TACIT_APPROVAL";

    /// <summary>Sở Xây dựng gắn cờ vi phạm (gian lận đất đai)</summary>
    public const string FlagViolation = "FLAG_VIOLATION";

    /// <summary>Sở Xây dựng gỡ cờ vi phạm</summary>
    public const string UnflagViolation = "UNFLAG_VIOLATION";

    /// <summary>Thanh toán đợt (installment) thành công → cập nhật PaymentInstallment</summary>
    public const string InstallmentPayment = "INSTALLMENT_PAYMENT";

    /// <summary>CĐT gán loại căn hộ cho người trúng bốc thăm</summary>
    public const string AssignApartment = "ASSIGN_APARTMENT";

    /// <summary>Người dân ký (đồng ý) hợp đồng nguyên tắc</summary>
    public const string ContractSigned = "CONTRACT_SIGNED";
}
