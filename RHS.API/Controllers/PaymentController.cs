using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Payment;
using RHS.Application.Interfaces;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    /// <summary>
    /// [Bước 1 luồng] Tạo URL thanh toán VNPay Sandbox.
    /// Client redirect người dùng đến URL trả về để thực hiện thanh toán.
    /// </summary>
    /// <remarks>
    /// **Test card Sandbox NCB:**
    /// - Số thẻ: 9704198526191432198
    /// - Ngày hết hạn: 07/15
    /// - OTP: 123456
    /// </remarks>
    [HttpPost("create-payment-url")]
    [Authorize]
    public async Task<IActionResult> CreatePaymentUrl([FromBody] CreatePaymentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Lấy userId từ JWT
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var result = await _paymentService.CreatePaymentAsync(userId, dto, HttpContext);

            return Ok(new
            {
                success    = result.Success,
                message    = result.Message,
                data = new
                {
                    paymentUrl = result.PaymentUrl,
                    orderId    = result.OrderId,
                    amount     = result.Amount
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Không thể tạo URL thanh toán",
                error   = ex.Message
            });
        }
    }

    /// <summary>
    /// [Bước 2 luồng] Callback VNPay gọi về sau khi người dùng thanh toán.
    /// Endpoint này xác minh chữ ký và cập nhật trạng thái giao dịch trong DB.
    /// ⚠️ AllowAnonymous vì VNPay gọi trực tiếp (không có JWT).
    /// </summary>
    [HttpGet("payment-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> PaymentCallback()
    {
        try
        {
            var queryParams = HttpContext.Request.Query;
            var isHandled   = await _paymentService.HandleCallbackAsync(queryParams);

            if (!isHandled)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Xác minh chữ ký thất bại hoặc giao dịch không tồn tại"
                });
            }

            // Đọc kết quả để trả về phản hồi tường minh
            var responseCode = queryParams["vnp_ResponseCode"].ToString();
            var orderId      = queryParams["vnp_TxnRef"].ToString();
            var amount       = long.Parse(queryParams["vnp_Amount"].ToString()) / 100; // chia 100 lấy lại VND

            if (responseCode == "00")
            {
                return Ok(new
                {
                    success = true,
                    message = "Thanh toán thành công",
                    data = new
                    {
                        orderId,
                        amount,
                        bankCode      = queryParams["vnp_BankCode"].ToString(),
                        transactionNo = queryParams["vnp_TransactionNo"].ToString(),
                        payDate       = queryParams["vnp_PayDate"].ToString()
                    }
                });
            }

            // Giao dịch thất bại hoặc bị hủy
            var status = responseCode == "24" ? "Cancelled" : "Failed";
            return Ok(new
            {
                success = false,
                message = status == "Cancelled"
                    ? "Giao dịch đã bị hủy"
                    : "Thanh toán thất bại",
                data = new
                {
                    orderId,
                    responseCode,
                    status
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi xử lý callback",
                error   = ex.Message
            });
        }
    }

    /// <summary>
    /// Tra cứu thông tin chi tiết một giao dịch theo mã đơn hàng.
    /// </summary>
    [HttpGet("payment-info/{orderId}")]
    [Authorize]
    public async Task<IActionResult> GetPaymentInfo(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return BadRequest(new { success = false, message = "Mã đơn hàng không hợp lệ" });

        var payment = await _paymentService.GetPaymentByOrderIdAsync(orderId);

        if (payment == null)
            return NotFound(new { success = false, message = "Không tìm thấy giao dịch" });

        return Ok(new
        {
            success = true,
            data    = payment
        });
    }

    /// <summary>
    /// Lấy lịch sử tất cả giao dịch của người dùng hiện tại.
    /// </summary>
    [HttpGet("my-payments")]
    [Authorize]
    public async Task<IActionResult> GetMyPayments()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var payments = await _paymentService.GetPaymentsByUserIdAsync(userId);

        return Ok(new
        {
            success = true,
            data    = payments
        });
    }
}
