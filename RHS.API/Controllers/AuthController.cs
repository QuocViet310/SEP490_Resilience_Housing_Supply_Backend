using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Auth;
using RHS.Application.Interfaces;
using System.Security.Claims;

namespace RHS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Đăng ký tài khoản mới bằng email và password
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.RegisterAsync(registerDto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Đăng nhập bằng email và password
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.LoginAsync(loginDto);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Đăng nhập/Đăng ký bằng Google OAuth
    /// </summary>
    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto googleLoginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.GoogleLoginAsync(googleLoginDto);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Xác thực OTP sau khi đăng ký
    /// </summary>
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto verifyOtpDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.VerifyOtpAsync(verifyOtpDto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Gửi lại mã OTP
    /// </summary>
    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] string email)
    {
        var result = await _authService.ResendOtpAsync(email);

        if (!result)
        {
            return BadRequest(new { success = false, message = "Không thể gửi lại mã OTP" });
        }

        return Ok(new { success = true, message = "Mã OTP đã được gửi lại" });
    }

    /// <summary>
    /// Làm mới access token bằng refresh token
    /// </summary>
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.RefreshTokenAsync(refreshTokenDto.RefreshToken);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Đăng xuất (thu hồi refresh token)
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDto refreshTokenDto)
    {
        var result = await _authService.RevokeTokenAsync(refreshTokenDto.RefreshToken);

        if (!result)
        {
            return BadRequest(new { success = false, message = "Không thể đăng xuất" });
        }

        return Ok(new { success = true, message = "Đăng xuất thành công" });
    }

    /// <summary>
    /// Yêu cầu đặt lại mật khẩu (gửi OTP qua email)
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.ForgotPasswordAsync(forgotPasswordDto);

        if (!result)
        {
            return BadRequest(new { success = false, message = "Không thể gửi mã OTP. Tài khoản có thể đăng nhập bằng Google." });
        }

        return Ok(new { success = true, message = "Mã OTP đã được gửi đến email của bạn" });
    }

    /// <summary>
    /// Đặt lại mật khẩu bằng OTP
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.ResetPasswordAsync(resetPasswordDto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Đổi mật khẩu khi đã đăng nhập (yêu cầu xác thực)
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Get user ID from JWT token claims (using "sub" claim from JwtRegisteredClaimNames.Sub)
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { success = false, message = "Token không hợp lệ" });
        }

        var result = await _authService.ChangePasswordAsync(userId, changePasswordDto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
