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
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]!);
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];
            var senderName = _configuration["EmailSettings:SenderName"];

            _logger.LogInformation("📧 Attempting to send OTP email to {Email}", email);
            _logger.LogInformation("📧 SMTP Server: {Server}:{Port}", smtpServer, smtpPort);
            _logger.LogInformation("📧 Sender Email: {SenderEmail}", senderEmail);

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword)
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail!, senderName),
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
            _logger.LogError(ex, "❌ Failed to send OTP email to {Email}. Error: {ErrorMessage}", email, ex.Message);
            return false;
        }
    }

    public async Task<bool> SendPasswordResetOtpEmailAsync(string email, string otpCode, string fullName)
    {
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]!);
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];
            var senderName = _configuration["EmailSettings:SenderName"];

            _logger.LogInformation("📧 Attempting to send password reset OTP email to {Email}", email);

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword)
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail!, senderName),
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
            _logger.LogError(ex, "❌ Failed to send password reset OTP email to {Email}. Error: {ErrorMessage}", email, ex.Message);
            return false;
        }
    }
}
