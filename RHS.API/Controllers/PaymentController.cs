using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.Installment;
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
    private readonly IConfiguration _configuration;

    public PaymentController(
        IPaymentService paymentService,
        IInstallmentService installmentService,
        IConfiguration configuration)
    {
        _paymentService = paymentService;
        _installmentService = installmentService;
        _configuration = configuration;
    }

    /// <summary>
    /// [Bước 1 luồng] Tạo URL thanh toán Đợt 1 (20% giá căn) sau khi ký HĐ.
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
    /// Browser → 302 về Frontend (#/payments?payment=...).
    /// Mobile/API (Accept: application/json hoặc ?format=json) → JSON.
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

            var responseCode = queryParams["vnp_ResponseCode"].ToString();
            var orderId      = queryParams["vnp_TxnRef"].ToString();
            var paymentNotice = !isHandled
                ? "error"
                : responseCode == "00"
                    ? "success"
                    : responseCode == "24"
                        ? "cancelled"
                        : "failed";

            // Browser (VNPay redirect): đưa user về FE, không để kẹt trang JSON API
            if (!WantsJsonCallbackResponse())
            {
                var frontend = (_configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
                var redirect =
                    $"{frontend}/#/payments?payment={Uri.EscapeDataString(paymentNotice)}" +
                    (string.IsNullOrEmpty(orderId) ? "" : $"&orderId={Uri.EscapeDataString(orderId)}");
                return Redirect(redirect);
            }

            if (!isHandled)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Xác minh chữ ký thất bại hoặc giao dịch không tồn tại"
                });
            }

            var amountRaw = queryParams["vnp_Amount"].ToString();
            var amount = long.TryParse(amountRaw, out var amt) ? amt / 100 : 0L;

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
            if (!WantsJsonCallbackResponse())
            {
                var frontend = (_configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
                return Redirect($"{frontend}/#/payments?payment=error");
            }

            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi xử lý callback",
                error   = ex.Message
            });
        }
    }

    /// <summary>
    /// Mobile gọi callback bằng fetch + Accept JSON; browser từ VNPay nhận HTML → redirect FE.
    /// </summary>
    private bool WantsJsonCallbackResponse()
    {
        if (string.Equals(Request.Query["format"], "json", StringComparison.OrdinalIgnoreCase))
            return true;

        var accept = Request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
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
    /// Tải Hợp đồng mua bán nhà ở xã hội (PDF) — Mẫu số 01 Phụ lục VI TT 05/2024/TT-BXD.
    /// PDF sinh on-demand từ dữ liệu hồ sơ trong DB.
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
                .Include(a => a.Applicant)
                .Include(a => a.Apartment)
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

            if (application == null)
                return NotFound(new { success = false, message = "Không tìm thấy hồ sơ" });

            // Chỉ cho phép applicant sở hữu hoặc officer/admin/CĐT/SXD
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            var isStaff = userRole.Contains("Admin")
                          || userRole.Contains("Officer")
                          || userRole.Contains("Developer")
                          || userRole.Contains("Construction");
            if (application.ApplicantId != userId && !isStaff)
            {
                return Forbid();
            }

            // Cho tải PDF từ CONTRACT_PENDING trở đi
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
                    message = "Hồ sơ chưa đến bước hợp đồng mua bán. Chỉ tải PDF sau khi được chốt/trúng và chuyển CONTRACT_PENDING."
                });
            }

            var project = await context.HousingProjects
                .Include(p => p.Developer)
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

            var wardManagerName = project.Developer?.FullName
                ?? approvedHistory?.ChangedByUser?.FullName
                ?? "Ban Quản lý Dự án";

            var slotCode = !string.IsNullOrEmpty(application.SlotCode)
                ? application.SlotCode
                : $"PENDING-{application.ApplicationId.ToString()[..8].ToUpperInvariant()}";

            var phase1Milestone = await context.PaymentMilestones
                .AsNoTracking()
                .Where(m => m.ProjectId == application.ProjectId && m.PhaseOrder == 1 && m.IsActive)
                .FirstOrDefaultAsync();

            var phase1Pct = phase1Milestone?.Percentage ?? 20m;
            var phase1Fallback = application.Apartment != null && phase1Pct > 0
                ? Math.Round(application.Apartment.Price * phase1Pct / 100m, 0, MidpointRounding.AwayFromZero)
                : 0m;

            var pdfBytes = pdfContractService.GeneratePdfBytesOnly(
                application, project, slotCode,
                payment?.Amount ?? phase1Fallback,
                payment?.VnpTransactionNo,
                wardManagerName);

            var fileName = $"HopDongMuaBanNOXH_{slotCode}.pdf";
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

    /// <summary>
    /// Xem trước bảng kê phạt cọc Đợt 1, tổng tiền Đợt 2+ đã đóng, khấu trừ phạt và số tiền thực hoàn khi hủy căn.
    /// </summary>
    [HttpGet("applications/{applicationId}/cancellation-preview")]
    [Authorize]
    public async Task<IActionResult> PreviewContractCancellation(Guid applicationId)
    {
        try
        {
            var result = await _installmentService.PreviewContractCancellationAsync(applicationId);
            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi tính toán bảng kê hủy hợp đồng.",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Thực hiện hủy hợp đồng & phạt cọc: Tịch thu cọc Đợt 1, tính hoàn trả Đợt 2+ (sau khấu trừ phạt trễ hạn 0.05%/ngày), giải phóng căn hộ.
    /// Dành cho cả Người dân (Nộp đơn xin ngừng thanh toán/rút hồ sơ) và Chủ đầu tư (Cưỡng chế thu hồi).
    /// </summary>
    [HttpPost("applications/{applicationId}/cancel-contract")]
    [Authorize]
    public async Task<IActionResult> CancelContract(Guid applicationId, [FromBody] CancelContractRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var result = await _installmentService.CancelContractAndProcessRefundAsync(userId, applicationId, dto);
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi thực hiện hủy hợp đồng.",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// [Dành cho Người dân] Nộp đơn xin ngừng thanh toán / tự nguyện rút hồ sơ.
    /// Trạng thái hồ sơ chuyển sang CANCELLATION_REQUESTED và gửi thông báo chờ CĐT phê duyệt.
    /// </summary>
    [HttpPost("applications/{applicationId}/request-cancellation")]
    [Authorize]
    public async Task<IActionResult> RequestCancellation(Guid applicationId, [FromBody] CancelContractRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var result = await _installmentService.SubmitCancellationRequestAsync(userId, applicationId, dto);
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi nộp đơn xin ngừng thanh toán.",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// [Dành cho Chủ đầu tư] Lấy danh sách các đơn xin ngừng thanh toán đang chờ duyệt theo dự án.
    /// </summary>
    [HttpGet("projects/{projectId}/cancellation-requests")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    public async Task<IActionResult> GetPendingCancellationRequests(Guid projectId)
    {
        try
        {
            var requests = await _installmentService.GetPendingCancellationRequestsAsync(projectId);
            return Ok(new { success = true, data = requests });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi lấy danh sách đơn xin ngừng thanh toán.",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// [Dành cho Chủ đầu tư] Phê duyệt đơn xin ngừng thanh toán của người dân.
    /// Hệ thống tịch thu cọc Đợt 1 (100%), tính hoàn tiền Đợt 2+ (sau khấu trừ nợ phạt), chuyển hồ sơ sang CANCELED, giải phóng căn và đôn Waitlist nếu có.
    /// </summary>
    [HttpPost("applications/{applicationId}/approve-cancellation")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    public async Task<IActionResult> ApproveCancellationRequest(Guid applicationId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var result = await _installmentService.ApproveCancellationRequestAsync(userId, applicationId);
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi phê duyệt đơn xin ngừng thanh toán.",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// [Dành cho Chủ đầu tư] Từ chối đơn xin ngừng thanh toán của người dân.
    /// Hồ sơ được khôi phục về trạng thái hoạt động trước đó.
    /// </summary>
    [HttpPost("applications/{applicationId}/reject-cancellation")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    public async Task<IActionResult> RejectCancellationRequest(Guid applicationId, [FromBody] RejectCancellationRequestDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var result = await _installmentService.RejectCancellationRequestAsync(userId, applicationId, dto);
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi từ chối đơn xin ngừng thanh toán.",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Thống kê báo cáo tiến độ thu tiền & nợ phạt trễ hạn (0.05%/ngày) của dự án.
    /// Dành cho Chủ đầu tư, Sở Xây dựng, Admin.
    /// </summary>
    [HttpGet("projects/{projectId}/payment-progress")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.DepartmentOfConstruction},{RoleConstants.SystemAdministrator}")]
    public async Task<IActionResult> GetProjectPaymentProgress(Guid projectId)
    {
        try
        {
            var progress = await _installmentService.GetProjectPaymentProgressAsync(projectId);
            return Ok(new { success = true, data = progress });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi lấy tiến độ thu tiền dự án.",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Kích hoạt / mở đợt thanh toán tiếp theo theo sự kiện tiến độ cho toàn bộ dự án.
    /// Dành cho Chủ đầu tư và Admin.
    /// </summary>
    [HttpPatch("projects/{projectId}/unlock-phase")]
    [Authorize(Roles = $"{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator}")]
    public async Task<IActionResult> UnlockProjectPhase(Guid projectId, [FromQuery] string triggerEvent)
    {
        if (string.IsNullOrWhiteSpace(triggerEvent))
            return BadRequest(new { success = false, message = "Vui lòng cung cấp triggerEvent." });

        try
        {
            var unlockedCount = await _installmentService.UnlockPhaseByEventAsync(projectId, triggerEvent);
            return Ok(new
            {
                success = true,
                message = $"Đã kích hoạt đợt thu tiền ({triggerEvent}) cho {unlockedCount} hồ sơ hợp lệ.",
                unlockedCount
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi kích hoạt đợt thanh toán.",
                error = ex.Message
            });
        }
    }

}
