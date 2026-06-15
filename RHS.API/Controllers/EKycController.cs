using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.EKyc;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Exceptions;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// Controller xử lý các nghiệp vụ eKYC (Electronic Know Your Customer):
/// - Trích xuất thông tin Căn cước công dân (OCR)
/// - So khớp khuôn mặt selfie với ảnh trên CCCD (Face Match)
/// - Kiểm tra liveness để chống giả mạo khuôn mặt (Liveness Detection)
/// - Kiểm tra CCCD đã tồn tại trong hệ thống chưa
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EKycController : ControllerBase
{
    private readonly IEKycService     _eKycService;
    private readonly IUserRepository  _userRepository;

    public EKycController(IEKycService eKycService, IUserRepository userRepository)
    {
        _eKycService    = eKycService;
        _userRepository = userRepository;
    }

    // ── Helper: lấy userId từ JWT claim ─────────────────────────────────
    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out var id) ? id : null;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Kiểm tra CCCD trùng
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra xem số CCCD đã được xác thực bởi tài khoản khác trong hệ thống chưa.
    /// Gọi sau bước OCR để xác nhận CCCD hợp lệ trước khi tiếp tục xác minh danh tính.
    /// </summary>
    /// <remarks>
    /// **HTTP Responses:**
    /// - `200` — CCCD chưa có ai dùng, có thể tiếp tục xác thực
    /// - `409` — CCCD đã thuộc về tài khoản khác, không thể xác thực
    /// - `400` — Số CCCD rỗng hoặc không hợp lệ
    /// - `401` — Chưa đăng nhập
    /// - `500` — Lỗi không xác định
    /// </remarks>
    [HttpGet("check-citizen-id")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CheckCitizenId(
        [FromQuery] string citizenId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(citizenId))
            return BadRequest(new { success = false, message = "Số CCCD không được để trống." });

        // Lấy userId từ JWT để loại trừ chính user đang kiểm tra
        var currentUserId = GetCurrentUserId();

        try
        {
            var exists = await _userRepository.CitizenIdExistsAsync(
                citizenId.Trim(),
                excludeUserId: currentUserId);

            if (exists)
                return Conflict(new
                {
                    success = false,
                    message = "Số CCCD này đã được xác thực bởi tài khoản khác trong hệ thống. Không thể tiếp tục xác minh danh tính.",
                    citizenId
                });

            return Ok(new
            {
                success   = true,
                message   = "Số CCCD hợp lệ, có thể tiếp tục xác minh danh tính.",
                citizenId,
                available = true
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Đã xảy ra lỗi khi kiểm tra CCCD.",
                detail  = ex.Message
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  OCR
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Trích xuất thông tin từ ảnh Căn cước công dân bằng FPT AI OCR.
    /// Chấp nhận ảnh mặt trước hoặc mặt sau của CCCD gắn chip.
    /// </summary>
    /// <remarks>
    /// **Content-Type:** multipart/form-data
    ///
    /// **Form field:** `image` — file ảnh CCCD (JPEG hoặc PNG, tối đa 5 MB)
    ///
    /// **HTTP Responses:**
    /// - `200` — Trích xuất thành công, trả về thông tin CCCD
    /// - `400` — File ảnh không hợp lệ (rỗng, sai định dạng, quá dung lượng)
    /// - `401` — Chưa đăng nhập
    /// - `502` — FPT AI API trả về lỗi hoặc không kết nối được
    /// - `500` — Lỗi không xác định từ server
    /// </remarks>
    [HttpPost("ocr")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExtractIdCard(
        IFormFile image,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new OcrIdCardRequest { Image = image };
            var result  = await _eKycService.ExtractIdCardAsync(request, cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Trích xuất thông tin CCCD thành công.",
                data    = new
                {
                    id          = result.Data?.Id,
                    name        = result.Data?.Name,
                    dob         = result.Data?.Dob,
                    sex         = result.Data?.Sex,
                    nationality = result.Data?.Nationality,
                    home        = result.Data?.Home,
                    address     = result.Data?.Address,
                    doe         = result.Data?.Doe,
                    issueDate   = result.Data?.IssueDate,
                    issueLoc    = result.Data?.IssueLoc,
                    type        = result.Data?.Type,
                    overallScore = result.Data?.OverallScore
                }
            });
        }
        catch (EKycValidationException ex)
        {
            // Lỗi do client gửi file không hợp lệ → 400 Bad Request
            return BadRequest(new
            {
                success   = false,
                message   = ex.Message,
                errorCode = ex.ErrorCode,
                field     = ex.FieldName
            });
        }
        catch (EKycIntegrationException ex)
        {
            // Lỗi do FPT AI API (timeout, HTTP error, parse lỗi) → 502 Bad Gateway
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success   = false,
                message   = "Không thể kết nối hoặc xử lý phản hồi từ FPT AI OCR API.",
                errorCode = ex.ErrorCode,
                detail    = ex.Message
            });
        }
        catch (Exception ex)
        {
            // Lỗi không mong đợi → 500 Internal Server Error
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Đã xảy ra lỗi không mong đợi trong quá trình xử lý.",
                detail  = ex.Message
            });
        }
    }

    /// <summary>
    /// So khớp khuôn mặt selfie với ảnh trên Căn cước công dân bằng FPT AI Face Match.
    /// Trả về kết quả khớp và điểm độ tương đồng.
    /// </summary>
    /// <remarks>
    /// **Content-Type:** multipart/form-data
    ///
    /// **Form fields:**
    /// - `faceImage`   — ảnh selfie chụp trực tiếp (JPEG hoặc PNG, tối đa 5 MB)
    /// - `idCardImage` — ảnh chân dung in trên CCCD (JPEG hoặc PNG, tối đa 5 MB)
    ///
    /// **HTTP Responses:**
    /// - `200` — So khớp hoàn tất (kiểm tra `isMatch` và `similarity` trong response body)
    /// - `400` — File ảnh không hợp lệ
    /// - `401` — Chưa đăng nhập
    /// - `502` — FPT AI API trả về lỗi hoặc không kết nối được
    /// - `500` — Lỗi không xác định từ server
    /// </remarks>
    [HttpPost("face-match")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MatchFace(
        IFormFile faceImage,
        IFormFile idCardImage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new FaceMatchRequest
            {
                FaceImage   = faceImage,
                IdCardImage = idCardImage
            };

            var result = await _eKycService.MatchFaceAsync(request, cancellationToken);

            return Ok(new
            {
                success = true,
                message = result.IsMatch
                    ? "Khuôn mặt khớp với ảnh CCCD."
                    : "Khuôn mặt KHÔNG khớp với ảnh CCCD.",
                data = new
                {
                    isMatch         = result.IsMatch,
                    similarity      = result.Similarity,
                    isBothImgIdCard = result.IsBothImgIdCard,   // FPT AI: isBothImgIDCard
                    fptMessage      = result.FptMessage          // FPT AI: "request successful."
                }
            });
        }
        catch (EKycValidationException ex)
        {
            // Lỗi do client gửi file không hợp lệ → 400 Bad Request
            return BadRequest(new
            {
                success   = false,
                message   = ex.Message,
                errorCode = ex.ErrorCode,
                field     = ex.FieldName
            });
        }
        catch (EKycIntegrationException ex)
        {
            // Lỗi do FPT AI API → 502 Bad Gateway
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success   = false,
                message   = "Không thể kết nối hoặc xử lý phản hồi từ FPT AI Face Match API.",
                errorCode = ex.ErrorCode,
                detail    = ex.Message
            });
        }
        catch (Exception ex)
        {
            // Lỗi không mong đợi → 500 Internal Server Error
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Đã xảy ra lỗi không mong đợi trong quá trình xử lý.",
                detail  = ex.Message
            });
        }
    }

    /// <summary>
    /// Xác minh thực thể sống qua video selfie + ảnh khuôn mặt (Liveness Detection v3).
    /// Phát hiện các hành vi giả mạo: dùng ảnh in, màn hình, mặt nạ, video deepfake.
    /// </summary>
    /// <remarks>
    /// **Content-Type:** multipart/form-data
    ///
    /// **Form fields:**
    /// - `videoFile` — video selfie quay trực tiếp từ camera (MP4/AVI/MOV, 3–5 giây)
    /// - `cmndImage` — ảnh khuôn mặt selfie của người dùng (JPEG/PNG)
    ///
    /// FPT AI Liveness v3 yêu cầu cả VIDEO + ảnh khuôn mặt (cmnd).
    ///
    /// **HTTP Responses:**
    /// - `200` — Kiểm tra hoàn tất. Xem `isLive`: `true` = hợp lệ, `false` = giả mạo
    /// - `400` — File không hợp lệ (rỗng, sai định dạng, quá dung lượng)
    /// - `401` — Chưa đăng nhập
    /// - `502` — FPT AI API trả về lỗi hoặc không kết nối được
    /// - `500` — Lỗi không xác định từ server
    /// </remarks>
    [HttpPost("liveness")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DetectLiveness(
        IFormFile videoFile,
        IFormFile cmndImage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new LivenessDetectionRequest
            {
                VideoFile = videoFile,
                CmndImage = cmndImage
            };
            var result  = await _eKycService.DetectLivenessAsync(request, cancellationToken);

            return Ok(new
            {
                success = true,
                message = result.IsLive
                    ? "Xác minh thực thể sống thành công. Video hợp lệ."
                    : "Phát hiện giả mạo. Video không hợp lệ.",
                data = new
                {
                    isLive           = result.IsLive,
                    spoofProbability = result.SpoofProbability,
                    needToReview     = result.NeedToReview,
                    isDeepfake       = result.IsDeepfake,
                    warning          = result.Warning,
                    livenessCode     = result.LivenessCode,
                    livenessMessage  = result.LivenessMessage,
                    fptMessage       = result.FptMessage
                }
            });
        }
        catch (EKycValidationException ex)
        {
            // Lỗi do client gửi file không hợp lệ → 400 Bad Request
            return BadRequest(new
            {
                success   = false,
                message   = ex.Message,
                errorCode = ex.ErrorCode,
                field     = ex.FieldName
            });
        }
        catch (EKycIntegrationException ex)
        {
            // Lỗi do FPT AI API → 502 Bad Gateway
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success   = false,
                message   = "Không thể kết nối hoặc xử lý phản hồi từ FPT AI Liveness API.",
                errorCode = ex.ErrorCode,
                detail    = ex.Message
            });
        }
        catch (Exception ex)
        {
            // Lỗi không mong đợi → 500 Internal Server Error
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Đã xảy ra lỗi không mong đợi trong quá trình xử lý.",
                detail  = ex.Message
            });
        }
    }
}
