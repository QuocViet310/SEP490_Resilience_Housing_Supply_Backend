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
    public string FormMatchStatus { get; set; } = string.Empty; // "MATCH", "MISMATCH", "ERROR", "MISSING"
    public string Status => FormMatchStatus;
    public string? Details { get; set; }
    public string? Note => Details;
}

public class ApplicationAuditResultDto
{
    public Guid ApplicationId { get; set; }
    public string PriorityGroup { get; set; } = string.Empty;
    public string HousingStatus { get; set; } = string.Empty;
    public bool IsComplete { get; set; }

    // FE status aliases
    public string Status => IsComplete ? "COMPLETE" : (CheckedDocuments.Any(d => d.FormMatchStatus == "MISSING") ? "INCOMPLETE" : "MISMATCH");
    public string OverallStatus => Status;
    public string StatusName => IsComplete ? "Đã xác minh" : (MissingDocumentTypes.Count > 0 ? "Thiếu giấy tờ" : "Cần kiểm tra lại");

    // Pass & total counters
    public int PassedCount => CheckedDocuments.Count(d => d.IsCorrectForm);
    public int TotalCount => CheckedDocuments.Count;

    // Document & Checklist list aliases
    public List<DocumentFormCheckDto> CheckedDocuments { get; set; } = new();
    public List<DocumentFormCheckDto> Checklist => CheckedDocuments;
    public List<DocumentFormCheckDto> Documents => CheckedDocuments;
    public List<DocumentFormCheckDto> ChecklistItems => CheckedDocuments;

    public List<string> MissingDocumentTypes { get; set; } = new();
    public List<string> MissingDocumentNames { get; set; } = new();
    public string SummaryNote { get; set; } = string.Empty;
    public string Summary => SummaryNote;
    public string RawText => SummaryNote;
    public DateTime AuditedAt { get; set; } = DateTime.UtcNow;
}
