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

    /// <summary>
    /// Kích hoạt nạp dữ liệu demo & 10 tài khoản test mẫu vào Database
    /// </summary>
    [HttpPost("seed-demo-data")]
    public async Task<IActionResult> SeedDemoData(
        [FromServices] RHS.Infrastructure.Data.AppDbContext db,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("DemoDataSeeder");
        var seedResult = await RHS.Infrastructure.Seed.DemoDataSeeder.EnsureSeededAsync(db, logger);

        var demoUsersInDb = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(
                Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(
                    db.Users.Where(u => u.Email.EndsWith("@rhs.local"))
                ).Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FullName,
                    u.CitizenId,
                    u.PhoneNumber,
                    u.Status,
                    u.IsEkycVerified
                })
            );

        return Ok(new
        {
            success = seedResult.Errors.Count == 0,
            message = seedResult.Errors.Count == 0
                ? $"Đã nạp thành công! Thêm mới {seedResult.UsersAdded} users, cập nhật {seedResult.UsersUpdated} users. Tổng {demoUsersInDb.Count} tài khoản trong DB."
                : $"Hoàn tất có cảnh báo ({seedResult.Errors.Count} lỗi). Thêm {seedResult.UsersAdded}, cập nhật {seedResult.UsersUpdated}. Tổng {demoUsersInDb.Count} tài khoản trong DB.",
            summary = new
            {
                usersAdded = seedResult.UsersAdded,
                usersUpdated = seedResult.UsersUpdated,
                appsAdded = seedResult.AppsAdded,
                agreementsAdded = seedResult.AgreementsAdded,
                totalDemoUsersInDb = demoUsersInDb.Count,
                errors = seedResult.Errors
            },
            password = "123456",
            demoUsersInDatabase = demoUsersInDb
        });
    }

    /// <summary>
    /// Lấy danh sách tài khoản demo có sẵn trong hệ thống
    /// </summary>
    [HttpGet("demo-accounts")]
    public IActionResult GetDemoAccounts()
    {
        return Ok(new
        {
            success = true,
            defaultPassword = "123456",
            testCitizens = new[]
            {
                new { email = "dan.test01@rhs.local", name = "Nguyễn Văn An", citizenId = "079095000001", phone = "0908000001", gender = "Nam" },
                new { email = "dan.test02@rhs.local", name = "Trần Thị Bình", citizenId = "079093000002", phone = "0908000002", gender = "Nữ" },
                new { email = "dan.test03@rhs.local", name = "Lê Hoàng Cường", citizenId = "079090000003", phone = "0908000003", gender = "Nam" },
                new { email = "dan.test04@rhs.local", name = "Phạm Thị Dung", citizenId = "079096000004", phone = "0908000004", gender = "Nữ" },
                new { email = "dan.test05@rhs.local", name = "Hoàng Văn Em", citizenId = "079088000005", phone = "0908000005", gender = "Nam" },
                new { email = "dan.test06@rhs.local", name = "Võ Thị Hạnh", citizenId = "079097000006", phone = "0908000006", gender = "Nữ" },
                new { email = "dan.test07@rhs.local", name = "Đặng Quốc Hùng", citizenId = "079091000007", phone = "0908000007", gender = "Nam" },
                new { email = "dan.test08@rhs.local", name = "Bùi Mai Linh", citizenId = "079094000008", phone = "0908000008", gender = "Nữ" },
                new { email = "dan.test09@rhs.local", name = "Ngô Thanh Nam", citizenId = "079092000009", phone = "0908000009", gender = "Nam" },
                new { email = "dan.test10@rhs.local", name = "Đỗ Phương Oanh", citizenId = "079099000010", phone = "0908000010", gender = "Nữ" },
                new { email = "dan.test11@rhs.local", name = "Trần Quốc Phong", citizenId = "079098000011", phone = "0908000011", gender = "Nam" },
                new { email = "dan.free@rhs.local", name = "Nguyễn Thị Free", citizenId = "001090000016", phone = "0901000016", gender = "Nữ" }
            },
            staffAccounts = new[]
            {
                new { role = "Housing Developer (CĐT)", email = "cdt.demo@rhs.local" },
                new { role = "Department Of Construction (SXD)", email = "sxd.demo@rhs.local" },
                new { role = "System Administrator (Admin)", email = "admin.demo@rhs.local" }
            }
        });
    }
}
