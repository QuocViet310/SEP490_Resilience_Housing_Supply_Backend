using Microsoft.AspNetCore.Http;

namespace RHS.Application.Interfaces;

/// <summary>
/// Dịch vụ tương tác trực tiếp với cổng thanh toán VNPay.
/// Chịu trách nhiệm tạo URL và xác minh chữ ký HMAC-SHA512.
/// </summary>
public interface IVnPayService
{
    /// <summary>
    /// Tạo URL thanh toán VNPay từ thông tin đơn hàng.
    /// </summary>
    /// <param name="context">HttpContext để lấy IP của client</param>
    /// <param name="request">Thông tin giao dịch cần thanh toán</param>
    /// <returns>URL đầy đủ để redirect sang VNPay Gateway</returns>
    string CreatePaymentUrl(HttpContext context, VnPaymentRequest request);

    /// <summary>
    /// Xác minh chữ ký HMAC-SHA512 từ callback của VNPay.
    /// </summary>
    /// <param name="queryParams">Tập hợp query parameters từ VNPay ReturnUrl</param>
    /// <returns>True nếu chữ ký hợp lệ</returns>
    bool ValidateSignature(IQueryCollection queryParams);
}

/// <summary>Model trung gian truyền dữ liệu vào VnPayService</summary>
public class VnPaymentRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string OrderInfo { get; set; } = string.Empty;
    public string OrderType { get; set; } = "other";
    public decimal Amount { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;
}
