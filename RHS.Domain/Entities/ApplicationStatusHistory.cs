namespace RHS.Domain.Entities;

/// <summary>
/// Lịch sử xét duyệt hồ sơ (Review History).
/// Ghi lại mọi hành động của Verification Officer và Ward Manager:
/// ai làm, hành động gì, thời gian, ghi chú.
/// </summary>
public class ApplicationStatusHistory
{
    public Guid HistoryId { get; set; }

    public Guid ApplicationId { get; set; }

    /// <summary>ID người thực hiện hành động (VO hoặc WM)</summary>
    public Guid ChangedBy { get; set; }

    /// <summary>
    /// Hành động được thực hiện.
    /// Ví dụ: APPROVE, REJECT, REQUEST_MORE_DOCUMENTS, ASSIGN_OFFICER
    /// </summary>
    public string Action { get; set; } = string.Empty;

    public string OldStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    /// <summary>Ghi chú/lý do của officer (bắt buộc khi Reject hoặc NeedMoreDocuments)</summary>
    public string? Note { get; set; }

    public DateTime ChangedAt { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Navigation properties
    // ─────────────────────────────────────────────────────────────

    public HousingApplication Application { get; set; } = null!;

    public User ChangedByUser { get; set; } = null!;
}
