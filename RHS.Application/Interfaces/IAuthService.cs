using RHS.Application.DTOs.Auth;

namespace RHS.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto registerDto);
    Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
    Task<AuthResponseDto> GoogleLoginAsync(GoogleLoginDto googleLoginDto);
    Task<AuthResponseDto> VerifyOtpAsync(VerifyOtpDto verifyOtpDto);
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
    Task<bool> RevokeTokenAsync(string refreshToken);
    Task<bool> ResendOtpAsync(string email);
    Task<bool> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto);
    Task<AuthResponseDto> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
    Task<AuthResponseDto> ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto);
}
