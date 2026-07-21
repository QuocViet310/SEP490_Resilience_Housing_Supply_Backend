using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHS.Application.DTOs.Announcement;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly AppDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AnnouncementService> _logger;

    public AnnouncementService(
        AppDbContext db,
        IFileStorageService fileStorage,
        INotificationService notificationService,
        ILogger<AnnouncementService> logger)
    {
        _db = db;
        _fileStorage = fileStorage;
        _notificationService = notificationService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════
    // CREATE
    // ═══════════════════════════════════════════════════════════════

    public async Task<AnnouncementDto> CreateAsync(
        Guid createdBy, CreateAnnouncementRequestDto request, CancellationToken ct = default)
    {
        // Validate type
        if (!AnnouncementConstants.ValidTypes.Contains(request.AnnouncementType))
            throw new ArgumentException(
                $"AnnouncementType không hợp lệ. Chấp nhận: {string.Join(", ", AnnouncementConstants.ValidTypes)}");

        // Validate status
        if (!AnnouncementConstants.ValidStatuses.Contains(request.Status))
            throw new ArgumentException(
                $"Status không hợp lệ. Chấp nhận: {string.Join(", ", AnnouncementConstants.ValidStatuses)}");

        // Validate title
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Tiêu đề không được để trống.");

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ArgumentException("Nội dung không được để trống.");

        // Validate project exists (if provided)
        if (request.ProjectId.HasValue)
        {
            var projectExists = await _db.HousingProjects
                .AnyAsync(p => p.Id == request.ProjectId.Value, ct);
            if (!projectExists)
                throw new ArgumentException("Dự án không tồn tại.");
        }

        var entity = new Announcement
        {
            Id = Guid.NewGuid(),
            CreatedBy = createdBy,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            AnnouncementType = request.AnnouncementType,
            LegalDocumentNumber = request.LegalDocumentNumber?.Trim(),
            EffectiveDate = request.EffectiveDate,
            ExpirationDate = request.ExpirationDate,
            ProjectId = request.ProjectId,
            IsPinned = request.IsPinned,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow
        };

        _db.Announcements.Add(entity);
        await _db.SaveChangesAsync(ct);

        // If published immediately, send notifications to all Applicants
        if (entity.Status == AnnouncementConstants.StatusPublished)
        {
            await NotifyApplicantsAsync(entity, ct);
        }

        _logger.LogInformation(
            "Announcement {Id} created by {UserId} with status {Status}",
            entity.Id, createdBy, entity.Status);

        return await GetByIdInternalAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("Không thể tải announcement vừa tạo.");
    }

    // ═══════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════

    public async Task<AnnouncementDto> UpdateAsync(
        Guid announcementId, Guid userId, UpdateAnnouncementRequestDto request, CancellationToken ct = default)
    {
        var entity = await _db.Announcements
            .FirstOrDefaultAsync(a => a.Id == announcementId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy thông báo.");

        var oldStatus = entity.Status;

        // Validate type if provided
        if (request.AnnouncementType != null &&
            !AnnouncementConstants.ValidTypes.Contains(request.AnnouncementType))
            throw new ArgumentException(
                $"AnnouncementType không hợp lệ. Chấp nhận: {string.Join(", ", AnnouncementConstants.ValidTypes)}");

        // Validate status if provided
        if (request.Status != null &&
            !AnnouncementConstants.ValidStatuses.Contains(request.Status))
            throw new ArgumentException(
                $"Status không hợp lệ. Chấp nhận: {string.Join(", ", AnnouncementConstants.ValidStatuses)}");

        // Validate project if provided
        if (request.ProjectId.HasValue)
        {
            var projectExists = await _db.HousingProjects
                .AnyAsync(p => p.Id == request.ProjectId.Value, ct);
            if (!projectExists)
                throw new ArgumentException("Dự án không tồn tại.");
        }

        // Apply partial updates
        if (request.Title != null)
            entity.Title = request.Title.Trim();
        if (request.Content != null)
            entity.Content = request.Content.Trim();
        if (request.AnnouncementType != null)
            entity.AnnouncementType = request.AnnouncementType;
        if (request.LegalDocumentNumber != null)
            entity.LegalDocumentNumber = request.LegalDocumentNumber.Trim();
        if (request.EffectiveDate.HasValue)
            entity.EffectiveDate = request.EffectiveDate;
        if (request.ExpirationDate.HasValue)
            entity.ExpirationDate = request.ExpirationDate;
        if (request.ProjectId.HasValue)
            entity.ProjectId = request.ProjectId;
        if (request.IsPinned.HasValue)
            entity.IsPinned = request.IsPinned.Value;
        if (request.Status != null)
            entity.Status = request.Status;

        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // If status changed from Draft → Published, send notifications
        if (oldStatus != AnnouncementConstants.StatusPublished &&
            entity.Status == AnnouncementConstants.StatusPublished)
        {
            await NotifyApplicantsAsync(entity, ct);
        }

        _logger.LogInformation(
            "Announcement {Id} updated by {UserId}",
            announcementId, userId);

        return await GetByIdInternalAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("Không thể tải announcement sau cập nhật.");
    }

    // ═══════════════════════════════════════════════════════════════
    // DELETE (soft)
    // ═══════════════════════════════════════════════════════════════

    public async Task DeleteAsync(Guid announcementId, Guid userId, CancellationToken ct = default)
    {
        var entity = await _db.Announcements
            .FirstOrDefaultAsync(a => a.Id == announcementId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy thông báo.");

        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Announcement {Id} soft-deleted by {UserId}",
            announcementId, userId);
    }

    // ═══════════════════════════════════════════════════════════════
    // GET BY ID
    // ═══════════════════════════════════════════════════════════════

    public async Task<AnnouncementDto?> GetByIdAsync(Guid announcementId, CancellationToken ct = default)
    {
        return await GetByIdInternalAsync(announcementId, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // GET PUBLISHED (public)
    // ═══════════════════════════════════════════════════════════════

    public async Task<PagedAnnouncementResultDto> GetPublishedAsync(
        int page, int pageSize,
        string? type = null, string? search = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Announcements
            .AsNoTracking()
            .Include(a => a.CreatedByUser)
            .Include(a => a.Project)
            .Include(a => a.Attachments)
            .Where(a => a.Status == AnnouncementConstants.StatusPublished);

        // Filter by type
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(a => a.AnnouncementType == type);

        // Search by title or content
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a =>
                a.Title.ToLower().Contains(s) ||
                a.Content.ToLower().Contains(s) ||
                (a.LegalDocumentNumber != null && a.LegalDocumentNumber.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync(ct);

        return new PagedAnnouncementResultDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // GET ALL FOR MANAGEMENT (SXD/Admin)
    // ═══════════════════════════════════════════════════════════════

    public async Task<PagedAnnouncementResultDto> GetAllForManagementAsync(
        int page, int pageSize,
        string? type = null, string? status = null, string? search = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Announcements
            .AsNoTracking()
            .Include(a => a.CreatedByUser)
            .Include(a => a.Project)
            .Include(a => a.Attachments)
            .AsQueryable();

        // Filter by type
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(a => a.AnnouncementType == type);

        // Filter by status
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a =>
                a.Title.ToLower().Contains(s) ||
                a.Content.ToLower().Contains(s) ||
                (a.LegalDocumentNumber != null && a.LegalDocumentNumber.ToLower().Contains(s)));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync(ct);

        return new PagedAnnouncementResultDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // ATTACHMENTS
    // ═══════════════════════════════════════════════════════════════

    public async Task<AnnouncementAttachmentDto> AddAttachmentAsync(
        Guid announcementId, Guid userId, IFormFile file, CancellationToken ct = default)
    {
        var entity = await _db.Announcements
            .Include(a => a.Attachments)
            .FirstOrDefaultAsync(a => a.Id == announcementId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy thông báo.");

        // Check max attachments
        if (entity.Attachments.Count >= AnnouncementConstants.MaxAttachmentsPerAnnouncement)
            throw new ArgumentException(
                $"Tối đa {AnnouncementConstants.MaxAttachmentsPerAnnouncement} file đính kèm mỗi thông báo.");

        // Validate file
        if (file == null || file.Length == 0)
            throw new ArgumentException("File không hợp lệ.");

        if (file.Length > AnnouncementConstants.MaxAttachmentSizeBytes)
            throw new ArgumentException("File vượt quá kích thước cho phép (10MB).");

        if (!AnnouncementConstants.AllowedContentTypes.Contains(file.ContentType.ToLower()))
            throw new ArgumentException(
                $"Loại file không được phép. Chấp nhận: {string.Join(", ", AnnouncementConstants.AllowedContentTypes)}");

        // Upload to Cloudinary
        string fileUrl;
        if (file.ContentType.ToLower() == "application/pdf")
        {
            fileUrl = await _fileStorage.UploadPdfAsync(file, AnnouncementConstants.AttachmentFolder);
        }
        else
        {
            fileUrl = await _fileStorage.UploadImageAsync(file, AnnouncementConstants.AttachmentFolder);
        }

        var attachment = new AnnouncementAttachment
        {
            Id = Guid.NewGuid(),
            AnnouncementId = announcementId,
            FileName = file.FileName,
            FileUrl = fileUrl,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        _db.Set<AnnouncementAttachment>().Add(attachment);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Attachment {AttachmentId} added to Announcement {AnnouncementId} by {UserId}",
            attachment.Id, announcementId, userId);

        return MapAttachmentToDto(attachment);
    }

    public async Task DeleteAttachmentAsync(
        Guid announcementId, Guid attachmentId, Guid userId, CancellationToken ct = default)
    {
        var announcement = await _db.Announcements
            .FirstOrDefaultAsync(a => a.Id == announcementId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy thông báo.");

        var attachment = await _db.Set<AnnouncementAttachment>()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.AnnouncementId == announcementId, ct)
            ?? throw new InvalidOperationException("Không tìm thấy file đính kèm.");

        // Delete from Cloudinary
        try
        {
            if (attachment.ContentType.ToLower() == "application/pdf")
            {
                // PDF files don't have a dedicated delete method — use image delete which works for raw files too
                await _fileStorage.DeleteImageAsync(attachment.FileUrl);
            }
            else
            {
                await _fileStorage.DeleteImageAsync(attachment.FileUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to delete attachment file from storage: {FileUrl}", attachment.FileUrl);
        }

        _db.Set<AnnouncementAttachment>().Remove(attachment);
        announcement.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Attachment {AttachmentId} deleted from Announcement {AnnouncementId} by {UserId}",
            attachmentId, announcementId, userId);
    }

    // ═══════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════

    private async Task<AnnouncementDto?> GetByIdInternalAsync(Guid id, CancellationToken ct)
    {
        var entity = await _db.Announcements
            .AsNoTracking()
            .Include(a => a.CreatedByUser)
            .Include(a => a.Project)
            .Include(a => a.Attachments)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        return entity is null ? null : MapToDto(entity);
    }

    /// <summary>
    /// Gửi notification in-app cho tất cả Applicant khi có announcement mới được publish.
    /// </summary>
    private async Task NotifyApplicantsAsync(Announcement announcement, CancellationToken ct)
    {
        try
        {
            var applicantIds = await _db.Users
                .AsNoTracking()
                .Where(u => u.Role.RoleName == RoleConstants.Applicant && u.Status == "Active")
                .Select(u => u.Id)
                .ToListAsync(ct);

            var title = $"📢 Thông báo mới: {announcement.Title}";
            var content = announcement.Content.Length > 200
                ? announcement.Content[..200] + "..."
                : announcement.Content;

            foreach (var applicantId in applicantIds)
            {
                await _notificationService.SendAsync(
                    applicantId,
                    title,
                    content,
                    NotificationTypeConstants.AnnouncementPublished);
            }

            _logger.LogInformation(
                "Sent announcement notification to {Count} applicants for Announcement {Id}",
                applicantIds.Count, announcement.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send notifications for Announcement {Id}", announcement.Id);
        }
    }

    private static AnnouncementDto MapToDto(Announcement a) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Content = a.Content,
        AnnouncementType = a.AnnouncementType,
        LegalDocumentNumber = a.LegalDocumentNumber,
        EffectiveDate = a.EffectiveDate,
        ExpirationDate = a.ExpirationDate,
        ProjectId = a.ProjectId,
        ProjectName = a.Project?.ProjectName,
        IsPinned = a.IsPinned,
        Status = a.Status,
        CreatedBy = a.CreatedBy,
        CreatedByName = a.CreatedByUser?.FullName ?? string.Empty,
        CreatedAt = a.CreatedAt,
        UpdatedAt = a.UpdatedAt,
        Attachments = a.Attachments?.Select(MapAttachmentToDto).ToList() ?? new()
    };

    private static AnnouncementAttachmentDto MapAttachmentToDto(AnnouncementAttachment att) => new()
    {
        Id = att.Id,
        FileName = att.FileName,
        FileUrl = att.FileUrl,
        ContentType = att.ContentType,
        FileSize = att.FileSize,
        UploadedAt = att.UploadedAt
    };
}
