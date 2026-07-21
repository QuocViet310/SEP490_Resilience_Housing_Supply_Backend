namespace RHS.Application.DTOs.Announcement;

public class CreateAnnouncementRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>LegalChange, DeadlineNotice, General.</summary>
    public string AnnouncementType { get; set; } = "General";

    /// <summary>Số hiệu văn bản pháp lý (nếu có).</summary>
    public string? LegalDocumentNumber { get; set; }

    /// <summary>Ngày có hiệu lực.</summary>
    public DateTime? EffectiveDate { get; set; }

    /// <summary>Ngày hết hạn / deadline nộp hồ sơ.</summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>Dự án liên quan (optional).</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Ghim lên đầu danh sách.</summary>
    public bool IsPinned { get; set; }

    /// <summary>Publish ngay hay lưu nháp? Default = Draft.</summary>
    public string Status { get; set; } = "Draft";
}
