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
    /// Tạo yêu cầu thanh toán đặt cọc cho hồ sơ đã APPROVED:
    /// 1. Validate hồ sơ đang ở trạng thái APPROVED
    /// 2. Lấy DepositAmount từ HousingProject
    /// 3. Lưu bản ghi Payment (Status=Pending) vào DB
    /// 4. Tạo URL redirect sang VNPay Sandbox
    /// </summary>
    /// <param name="userId">ID người dùng đang thanh toán (từ JWT)</param>
    /// <param name="dto">Chứa ApplicationId của hồ sơ cần thanh toán</param>
    /// <param name="httpContext">HttpContext để lấy IP client</param>
    /// <returns>PaymentResponseDto chứa PaymentUrl để redirect</returns>
    Task<PaymentResponseDto> CreatePaymentAsync(Guid userId, CreatePaymentDto dto, HttpContext httpContext);

    /// <summary>
    /// Xử lý callback từ VNPay (ReturnUrl — browser redirect):
    /// 1. Xác minh chữ ký HMAC-SHA512
    /// 2. Cập nhật trạng thái Payment trong DB (Paid / Failed)
    /// 3. Nếu Success → SlotCode + DEPOSIT_PAID (hoặc đánh dấu installment PAID)
    /// </summary>
    Task<bool> HandleCallbackAsync(IQueryCollection queryParams);

    /// <summary>
    /// Xử lý IPN từ VNPay Sandbox (server-to-server), idempotent.
    /// Trả RspCode theo chuẩn VNPay: 00 confirm, 02 already, 97 invalid signature, 01 not found.
    /// </summary>
    Task<VnPayIpnResultDto> HandleIpnAsync(IQueryCollection queryParams);

    /// <summary>
    /// Tra cứu thông tin giao dịch theo mã đơn hàng nội bộ.
    /// </summary>
    Task<PaymentInfoDto?> GetPaymentByOrderIdAsync(string orderId);

    /// <summary>
    /// Lấy lịch sử thanh toán của một người dùng.
    /// </summary>
    Task<IEnumerable<PaymentInfoDto>> GetPaymentsByUserIdAsync(Guid userId);

    /// <summary>
    /// Tra cứu kết quả thanh toán đặt cọc: SlotCode, PDF URL, thông tin giao dịch.
    /// Dùng cho FE hiển thị trang "Thanh toán thành công".
    /// </summary>
    /// <param name="orderId">Mã đơn hàng nội bộ</param>
    /// <returns>DepositPaymentResultDto hoặc null nếu không tìm thấy</returns>
    Task<DepositPaymentResultDto?> GetDepositResultAsync(string orderId);
}
