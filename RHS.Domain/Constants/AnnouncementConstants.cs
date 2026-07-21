namespace RHS.Domain.Constants;

public static class AnnouncementConstants
{
    // ── Announcement Types ──────────────────────────────────────
    /// <summary>Thay đổi pháp lý (nghị định, thông tư, quyết định).</summary>
    public const string TypeLegalChange = "LegalChange";

    /// <summary>Thời hạn nộp hồ sơ.</summary>
    public const string TypeDeadlineNotice = "DeadlineNotice";

    /// <summary>Thông báo chung.</summary>
    public const string TypeGeneral = "General";

    public static readonly string[] ValidTypes =
        { TypeLegalChange, TypeDeadlineNotice, TypeGeneral };

    // ── Announcement Statuses ───────────────────────────────────
    public const string StatusDraft = "Draft";
    public const string StatusPublished = "Published";
    public const string StatusArchived = "Archived";

    public static readonly string[] ValidStatuses =
        { StatusDraft, StatusPublished, StatusArchived };

    // ── File Upload ─────────────────────────────────────────────
    /// <summary>Cloudinary folder cho attachments.</summary>
    public const string AttachmentFolder = "announcements";

    /// <summary>Max file size: 10 MB.</summary>
    public const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;

    /// <summary>Max số file đính kèm mỗi announcement.</summary>
    public const int MaxAttachmentsPerAnnouncement = 5;

    /// <summary>Các content type được phép.</summary>
    public static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    };
}
