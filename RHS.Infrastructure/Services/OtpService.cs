using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RHS.Application.Interfaces;
using System.Net;
using System.Net.Mail;

namespace RHS.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OtpService> _logger;

    public OtpService(IConfiguration configuration, ILogger<OtpService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GenerateOtp()
    {
        var random = new Random();
        return random.Next(100000, 999999).ToString();
    }

    public async Task<bool> SendOtpEmailAsync(string email, string otpCode, string fullName)
    {
        // 🔑 Luôn log rõ ràng mã OTP ra Console & Log để tiện cho FE test trên Render / Local
        Console.WriteLine($"=================================================");
        Console.WriteLine($"🔑 [OTP GENERATED] Email: {email} | OTP Code: {otpCode}");
        Console.WriteLine($"=================================================");
        _logger.LogInformation("🔑 [OTP GENERATED] Target Email: {Email} | OTP Code: {OtpCode}", email, otpCode);

        var enableMock = _configuration["EmailSettings:EnableMock"];
        var smtpServer = _configuration["EmailSettings:SmtpServer"];
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var senderPassword = _configuration["EmailSettings:SenderPassword"];

        // Nếu bật EnableMock = true hoặc dùng email/password mặc định chưa cấu hình
        if (string.Equals(enableMock, "true", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(senderEmail) ||
            senderEmail.Contains("example.com") ||
            string.IsNullOrEmpty(senderPassword) ||
            senderPassword == "YOUR_EMAIL_APP_PASSWORD")
        {
            _logger.LogInformation("💡 [MOCK OTP MODE] Skipping SMTP call. OTP code for {Email} is: {OtpCode}", email, otpCode);
            return true;
        }

        try
        {
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RHS Platform";

            _logger.LogInformation("📧 Attempting to send OTP email to {Email} via {Server}:{Port}", email, smtpServer, smtpPort);

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                Timeout = 10000 // 10 giây timeout tránh treo request nếu Render chặn cổng 587
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = "Mã xác thực OTP - Resilience Housing Supply",
                Body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>Xin chào {fullName},</h2>
                        <p>Mã OTP của bạn là:</p>
                        <h1 style='color: #4CAF50; font-size: 32px; letter-spacing: 5px;'>{otpCode}</h1>
                        <p>Mã này sẽ hết hạn sau 5 phút.</p>
                        <p>Nếu bạn không yêu cầu mã này, vui lòng bỏ qua email này.</p>
                        <br>
                        <p>Trân trọng,</p>
                        <p><strong>Resilience Housing Supply Team</strong></p>
                    </body>
                    </html>",
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            await client.SendMailAsync(mailMessage);
            
            _logger.LogInformation("✅ OTP email sent successfully to {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send OTP email to {Email}. Render/Network error: {ErrorMessage}. Fallback OTP code: {OtpCode}", email, ex.Message, otpCode);
            // Trả về true để luồng Đăng ký không bị văng lỗi 500, FE lấy mã OTP từ Render Logs để verify
            return true;
        }
    }

    public async Task<bool> SendPasswordResetOtpEmailAsync(string email, string otpCode, string fullName)
    {
        // 🔑 Luôn log rõ ràng mã OTP ra Console & Log
        Console.WriteLine($"=================================================");
        Console.WriteLine($"🔑 [RESET OTP GENERATED] Email: {email} | OTP Code: {otpCode}");
        Console.WriteLine($"=================================================");
        _logger.LogInformation("🔑 [RESET OTP GENERATED] Target Email: {Email} | OTP Code: {OtpCode}", email, otpCode);

        var enableMock = _configuration["EmailSettings:EnableMock"];
        var smtpServer = _configuration["EmailSettings:SmtpServer"];
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var senderPassword = _configuration["EmailSettings:SenderPassword"];

        if (string.Equals(enableMock, "true", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(senderEmail) ||
            senderEmail.Contains("example.com") ||
            string.IsNullOrEmpty(senderPassword) ||
            senderPassword == "YOUR_EMAIL_APP_PASSWORD")
        {
            _logger.LogInformation("💡 [MOCK OTP MODE] Skipping SMTP call for Password Reset. OTP code for {Email} is: {OtpCode}", email, otpCode);
            return true;
        }

        try
        {
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            var senderName = _configuration["EmailSettings:SenderName"] ?? "RHS Platform";

            _logger.LogInformation("📧 Attempting to send password reset OTP email to {Email}", email);

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                Timeout = 10000
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = "Đặt lại mật khẩu - Resilience Housing Supply",
                Body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>Xin chào {fullName},</h2>
                        <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản của mình.</p>
                        <p>Mã OTP để đặt lại mật khẩu của bạn là:</p>
                        <h1 style='color: #FF5722; font-size: 32px; letter-spacing: 5px;'>{otpCode}</h1>
                        <p>Mã này sẽ hết hạn sau 5 phút.</p>
                        <p><strong>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này và bảo mật tài khoản của bạn.</strong></p>
                        <br>
                        <p>Trân trọng,</p>
                        <p><strong>Resilience Housing Supply Team</strong></p>
                    </body>
                    </html>",
                IsBodyHtml = true
            };

            mailMessage.To.Add(email);

            await client.SendMailAsync(mailMessage);
            
            _logger.LogInformation("✅ Password reset OTP email sent successfully to {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send password reset OTP email to {Email}. Render/Network error: {ErrorMessage}. Fallback OTP code: {OtpCode}", email, ex.Message, otpCode);
            return true;
        }
    }
}
