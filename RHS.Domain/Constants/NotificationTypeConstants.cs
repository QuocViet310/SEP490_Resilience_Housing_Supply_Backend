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
}
