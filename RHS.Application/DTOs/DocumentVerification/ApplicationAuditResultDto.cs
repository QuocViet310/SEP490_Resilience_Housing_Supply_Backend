using System;
using System.Collections.Generic;

namespace RHS.Application.DTOs.DocumentVerification;

public class DocumentFormCheckDto
{
    public Guid DocumentId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentTypeName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public bool IsCorrectForm { get; set; }
    public string FormMatchStatus { get; set; } = string.Empty; // "MATCH", "MISMATCH", "ERROR"
    public string? Details { get; set; }
}

public class ApplicationAuditResultDto
{
    public Guid ApplicationId { get; set; }
    public string PriorityGroup { get; set; } = string.Empty;
    public string HousingStatus { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public List<DocumentFormCheckDto> CheckedDocuments { get; set; } = new();
    public List<string> MissingDocumentTypes { get; set; } = new();
    public List<string> MissingDocumentNames { get; set; } = new();
    public string SummaryNote { get; set; } = string.Empty;
    public DateTime AuditedAt { get; set; } = DateTime.UtcNow;
}
