using System;
using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Payment;

/// <summary>
/// DTO chứa bộ lọc và tham số phân trang tra cứu lịch sử giao dịch dành cho Admin
/// </summary>
public class AdminTransactionQueryDto
{
    /// <summary>Trang hiện tại (bắt đầu từ 1)</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page phải lớn hơn 0.")]
    public int Page { get; set; } = 1;

    /// <summary>Số lượng bản ghi trên một trang</summary>
    [Range(1, 100, ErrorMessage = "PageSize phải từ 1 đến 100.")]
    public int PageSize { get; set; } = 10;

    /// <summary>Lọc theo trạng thái: Pending | Success | Paid | Failed | Cancelled</summary>
    public string? Status { get; set; }

    /// <summary>Lọc theo ID dự án</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Lọc theo ID người dùng thanh toán</summary>
    public Guid? UserId { get; set; }

    /// <summary>Từ ngày tạo/thanh toán (UTC)</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Đến ngày tạo/thanh toán (UTC)</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Từ khóa tìm kiếm (Mã OrderId, Mã giao dịch VNPay, Họ tên/Email/SĐT người dùng)</summary>
    public string? SearchKeyword { get; set; }
}
