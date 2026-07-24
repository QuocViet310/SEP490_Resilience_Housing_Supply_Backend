using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.Payment;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly IInstallmentService _installmentService;

    public PaymentController(
        IPaymentService paymentService,
        IInstallmentService installmentService)
    {
        _paymentService = paymentService;
        _installmentService = installmentService;
    }

    /// <summary>
    /// [Bước 1 luồng] Tạo URL thanh toán đặt cọc cho hồ sơ đã APPROVED.
    /// Số tiền tự động lấy từ DepositAmount của dự án.
    /// </summary>
    /// <remarks>
    /// **Body chỉ cần ApplicationId:**
    /// ```json
    /// { "applicationId": "guid-of-approved-application" }
    /// ```
    /// 
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

            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message
                });
            }

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
    /// [Bước 2] ReturnUrl — browser redirect sau thanh toán (UX).
    /// IPN (`payment-ipn`) là nguồn xác nhận authoritative hơn.
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

            var responseCode = queryParams["vnp_ResponseCode"].ToString();
            var orderId      = queryParams["vnp_TxnRef"].ToString();
            var amount       = long.Parse(queryParams["vnp_Amount"].ToString()) / 100;

            if (responseCode == "00")
            {
                var depositResult = await _paymentService.GetDepositResultAsync(orderId);

                return Ok(new
                {
                    success = true,
                    message = "Thanh toán thành công",
                    data = new
                    {
                        orderId,
                        amount,
                        bankCode        = queryParams["vnp_BankCode"].ToString(),
                        transactionNo   = queryParams["vnp_TransactionNo"].ToString(),
                        payDate         = queryParams["vnp_PayDate"].ToString(),
                        slotCode        = depositResult?.SlotCode,
                        pdfUrl          = depositResult?.PdfUrl,
                        applicationId   = depositResult?.ApplicationId
                    }
                });
            }

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
    /// [Bước 2b] IPN VNPay Sandbox — server-to-server, idempotent.
    /// Trả JSON RspCode theo chuẩn VNPay (00 / 02 / 97 / 01).
    /// Cấu hình VnPay:IpnUrl = https://.../api/payment/payment-ipn
    /// </summary>
    [HttpGet("payment-ipn")]
    [HttpPost("payment-ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> PaymentIpn()
    {
        try
        {
            var result = await _paymentService.HandleIpnAsync(HttpContext.Request.Query);
            return Ok(new { RspCode = result.RspCode, Message = result.Message });
        }
        catch (Exception ex)
        {
            return Ok(new { RspCode = "99", Message = ex.Message });
        }
    }

    /// <summary>
    /// Tra cứu kết quả thanh toán đặt cọc: SlotCode, PDF hợp đồng, thông tin giao dịch.
    /// Dùng cho FE hiển thị trang "Thanh toán thành công".
    /// </summary>
    [HttpGet("deposit-result/{orderId}")]
    [Authorize]
    public async Task<IActionResult> GetDepositResult(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return BadRequest(new { success = false, message = "Mã đơn hàng không hợp lệ" });

        var result = await _paymentService.GetDepositResultAsync(orderId);

        if (result == null)
            return NotFound(new
            {
                success = false,
                message = "Không tìm thấy kết quả thanh toán đặt cọc hoặc giao dịch chưa thành công"
            });

        return Ok(new
        {
            success = true,
            data    = result
        });
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

    /// <summary>
    /// Tải hợp đồng nguyên tắc dưới dạng PDF.
    /// PDF được sinh on-demand từ dữ liệu hồ sơ trong DB (không lưu trên Cloudinary).
    /// </summary>
    [HttpGet("download-contract/{applicationId}")]
    [Authorize]
    public async Task<IActionResult> DownloadContract(
        Guid applicationId,
        [FromServices] IPdfContractService pdfContractService,
        [FromServices] RHS.Infrastructure.Data.AppDbContext context)
    {
        try
        {
            // Verify user owns this application hoặc là Officer/Admin
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { success = false, message = "Token không hợp lệ" });

            var application = await context.HousingApplications
                .Include(a => a.Officer)
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

            if (application == null)
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ" });

            // Chỉ cho phép applicant sở hữu hoặc officer/admin
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            if (application.ApplicantId != userId
                && !userRole.Contains("Admin")
                && !userRole.Contains("Officer"))
            {
                return Forbid();
            }

            // Cho tải PDF sau khi đã có HĐ nguyên tắc (CONTRACT_PENDING trở đi)
            var previewStatuses = new[]
            {
                ApplicationStatusConstants.ContractPending,
                ApplicationStatusConstants.ContractSigned,
                ApplicationStatusConstants.DepositPaid,
                ApplicationStatusConstants.FullyPaid
            };
            if (!previewStatuses.Contains(application.ApplicationStatus))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Hồ sơ chưa đến bước hợp đồng nguyên tắc. Chỉ tải PDF sau khi được chốt/trúng và chuyển CONTRACT_PENDING."
                });
            }

            var project = await context.HousingProjects
                .FirstOrDefaultAsync(p => p.Id == application.ProjectId);

            if (project == null)
                return NotFound(new { success = false, message = "Không tìm thấy dự án" });

            // Payment có thể chưa có (xem trước HĐ trước khi đóng cọc)
            var payment = await context.Payments
                .Where(p => p.ApplicationId == applicationId
                            && (p.Status == "Success" || p.Status == "Paid"))
                .OrderByDescending(p => p.PaidAt)
                .FirstOrDefaultAsync();

            var approvedHistory = await context.ApplicationStatusHistories
                .Include(h => h.ChangedByUser)
                .Where(h => h.ApplicationId == applicationId
                         && (h.NewStatus == "APPROVED" || h.NewStatus == "APPROVED_BY_TIMEOUT"
                             || h.NewStatus == "CONTRACT_PENDING"))
                .OrderByDescending(h => h.ChangedAt)
                .FirstOrDefaultAsync();

            var wardManagerName = approvedHistory?.ChangedByUser?.FullName
                ?? "Ban Quản lý Dự án";

            var slotCode = !string.IsNullOrEmpty(application.SlotCode)
                ? application.SlotCode
                : $"PENDING-{application.ApplicationId.ToString()[..8].ToUpperInvariant()}";

            var pdfBytes = pdfContractService.GeneratePdfBytesOnly(
                application, project, slotCode,
                payment?.Amount ?? project.DepositAmount,
                payment?.VnpTransactionNo,
                wardManagerName);

            var fileName = $"HopDong_{slotCode}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi tạo hợp đồng PDF",
                error   = ex.Message
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Installment — Lịch đóng tiền theo đợt
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lấy tổng hợp lịch đóng tiền (tất cả đợt) cho một hồ sơ.
    /// Bao gồm: tổng tiền, đã đóng, còn lại, chi tiết từng đợt.
    /// </summary>
    [HttpGet("installments/{applicationId}")]
    [Authorize]
    public async Task<IActionResult> GetInstallments(Guid applicationId)
    {
        try
        {
            var summary = await _installmentService.GetSummaryAsync(applicationId);

            if (summary == null)
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ." });

            return Ok(new { success = true, data = summary });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi lấy lịch đóng tiền.",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Tạo URL VNPay thanh toán cho một đợt cụ thể (PaymentInstallment).
    /// Chỉ cho thanh toán đợt PENDING/OVERDUE, phải đúng thứ tự.
    /// </summary>
    [HttpPost("installments/{installmentId}/pay")]
    [Authorize]
    public async Task<IActionResult> PayInstallment(Guid installmentId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var result = await _installmentService.CreateInstallmentPaymentAsync(
                userId, installmentId, HttpContext);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new
            {
                success = true,
                message = result.Message,
                data = new
                {
                    paymentUrl = result.PaymentUrl,
                    orderId = result.OrderId,
                    amount = result.Amount
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Không thể tạo URL thanh toán.",
                error = ex.Message
            });
        }
    }

}
