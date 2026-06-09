namespace RHS.Infrastructure.Exceptions;

/// <summary>
/// Ném ra khi chuyển trạng thái hồ sơ không hợp lệ theo luồng nghiệp vụ.
/// Ví dụ: Verification Officer cố Approve hồ sơ đang ở trạng thái DRAFT.
/// Controller nên bắt exception này và trả về HTTP 422 (Unprocessable Entity).
/// </summary>
public sealed class InvalidApplicationStatusTransitionException : HousingApplicationException
{
    // ── Mã lỗi nội bộ ────────────────────────────────────────────

    /// <summary>Trạng thái hiện tại không cho phép chuyển sang trạng thái mới.</summary>
    public const string CodeInvalidTransition = "APP_INVALID_STATUS_TRANSITION";

    /// <summary>Role hiện tại không có quyền thực hiện hành động này.</summary>
    public const string CodeUnauthorizedTransition = "APP_UNAUTHORIZED_TRANSITION";

    // ── Properties ────────────────────────────────────────────────

    /// <summary>Trạng thái hiện tại của hồ sơ.</summary>
    public string CurrentStatus { get; }

    /// <summary>Trạng thái muốn chuyển sang.</summary>
    public string TargetStatus { get; }

    // ── Constructors ─────────────────────────────────────────────

    /// <summary>
    /// Tạo exception khi chuyển trạng thái không hợp lệ.
    /// </summary>
    public InvalidApplicationStatusTransitionException(string currentStatus, string targetStatus)
        : base(
            CodeInvalidTransition,
            $"Không thể chuyển trạng thái từ '{currentStatus}' sang '{targetStatus}'.")
    {
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }

    /// <summary>
    /// Tạo exception khi role không có quyền thực hiện chuyển trạng thái.
    /// </summary>
    public InvalidApplicationStatusTransitionException(
        string currentStatus, string targetStatus, string requiredRole)
        : base(
            CodeUnauthorizedTransition,
            $"Role '{requiredRole}' không có quyền chuyển trạng thái từ '{currentStatus}' sang '{targetStatus}'.")
    {
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }
}
