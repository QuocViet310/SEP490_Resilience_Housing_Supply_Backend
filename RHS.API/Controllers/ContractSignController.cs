using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.Interfaces;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// API ký hợp đồng nguyên tắc (giả lập ký số).
/// Người dân bấm "Đồng ý điều khoản" trên App → hệ thống ghi nhận trạng thái đã ký.
/// </summary>
[ApiController]
[Route("api/contract-sign")]
public class ContractSignController : ControllerBase
{
    private readonly IContractSignService _contractSignService;

    public ContractSignController(IContractSignService contractSignService)
    {
        _contractSignService = contractSignService;
    }

    /// <summary>
    /// Người dân bấm "Đồng ý điều khoản" → ký hợp đồng nguyên tắc.
    /// Yêu cầu: hồ sơ đã ở trạng thái DEPOSIT_PAID, người gọi là chủ hồ sơ.
    /// Idempotent: gọi lại khi đã ký → trả OK (không tạo duplicate).
    /// </summary>
    /// <remarks>
    /// **Luồng nghiệp vụ:**
    /// 1. Hệ thống validate JWT → lấy applicantId
    /// 2. Kiểm tra hồ sơ thuộc applicant + trạng thái DEPOSIT_PAID
    /// 3. Ghi nhận: PrincipleAgreement.IsSigned=true + ApplicationStatus→CONTRACT_SIGNED
    /// 4. Ghi lịch sử + gửi notification
    /// 
    /// **Response khi thành công:**
    /// ```json
    /// {
    ///   "success": true,
    ///   "message": "Ký hợp đồng nguyên tắc thành công.",
    ///   "data": { "signedAt": "2026-07-21T05:00:00Z" }
    /// }
    /// ```
    /// </remarks>
    [HttpPost("{applicationId}/sign")]
    [Authorize]
    public async Task<IActionResult> SignContract(Guid applicationId)
    {
        // Lấy userId từ JWT
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            // Lấy IP address cho consent log
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var result = await _contractSignService.SignContractAsync(userId, applicationId, ipAddress);

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
                success = true,
                message = result.Message,
                data = new
                {
                    signedAt = result.SignedAt
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi ký hợp đồng",
                error   = ex.Message
            });
        }
    }

    /// <summary>
    /// Xem trạng thái ký hợp đồng nguyên tắc.
    /// Trả về: đã ký chưa, thời gian ký, URL PDF hợp đồng.
    /// </summary>
    [HttpGet("{applicationId}/status")]
    [Authorize]
    public async Task<IActionResult> GetSignStatus(Guid applicationId)
    {
        // Lấy userId từ JWT
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var status = await _contractSignService.GetSignStatusAsync(applicationId);

            if (status == null)
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy hợp đồng nguyên tắc cho hồ sơ này."
                });

            return Ok(new
            {
                success = true,
                data    = status
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Lỗi khi lấy trạng thái ký hợp đồng",
                error   = ex.Message
            });
        }
    }
}
