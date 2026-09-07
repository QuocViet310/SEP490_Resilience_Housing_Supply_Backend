using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Payment;

/// <summary>
/// DTO chứa bộ lọc và tham số phân trang tra cứu lịch sử giao dịch cá nhân người dùng
/// </summary>
public class UserTransactionQueryDto
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
}

/// <summary>
/// DTO phản hồi danh sách phân trang giao dịch cá nhân người dùng
/// </summary>
public class UserTransactionListResponseDto
{
    public List<PaymentInfoDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 1));
}
