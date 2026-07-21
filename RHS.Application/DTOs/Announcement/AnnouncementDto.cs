namespace RHS.Application.DTOs.Announcement;

/// <summary>DTO trả về chi tiết một Announcement.</summary>
public class AnnouncementDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AnnouncementType { get; set; } = string.Empty;
    public string? LegalDocumentNumber { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public bool IsPinned { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Danh sách file đính kèm.</summary>
    public List<AnnouncementAttachmentDto> Attachments { get; set; } = new();
}

/// <summary>DTO cho file đính kèm.</summary>
public class AnnouncementAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>DTO danh sách có phân trang.</summary>
public class PagedAnnouncementResultDto
{
    public IEnumerable<AnnouncementDto> Items { get; set; } = Enumerable.Empty<AnnouncementDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
