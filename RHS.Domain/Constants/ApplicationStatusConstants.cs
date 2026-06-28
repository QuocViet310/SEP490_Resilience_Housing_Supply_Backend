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

    /// <summary>Đang thẩm định bởi Verification Officer</summary>
    public const string UnderReview = "UNDER_REVIEW";

    /// <summary>Yêu cầu bổ sung giấy tờ (VO hoặc Ward Manager yêu cầu)</summary>
    public const string NeedMoreDocuments = "NEED_MORE_DOCUMENTS";

    /// <summary>VO đã đề xuất phê duyệt, chờ WM quyết định cuối cùng</summary>
    public const string Proposed = "PROPOSED";

    /// <summary>Đã được phê duyệt (chỉ Ward Manager mới có quyền)</summary>
    public const string Approved = "APPROVED";

    /// <summary>Đã bị từ chối</summary>
    public const string Rejected = "REJECTED";

    /// <summary>Đã bị hủy</summary>
    public const string Canceled = "CANCELED";

    /// <summary>Đã hết hạn thanh toán</summary>
    public const string Expired = "EXPIRED";

    /// <summary>Đã thanh toán đặt cọc thành công</summary>
    public const string DepositPaid = "DEPOSIT_PAID";

    /// <summary>Danh sách tất cả trạng thái hợp lệ</summary>
    public static readonly IReadOnlyList<string> AllStatuses = new[]
    {
        Draft,
        Submitted,
        UnderReview,
        NeedMoreDocuments,
        Proposed,
        Approved,
        Rejected,
        Canceled,
        Expired,
        DepositPaid
    };

    /// <summary>
    /// Kiểm tra trạng thái có hợp lệ không.
    /// </summary>
    public static bool IsValid(string status) => AllStatuses.Contains(status);

    /// <summary>
    /// Định nghĩa các chuyển trạng thái hợp lệ theo vai trò nghiệp vụ.
    /// Key: trạng thái hiện tại → Value: các trạng thái có thể chuyển sang
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> VerificationOfficerTransitions =
        new Dictionary<string, string[]>
        {
            // VO có thể chuyển từ SUBMITTED → UNDER_REVIEW (nhận hồ sơ)
            [Submitted] = new[] { UnderReview },
            // VO kiểm tra và đề xuất, KHÔNG được chốt duyệt/từ chối
            [UnderReview] = new[] { Proposed, NeedMoreDocuments },
            // VO có thể chuyển từ NEED_MORE_DOCUMENTS → UNDER_REVIEW (sau khi người dân bổ sung)
            [NeedMoreDocuments] = new[] { UnderReview }
        };

    public static readonly IReadOnlyDictionary<string, string[]> WardManagerTransitions =
        new Dictionary<string, string[]>
        {
            // WM chốt quyết định cuối cùng từ hồ sơ VO đã đề xuất
            [Proposed] = new[] { Approved, Rejected, NeedMoreDocuments },
            // WM cũng có thể trực tiếp xét duyệt hồ sơ chưa qua VO (UNDER_REVIEW)
            [UnderReview] = new[] { Approved, Rejected, NeedMoreDocuments }
        };
}
