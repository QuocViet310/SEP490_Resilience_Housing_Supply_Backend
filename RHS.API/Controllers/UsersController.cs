using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.CitizenProfile;
using RHS.Application.DTOs.User;
using RHS.Application.Interfaces;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ICitizenProfileService _citizenProfileService;

    public UsersController(
        IUserService userService,
        ICitizenProfileService citizenProfileService)
    {
        _userService           = userService;
        _citizenProfileService = citizenProfileService;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return Guid.TryParse(claim?.Value, out var id) ? id : null;
    }

    // ─────────────────────────────────────────────────────────────
    // 1. Basic Profile (Existing)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy thông tin profile cơ bản của user hiện tại
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var profile = await _userService.GetProfileAsync(userId.Value);
        if (profile == null)
            return NotFound(new { success = false, message = "Người dùng không tồn tại" });

        return Ok(new
        {
            success = true,
            user = profile
        });
    }

    /// <summary>
    /// Cập nhật thông tin profile cơ bản của user hiện tại
    /// </summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto updateProfileDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var updatedProfile = await _userService.UpdateProfileAsync(userId.Value, updateProfileDto);
        if (updatedProfile == null)
            return NotFound(new { success = false, message = "Người dùng không tồn tại" });

        return Ok(new
        {
            success = true,
            message = "Cập nhật thông tin thành công",
            user = updatedProfile
        });
    }

    // ─────────────────────────────────────────────────────────────
    // 2. Full Citizen Profile & Prefill (New)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// [Citizen] Lấy toàn bộ Hồ sơ cá nhân của công dân:
    /// eKYC, Tình trạng hôn nhân, Vợ/Chồng, Thu nhập, Việc làm, Nơi ở, Sổ hộ khẩu và Kho tài liệu cá nhân.
    /// </summary>
    [HttpGet("profile/full")]
    [ProducesResponseType(typeof(CitizenFullProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullProfile(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var profile = await _citizenProfileService.GetFullProfileAsync(userId.Value, ct);
        if (profile == null)
            return NotFound(new { success = false, message = "Không tìm thấy hồ sơ người dùng." });

        return Ok(new
        {
            success = true,
            data = profile
        });
    }

    /// <summary>
    /// [Citizen] Cập nhật Hồ sơ cá nhân:
    /// Tình trạng hôn nhân, Vợ/Chồng, Thu nhập, Nơi ở, Nghề nghiệp, Thực trạng nhà ở và Đối tượng ưu tiên.
    /// (Nếu đã eKYC, trường Họ tên, CCCD, Ngày sinh sẽ được bảo vệ không bị thay đổi).
    /// </summary>
    [HttpPut("profile/citizen")]
    [ProducesResponseType(typeof(CitizenFullProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateCitizenProfile(
        [FromBody] UpdateCitizenProfileDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var updated = await _citizenProfileService.UpdateCitizenProfileAsync(userId.Value, dto, ct);
            return Ok(new
            {
                success = true,
                message = "Cập nhật hồ sơ cá nhân thành công.",
                data = updated
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi cập nhật hồ sơ.", detail = ex.Message });
        }
    }

    /// <summary>
    /// [Citizen] Lấy toàn bộ dữ liệu kế thừa từ Hồ sơ cá nhân (Pre-fill) để tự động điền vào Form nộp hồ sơ NOXH.
    /// Giúp người dân không cần nhập lại giấy tờ và thông tin từ đầu.
    /// </summary>
    [HttpGet("profile/prefill")]
    [ProducesResponseType(typeof(ApplicationPrefillResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetApplicationPrefill(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var prefill = await _citizenProfileService.GetApplicationPrefillAsync(userId.Value, ct);
        return Ok(new
        {
            success = true,
            data = prefill
        });
    }

    // ─────────────────────────────────────────────────────────────
    // 3. User Household Profile (Nhân khẩu tái sử dụng)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// [Citizen] Lấy danh sách thành viên hộ gia đình / người phụ thuộc trong Profile của công dân.
    /// </summary>
    [HttpGet("household-members")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHouseholdMembers(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var members = await _citizenProfileService.GetHouseholdMembersAsync(userId.Value, ct);
        return Ok(new
        {
            success = true,
            data = members
        });
    }

    /// <summary>
    /// [Citizen] Thêm thành viên / người phụ thuộc vào sổ hộ khẩu cá nhân tái sử dụng.
    /// </summary>
    [HttpPost("household-members")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AddHouseholdMember(
        [FromBody] UserHouseholdMemberRequestDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var created = await _citizenProfileService.AddHouseholdMemberAsync(userId.Value, dto, ct);
            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message = "Thêm thành viên vào hộ gia đình thành công.",
                data = created
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi thêm thành viên.", detail = ex.Message });
        }
    }

    /// <summary>
    /// [Citizen] Cập nhật thông tin thành viên trong hộ gia đình của Profile.
    /// </summary>
    [HttpPut("household-members/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateHouseholdMember(
        Guid memberId,
        [FromBody] UserHouseholdMemberRequestDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var updated = await _citizenProfileService.UpdateHouseholdMemberAsync(userId.Value, memberId, dto, ct);
            return Ok(new
            {
                success = true,
                message = "Cập nhật thông tin thành viên thành công.",
                data = updated
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// [Citizen] Xóa thành viên khỏi danh sách hộ gia đình của Profile.
    /// </summary>
    [HttpDelete("household-members/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteHouseholdMember(Guid memberId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var success = await _citizenProfileService.DeleteHouseholdMemberAsync(userId.Value, memberId, ct);
        if (!success)
            return NotFound(new { success = false, message = "Không tìm thấy thành viên trong sổ hộ khẩu." });

        return Ok(new
        {
            success = true,
            message = "Xóa thành viên khỏi sổ hộ khẩu thành công."
        });
    }

    // ─────────────────────────────────────────────────────────────
    // 4. User Document Vault (Kho tài liệu cá nhân tái sử dụng)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// [Citizen] Lấy danh sách tất cả tài liệu trong Kho hồ sơ cá nhân tái sử dụng (Personal Document Vault).
    /// </summary>
    [HttpGet("documents")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDocuments(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var docs = await _citizenProfileService.GetDocumentsAsync(userId.Value, ct);
        return Ok(new
        {
            success = true,
            data = docs
        });
    }

    /// <summary>
    /// [Citizen] Upload tài liệu mới (hoặc cập nhật loại giấy tờ đã có) vào Kho hồ sơ cá nhân tái sử dụng.
    /// Chấp nhận file PDF và hình ảnh (JPG, PNG, WEBP), dung lượng tối đa 10MB.
    /// </summary>
    [HttpPost("documents")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadDocument(
        [FromForm] UploadUserDocumentRequestDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var uploaded = await _citizenProfileService.UploadDocumentAsync(userId.Value, dto, ct);
            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message = "Upload tài liệu vào kho hồ sơ cá nhân thành công.",
                data = uploaded
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = "Đã xảy ra lỗi khi upload tài liệu.", detail = ex.Message });
        }
    }

    /// <summary>
    /// [Citizen] Xóa tài liệu khỏi Kho hồ sơ cá nhân tái sử dụng.
    /// </summary>
    [HttpDelete("documents/{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(Guid documentId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var success = await _citizenProfileService.DeleteDocumentAsync(userId.Value, documentId, ct);
        if (!success)
            return NotFound(new { success = false, message = "Không tìm thấy tài liệu trong kho lưu trữ." });

        return Ok(new
        {
            success = true,
            message = "Xóa tài liệu khỏi kho hồ sơ cá nhân thành công."
        });
    }

    // ─────────────────────────────────────────────────────────────
    // 5. Account & Profile Image Operations
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Upload ảnh đại diện cho user hiện tại
    /// </summary>
    [HttpPost("profile/image")]
    public async Task<IActionResult> UploadProfileImage([FromForm] UploadProfileImageDto uploadDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        try
        {
            var updatedProfile = await _userService.UploadProfileImageAsync(userId.Value, uploadDto.Image);
            if (updatedProfile == null)
                return NotFound(new { success = false, message = "Người dùng không tồn tại" });

            return Ok(new
            {
                success = true,
                message = "Upload ảnh đại diện thành công",
                user = updatedProfile
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa ảnh đại diện của user hiện tại
    /// </summary>
    [HttpDelete("profile/image")]
    public async Task<IActionResult> DeleteProfileImage()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _userService.DeleteProfileImageAsync(userId.Value);
        if (!result)
            return NotFound(new { success = false, message = "Không tìm thấy ảnh đại diện để xóa" });

        return Ok(new
        {
            success = true,
            message = "Xóa ảnh đại diện thành công"
        });
    }

    /// <summary>
    /// Xóa tài khoản của user hiện tại (soft delete)
    /// </summary>
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountDto deleteAccountDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });

        var result = await _userService.DeleteAccountAsync(userId.Value, deleteAccountDto.Password, deleteAccountDto.Reason);
        if (!result)
            return BadRequest(new { success = false, message = "Mật khẩu không chính xác hoặc tài khoản không tồn tại" });

        return Ok(new
        {
            success = true,
            message = "Xóa tài khoản thành công. Chúng tôi rất tiếc khi bạn rời đi."
        });
    }

    /// <summary>
    /// Test endpoint chỉ dành cho Admin
    /// </summary>
    [HttpGet("admin-only")]
    [Authorize(Roles = "Admin")]
    public IActionResult AdminOnly()
    {
        return Ok(new
        {
            success = true,
            message = "Chào mừng Admin!"
        });
    }

    /// <summary>
    /// Test endpoint chỉ dành cho Officer
    /// </summary>
    [HttpGet("officer-only")]
    [Authorize(Roles = "Officer")]
    public IActionResult OfficerOnly()
    {
        return Ok(new
        {
            success = true,
            message = "Chào mừng Officer!"
        });
    }
}

