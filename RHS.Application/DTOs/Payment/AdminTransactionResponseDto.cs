using System;
using System.Collections.Generic;

namespace RHS.Application.DTOs.Payment;

/// <summary>
/// DTO thông tin chi tiết một giao dịch dành cho Admin/Quản lý
/// </summary>
public class AdminTransactionDetailDto
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string OrderInfo { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    /// <summary>Pending | Success | Paid | Failed | Cancelled</summary>
    public string Status { get; set; } = string.Empty;

    // ── Thông tin Người thanh toán ──────────────────────────────────────
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserEmail { get; set; }
    public string? UserPhoneNumber { get; set; }

    // ── Thông tin Dự án & Hồ sơ ──────────────────────────────────────────
    public Guid? HousingProjectId { get; set; }
    public string? ProjectName { get; set; }

    public Guid? ApplicationId { get; set; }
    public string? SlotCode { get; set; }
    public string? PdfUrl { get; set; }

    // ── Thông tin phản hồi VNPay ─────────────────────────────────────────
    public string? VnpResponseCode { get; set; }
    public string? VnpTransactionNo { get; set; }
    public string? VnpBankCode { get; set; }
    public string? VnpBankTranNo { get; set; }
    public string? VnpCardType { get; set; }
    public string? VnpPayDate { get; set; }
    public string? VnpTransactionStatus { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

/// <summary>
/// DTO phản hồi danh sách phân trang giao dịch dành cho Admin
/// </summary>
public class AdminTransactionListResponseDto
{
    public List<AdminTransactionDetailDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 1));
}
