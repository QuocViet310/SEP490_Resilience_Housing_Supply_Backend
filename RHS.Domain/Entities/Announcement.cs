namespace RHS.Domain.Entities;

public class Announcement
{
    public Guid Id { get; set; }

    /// <summary>Người tạo (SXD hoặc Admin).</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>Tiêu đề thông báo.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Nội dung chi tiết (HTML hoặc plain text).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Loại thông báo: LegalChange, DeadlineNotice, General.</summary>
    public string AnnouncementType { get; set; } = "General";

    /// <summary>Số hiệu văn bản pháp lý (nếu có), ví dụ "NĐ100/2024/NĐ-CP".</summary>
    public string? LegalDocumentNumber { get; set; }

    /// <summary>Ngày có hiệu lực (nếu là văn bản pháp lý).</summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>Ngày hết hạn / deadline nộp hồ sơ (nếu có).</summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>Dự án liên quan (optional).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Ghim lên đầu danh sách.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Trạng thái: Draft, Published, Archived.</summary>
    public string Status { get; set; } = "Draft";

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public User CreatedByUser { get; set; } = null!;
    public HousingProject? Project { get; set; }
    public ICollection<AnnouncementAttachment> Attachments { get; set; } = new List<AnnouncementAttachment>();
}
