using System;

namespace RHS.Application.DTOs.Reports;

/// <summary>
/// DTO chứa tham số lọc khi xuất báo cáo danh sách hồ sơ nhà ở xã hội.
/// </summary>
public class ExportApplicationFilterDto
{
    /// <summary>
    /// ID dự án (nếu null sẽ xuất cho tất cả dự án)
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Trạng thái hồ sơ lọc (VD: SUBMITTED, REVIEWING, PENDING_SXD_REVIEW, APPROVED, REJECTED, DEPOSIT_PAID...)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Từ ngày nộp (UTC / Local)
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Đến ngày nộp (UTC / Local)
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Từ khóa tìm kiếm theo tên, CCCD, mã hồ sơ
    /// </summary>
    public string? SearchTerm { get; set; }
}
