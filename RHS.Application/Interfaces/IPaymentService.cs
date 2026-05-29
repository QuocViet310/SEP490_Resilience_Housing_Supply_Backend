using Microsoft.AspNetCore.Http;
using RHS.Application.DTOs.Payment;
using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ thanh toán của hệ thống RHS.
/// Kết hợp IVnPayService (tạo URL) và IPaymentRepository (lưu DB).
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Tạo yêu cầu thanh toán mới:
    /// 1. Lưu bản ghi Payment (Status=Pending) vào DB
    /// 2. Tạo URL redirect sang VNPay Sandbox
    /// </summary>
    /// <param name="userId">ID người dùng đang thanh toán</param>
    /// <param name="dto">Thông tin đơn hàng</param>
    /// <param name="httpContext">HttpContext để lấy IP client</param>
    /// <returns>PaymentResponseDto chứa PaymentUrl để redirect</returns>
    Task<PaymentResponseDto> CreatePaymentAsync(Guid userId, CreatePaymentDto dto, HttpContext httpContext);

    /// <summary>
    /// Xử lý callback từ VNPay:
    /// 1. Xác minh chữ ký HMAC-SHA512
    /// 2. Cập nhật trạng thái Payment trong DB (Success / Failed)
    /// </summary>
    /// <param name="queryParams">Query parameters từ VNPay ReturnUrl</param>
    /// <returns>True nếu callback hợp lệ và đã xử lý thành công</returns>
    Task<bool> HandleCallbackAsync(IQueryCollection queryParams);

    /// <summary>
    /// Tra cứu thông tin giao dịch theo mã đơn hàng nội bộ.
    /// </summary>
    Task<PaymentInfoDto?> GetPaymentByOrderIdAsync(string orderId);

    /// <summary>
    /// Lấy lịch sử thanh toán của một người dùng.
    /// </summary>
    Task<IEnumerable<PaymentInfoDto>> GetPaymentsByUserIdAsync(Guid userId);
}
