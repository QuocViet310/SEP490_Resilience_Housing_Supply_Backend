using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RHS.Application.DTOs.EKyc;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Exceptions;
using System.Globalization;
using System.Security.Claims;

namespace RHS.API.Controllers;

/// <summary>
/// Controller xử lý các nghiệp vụ eKYC (Electronic Know Your Customer):
/// - Xác minh danh tính toàn diện (OCR + Face Match + Auto-save Profile)
/// - Trích xuất thông tin Căn cước công dân (OCR)
/// - So khớp khuôn mặt selfie với ảnh trên CCCD (Face Compare)
/// - Kiểm tra CCCD đã tồn tại trong hệ thống chưa
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EKycController : ControllerBase
{
    private readonly IEKycService     _eKycService;
    private readonly IUserRepository  _userRepository;
    private readonly double           _faceMatchThreshold;

    public EKycController(
        IEKycService              eKycService,
        IUserRepository           userRepository,
        IOptions<VnptEKycOptions>  vnptOptions)
    {
        _eKycService        = eKycService;
        _userRepository     = userRepository;
        _faceMatchThreshold = vnptOptions.Value.FaceMatchThreshold;
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
                    message = "Số CCCD này đã được xác thực bởi tài khoản đang hoạt động khác. Không thể tiếp tục xác minh danh tính.",
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
    /// Trích xuất thông tin từ ảnh Căn cước công dân bằng VNPT eKYC OCR.
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
    /// - `502` — eKYC API trả về lỗi hoặc không kết nối được
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
            // Lỗi do eKYC API (timeout, HTTP error, parse lỗi) → 502 Bad Gateway
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success   = false,
                message   = "Không thể kết nối hoặc xử lý phản hồi từ eKYC OCR API.",
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
    /// So khớp khuôn mặt selfie với ảnh trên Căn cước công dân bằng VNPT eKYC Face Compare.
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
    /// - `502` — eKYC API trả về lỗi hoặc không kết nối được
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
                    isBothImgIdCard = result.IsBothImgIdCard,
                    providerMessage = result.ProviderMessage
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
            // Lỗi do eKYC API → 502 Bad Gateway
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success   = false,
                message   = "Không thể kết nối hoặc xử lý phản hồi từ eKYC Face Compare API.",
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
    /// Xác minh thực thể sống qua video selfie + ảnh khuôn mặt (Liveness Detection).
    /// ⚠️ VNPT eKYC không hỗ trợ Liveness qua REST API — endpoint này sẽ trả về lỗi.
    /// </summary>
    /// <remarks>
    /// **Content-Type:** multipart/form-data
    ///
    /// **Form fields:**
    /// - `videoFile` — video selfie quay trực tiếp từ camera (MP4/AVI/MOV, 3–5 giây)
    /// - `cmndImage` — ảnh khuôn mặt selfie của người dùng (JPEG/PNG)
    ///
    /// ⚠️ Liveness Detection yêu cầu tích hợp VNPT eKYC SDK phía client.
    ///
    /// **HTTP Responses:**
    /// - `200` — Kiểm tra hoàn tất. Xem `isLive`: `true` = hợp lệ, `false` = giả mạo
    /// - `400` — File không hợp lệ (rỗng, sai định dạng, quá dung lượng)
    /// - `401` — Chưa đăng nhập
    /// - `502` — eKYC API trả về lỗi hoặc không kết nối được
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
                    providerMessage  = result.ProviderMessage
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
            // Lỗi do eKYC API → 502 Bad Gateway
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success   = false,
                message   = "Không thể kết nối hoặc xử lý phản hồi từ eKYC Liveness API.",
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

    // ─────────────────────────────────────────────────────────────────────────────
    //  Xác minh danh tính toàn diện (One-shot: OCR + Face Match + Auto-save)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Xác minh danh tính toàn diện: OCR + Face Match + tự động lưu thông tin vào Profile.
    /// </summary>
    /// <remarks>
    /// **Content-Type:** multipart/form-data
    ///
    /// **Form fields:**
    /// - `idCardFrontImage` — ảnh mặt trước CCCD (JPEG/PNG, ≤ 5 MB)
    /// - `selfieImage`      — ảnh selfie chụp trực tiếp (JPEG/PNG, ≤ 5 MB)
    ///
    /// **Luồng xử lý:**
    /// 1. OCR — Trích xuất thông tin từ ảnh CCCD (họ tên, số CCCD, ngày sinh, địa chỉ)
    /// 2. Kiểm tra CCCD trùng — Đảm bảo số CCCD chưa được xác thực bởi tài khoản khác
    /// 3. Face Match — So khớp ảnh selfie với ảnh trên CCCD
    /// 4. Auto-save — Nếu similarity ≥ ngưỡng (mặc định 85%), tự động lưu thông tin vào Profile
    ///
    /// **HTTP Responses:**
    /// - `200` — Xác minh hoàn tất (kiểm tra `success` và `data.profileLocked`)
    /// - `400` — Ảnh không hợp lệ hoặc OCR không đọc được thông tin
    /// - `401` — Chưa đăng nhập
    /// - `404` — Không tìm thấy tài khoản
    /// - `409` — Số CCCD đã được xác thực bởi tài khoản khác
    /// - `502` — eKYC API trả về lỗi hoặc không kết nối được
    /// - `500` — Lỗi không xác định
    /// </remarks>
    [HttpPost("verify-identity")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VerifyIdentity(
        IFormFile idCardFrontImage,
        IFormFile selfieImage,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(new { success = false, message = "Không xác định được tài khoản." });

        try
        {
            // ── Bước 1: OCR — Trích xuất thông tin từ ảnh CCCD ──────────────
            var ocrResult = await _eKycService.ExtractIdCardAsync(
                new OcrIdCardRequest { Image = idCardFrontImage },
                cancellationToken);

            if (ocrResult.Data is null || string.IsNullOrWhiteSpace(ocrResult.Data.Id))
                return BadRequest(new
                {
                    success = false,
                    message = "Không thể đọc thông tin từ ảnh CCCD. Vui lòng chụp lại ảnh rõ nét hơn."
                });

            var extractedCitizenId = ocrResult.Data.Id.Trim();

            // ── Bước 2: Kiểm tra CCCD trùng ─────────────────────────────────
            var citizenIdTaken = await _userRepository.CitizenIdExistsAsync(
                extractedCitizenId, excludeUserId: userId);

            if (citizenIdTaken)
                return Conflict(new
                {
                    success   = false,
                    message   = "Số CCCD này đã được xác thực bởi tài khoản khác. Không thể tiếp tục.",
                    citizenId = extractedCitizenId
                });

            // ── Bước 3: Face Match — So khớp selfie với ảnh CCCD ─────────────
            var faceResult = await _eKycService.MatchFaceAsync(
                new FaceMatchRequest
                {
                    FaceImage   = selfieImage,
                    IdCardImage = idCardFrontImage
                },
                cancellationToken);

            // Nếu similarity < ngưỡng → trả về kết quả nhưng KHÔNG lưu Profile
            if (faceResult.Similarity < _faceMatchThreshold)
                return Ok(new
                {
                    success = false,
                    message = $"Khuôn mặt không khớp với ảnh CCCD " +
                              $"(similarity: {faceResult.Similarity:F1}%, yêu cầu ≥ {_faceMatchThreshold}%). " +
                              "Vui lòng thử lại với ảnh selfie rõ nét hơn.",
                    data = new
                    {
                        similarity    = faceResult.Similarity,
                        isMatch       = false,
                        threshold     = _faceMatchThreshold,
                        profileLocked = false
                    }
                });

            // ── Bước 4: Auto-save Profile ────────────────────────────────────
            // Similarity ≥ ngưỡng → tự động lưu thông tin CCCD vào User Profile
            var user = await _userRepository.GetByIdAsync(userId.Value);
            if (user is null)
                return NotFound(new { success = false, message = "Không tìm thấy tài khoản." });

            user.FullName    = ocrResult.Data.Name;
            user.CitizenId   = extractedCitizenId;
            user.DateOfBirth = ParseVietnameseDate(ocrResult.Data.Dob);
            user.Address     = ocrResult.Data.Address;
            user.UpdatedAt   = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);

            return Ok(new
            {
                success = true,
                message = "Xác minh danh tính thành công. Thông tin CCCD đã được lưu vào hồ sơ.",
                data = new
                {
                    similarity    = faceResult.Similarity,
                    isMatch       = true,
                    threshold     = _faceMatchThreshold,
                    profileLocked = true,
                    citizenId     = user.CitizenId,
                    fullName      = user.FullName,
                    dateOfBirth   = user.DateOfBirth?.ToString("dd/MM/yyyy"),
                    address       = user.Address
                }
            });
        }
        catch (EKycValidationException ex)
        {
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
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success   = false,
                message   = "Không thể kết nối hoặc xử lý phản hồi từ eKYC API.",
                errorCode = ex.ErrorCode,
                detail    = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Đã xảy ra lỗi không mong đợi trong quá trình xác minh danh tính.",
                detail  = ex.Message
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parse ngày sinh từ chuỗi định dạng Việt Nam (dd/MM/yyyy) trả về từ OCR.
    /// Trả về null nếu chuỗi không hợp lệ.
    /// </summary>
    private static DateTime? ParseVietnameseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        // Thử nhiều format vì OCR có thể trả về dd/MM/yyyy hoặc dd-MM-yyyy
        string[] formats = { "dd/MM/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" };

        return DateTime.TryParseExact(
            dateStr.Trim(), formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result)
            ? result
            : null;
    }
}
