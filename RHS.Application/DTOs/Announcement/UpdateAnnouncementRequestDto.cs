namespace RHS.Application.DTOs.Announcement;

public class UpdateAnnouncementRequestDto
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? AnnouncementType { get; set; }
    public string? LegalDocumentNumber { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public Guid? ProjectId { get; set; }
    public bool? IsPinned { get; set; }
    public string? Status { get; set; }
}
