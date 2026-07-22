namespace RHS.Domain.Constants;

/// <summary>
/// Hằng số phân loại thông báo in-app.
/// Dùng cho trường Notification.NotificationType.
/// </summary>
public static class NotificationTypeConstants
{
    /// <summary>Hồ sơ đã nộp thành công</summary>
    public const string ApplicationSubmitted = "APPLICATION_SUBMITTED";

    /// <summary>Hồ sơ được phê duyệt → cần thanh toán đặt cọc</summary>
    public const string ApplicationApproved = "APPLICATION_APPROVED";

    /// <summary>Hồ sơ bị từ chối</summary>
    public const string ApplicationRejected = "APPLICATION_REJECTED";

    /// <summary>Yêu cầu bổ sung giấy tờ</summary>
    public const string ApplicationNeedMoreDocs = "APPLICATION_NEED_MORE_DOCS";

    /// <summary>Thanh toán đặt cọc thành công → có SlotCode + hợp đồng</summary>
    public const string DepositPaid = "DEPOSIT_PAID";

    /// <summary>Hết hạn thanh toán, hồ sơ bị hủy tự động</summary>
    public const string ApplicationExpired = "APPLICATION_EXPIRED";

    /// <summary>CĐT gửi hồ sơ lên Sở Xây dựng — thông báo cho SXD</summary>
    public const string ApplicationPendingSxdReview = "APPLICATION_PENDING_SXD_REVIEW";

    /// <summary>Người dân tự hủy hồ sơ</summary>
    public const string ApplicationCanceled = "APPLICATION_CANCELED";

    /// <summary>Khoản thu mới được tạo (installment created)</summary>
    public const string InstallmentCreated = "INSTALLMENT_CREATED";

    /// <summary>Thanh toán đợt thành công</summary>
    public const string InstallmentPaid = "INSTALLMENT_PAID";

    /// <summary>Khoản thu quá hạn</summary>
    public const string InstallmentOverdue = "INSTALLMENT_OVERDUE";

    /// <summary>Đã thanh toán đủ toàn bộ đợt trả trước</summary>
    public const string FullyPaid = "FULLY_PAID";

    /// <summary>Hợp đồng nguyên tắc đã được ký (đồng ý điều khoản)</summary>
    public const string ContractSigned = "CONTRACT_SIGNED";

    /// <summary>Thông báo mới từ Sở Xây dựng (announcement published)</summary>
    public const string AnnouncementPublished = "ANNOUNCEMENT_PUBLISHED";

    /// <summary>Lịch bốc thăm đã được lên/duyệt</summary>
    public const string LotteryScheduled = "LOTTERY_SCHEDULED";

    /// <summary>Kết quả bốc thăm đã được công bố</summary>
    public const string LotteryResultPublished = "LOTTERY_RESULT_PUBLISHED";
}
