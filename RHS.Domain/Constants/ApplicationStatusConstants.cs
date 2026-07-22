namespace RHS.Domain.Constants;

/// <summary>
/// Định nghĩa các hằng số trạng thái cho hồ sơ xét duyệt nhà ở xã hội.
/// Sử dụng string constants thay vì enum để tương thích với cột string trong DB.
/// </summary>
public static class ApplicationStatusConstants
{
    /// <summary>Hồ sơ nháp - chưa nộp</summary>
    public const string Draft = "DRAFT";

    /// <summary>Đã nộp - chờ xét duyệt</summary>
    public const string Submitted = "SUBMITTED";

    /// <summary>Đang thẩm định bởi Housing Developer (CĐT)</summary>
    public const string Reviewing = "REVIEWING";

    /// <summary>Yêu cầu bổ sung giấy tờ (CĐT yêu cầu)</summary>
    public const string NeedMoreDocuments = "NEED_MORE_DOCUMENTS";

    /// <summary>CĐT đã chốt danh sách, gửi lên Sở Xây dựng</summary>
    public const string PendingSxdReview = "PENDING_SXD_REVIEW";

    /// <summary>Sở Xây dựng đã phê duyệt</summary>
    public const string Approved = "APPROVED";

    /// <summary>Tự động phê duyệt do quá 20 ngày Sở Xây dựng không phản hồi (Approved by Timeout)</summary>
    public const string ApprovedByTimeout = "APPROVED_BY_TIMEOUT";

    /// <summary>Đã bị từ chối</summary>
    public const string Rejected = "REJECTED";

    /// <summary>Đã bị hủy</summary>
    public const string Canceled = "CANCELED";

    /// <summary>Đã hết hạn thanh toán</summary>
    public const string Expired = "EXPIRED";

    /// <summary>Đã thanh toán đặt cọc thành công</summary>
    public const string DepositPaid = "DEPOSIT_PAID";

    /// <summary>Đã thanh toán đủ toàn bộ đợt trả trước (tất cả installments PAID)</summary>
    public const string FullyPaid = "FULLY_PAID";

    /// <summary>Đã ký hợp đồng nguyên tắc (đồng ý điều khoản)</summary>
    public const string ContractSigned = "CONTRACT_SIGNED";

    /// <summary>Danh sách tất cả trạng thái hợp lệ</summary>
    public static readonly IReadOnlyList<string> AllStatuses = new[]
    {
        Draft,
        Submitted,
        Reviewing,
        NeedMoreDocuments,
        PendingSxdReview,
        Approved,
        ApprovedByTimeout,
        Rejected,
        Canceled,
        Expired,
        DepositPaid,
        FullyPaid,
        ContractSigned
    };

    /// <summary>
    /// Kiểm tra trạng thái có hợp lệ không.
    /// </summary>
    public static bool IsValid(string status) => AllStatuses.Contains(status);

    /// <summary>
    /// Định nghĩa các chuyển trạng thái hợp lệ theo vai trò nghiệp vụ.
    /// Key: trạng thái hiện tại → Value: các trạng thái có thể chuyển sang
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> HousingDeveloperTransitions =
        new Dictionary<string, string[]>
        {
            // CĐT có thể chuyển từ SUBMITTED → REVIEWING (nhận hồ sơ)
            [Submitted] = new[] { Reviewing },
            // CĐT kiểm tra và yêu cầu bổ sung, hoặc từ chối
            // (Gửi lên SXD sẽ dùng API batch riêng: POST /api/housing-developer/submit-to-department)
            [Reviewing] = new[] { NeedMoreDocuments, Rejected },
            // CĐT có thể chuyển từ NEED_MORE_DOCUMENTS → REVIEWING (sau khi người dân bổ sung)
            [NeedMoreDocuments] = new[] { Reviewing }
        };

    public static readonly IReadOnlyDictionary<string, string[]> DepartmentOfConstructionTransitions =
        new Dictionary<string, string[]>
        {
            // SXD chỉ xử lý hồ sơ đã được CĐT gửi lên (PENDING_SXD_REVIEW)
            // Phê duyệt hoặc từ chối cuối cùng
            [PendingSxdReview] = new[] { Approved, Rejected }
        };

    /// <summary>
    /// Các trạng thái mà Applicant KHÔNG được phép tự hủy (trạng thái đóng).
    /// </summary>
    public static readonly IReadOnlyList<string> ClosedStatuses = new[]
    {
        DepositPaid,
        FullyPaid,
        ContractSigned,
        Rejected,
        Canceled,
        Expired
    };
}
