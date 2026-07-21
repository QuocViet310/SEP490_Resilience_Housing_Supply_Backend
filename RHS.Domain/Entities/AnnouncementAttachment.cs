namespace RHS.Domain.Entities;

public class AnnouncementAttachment
{
    public Guid Id { get; set; }

    public Guid AnnouncementId { get; set; }

    /// <summary>Tên file gốc do người dùng upload.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>URL file trên Cloudinary.</summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>Content type (application/pdf, image/png, ...).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Kích thước file (bytes).</summary>
    public long FileSize { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Announcement Announcement { get; set; } = null!;
}
