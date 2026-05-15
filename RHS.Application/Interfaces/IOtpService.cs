namespace RHS.Application.Interfaces;

public interface IOtpService
{
    string GenerateOtp();
    Task<bool> SendOtpEmailAsync(string email, string otpCode, string fullName);
    Task<bool> SendPasswordResetOtpEmailAsync(string email, string otpCode, string fullName);
}
