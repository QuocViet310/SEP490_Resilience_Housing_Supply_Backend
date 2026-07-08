namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Response DTO đầy đủ cho chi tiết một hồ sơ.
/// Bao gồm thông tin form đăng ký, danh sách tài liệu, và lịch sử xét duyệt.
/// </summary>
public class ApplicationDetailResponseDto
{
    // ── Thông tin hồ sơ ───────────────────────────────────────────
    public Guid ApplicationId { get; set; }
    public string ApplicationStatus { get; set; } = string.Empty;
    public decimal PriorityScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? FinalDecisionDate { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // ── Thông tin dự án ───────────────────────────────────────────
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;

    // ── Thông tin người đăng ký ───────────────────────────────────
    public Guid ApplicantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string? Occupation { get; set; }
    public string? WorkPlace { get; set; }
    public string CurrentResidence { get; set; } = string.Empty;
    public string PermanentAddress { get; set; } = string.Empty;
    public string HousingStatus { get; set; } = string.Empty;
    public string? MaritalStatus { get; set; }
    public int HouseholdMembersCount { get; set; }
    public string? PriorityGroup { get; set; }
    public string? ReceiptUrl { get; set; }

    // ── Cán bộ thẩm định ──────────────────────────────────────────
    public Guid? OfficerId { get; set; }
    public string? OfficerFullName { get; set; }

    // ── Danh sách tài liệu ────────────────────────────────────────
    public IEnumerable<ApplicationDocumentResponseDto> Documents { get; set; }
        = new List<ApplicationDocumentResponseDto>();

    // ── Lịch sử xét duyệt ────────────────────────────────────────
    public IEnumerable<ReviewHistoryResponseDto> ReviewHistories { get; set; }
        = new List<ReviewHistoryResponseDto>();
}

/// <summary>Response DTO cho một tài liệu đính kèm trong hồ sơ.</summary>
public class ApplicationDocumentResponseDto
{
    public Guid DocumentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public string? AiRejectedReason { get; set; }
    public DateTime UploadedAt { get; set; }
    public Guid UploadedBy { get; set; }
}

/// <summary>Response DTO cho một bản ghi lịch sử xét duyệt.</summary>
public class ReviewHistoryResponseDto
{
    public Guid HistoryId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }

    /// <summary>Tên người thực hiện hành động</summary>
    public Guid ChangedBy { get; set; }
    public string ChangedByFullName { get; set; } = string.Empty;
}
