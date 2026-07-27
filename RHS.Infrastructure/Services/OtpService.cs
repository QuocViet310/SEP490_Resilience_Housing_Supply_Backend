using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RHS.Application.Interfaces;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

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

        var resendApiKey = _configuration["EmailSettings:ResendApiKey"] ?? _configuration["ResendApiKey"];
        var senderName = _configuration["EmailSettings:SenderName"] ?? "Resilience Housing Supply";
        var subject = "Mã xác thực OTP - Resilience Housing Supply";
        var bodyHtml = $@"
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
            </html>";

        // 1️⃣ ƯU TIÊN 1: Dùng Resend HTTP REST API (Gửi qua HTTPS Cổng 443 - Không bao giờ bị Render chặn)
        if (!string.IsNullOrEmpty(resendApiKey))
        {
            var resendSuccess = await TrySendViaResendApiAsync(resendApiKey, email, subject, bodyHtml, senderName);
            if (resendSuccess) return true;
        }

        // 2️⃣ ƯU TIÊN 2: Kiểm tra nếu bật Mock hoặc cấu hình mặc định
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
            _logger.LogInformation("💡 [MOCK OTP MODE] Skipping SMTP call. OTP code for {Email} is: {OtpCode}", email, otpCode);
            return true;
        }

        // 3️⃣ ƯU TIÊN 3: Gửi qua SMTP (Timeout ngắn 3 giây tránh làm đơ app nếu Render chặn cổng 587)
        try
        {
            var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
            _logger.LogInformation("📧 Attempting to send OTP email via SMTP to {Email} ({Server}:{Port})", email, smtpServer, smtpPort);

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                Timeout = 3000 // Timeout ngắn 3 giây
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };
            mailMessage.To.Add(email);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("✅ OTP email sent successfully to {Email} via SMTP", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send OTP email to {Email}. Render/SMTP error: {ErrorMessage}. Fallback OTP code: {OtpCode}", email, ex.Message, otpCode);
            return true;
        }
    }

    public async Task<bool> SendPasswordResetOtpEmailAsync(string email, string otpCode, string fullName)
    {
        Console.WriteLine($"=================================================");
        Console.WriteLine($"🔑 [RESET OTP GENERATED] Email: {email} | OTP Code: {otpCode}");
        Console.WriteLine($"=================================================");
        _logger.LogInformation("🔑 [RESET OTP GENERATED] Target Email: {Email} | OTP Code: {OtpCode}", email, otpCode);

        var resendApiKey = _configuration["EmailSettings:ResendApiKey"] ?? _configuration["ResendApiKey"];
        var senderName = _configuration["EmailSettings:SenderName"] ?? "Resilience Housing Supply";
        var subject = "Đặt lại mật khẩu - Resilience Housing Supply";
        var bodyHtml = $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>Xin chào {fullName},</h2>
                <p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản của mình.</p>
                <p>Mã OTP để đặt lại mật khẩu của bạn là:</p>
                <h1 style='color: #FF5722; font-size: 32px; letter-spacing: 5px;'>{otpCode}</h1>
                <p>Mã này sẽ hết hạn sau 5 phút.</p>
                <p><strong>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</strong></p>
                <br>
                <p>Trân trọng,</p>
                <p><strong>Resilience Housing Supply Team</strong></p>
            </body>
            </html>";

        if (!string.IsNullOrEmpty(resendApiKey))
        {
            var resendSuccess = await TrySendViaResendApiAsync(resendApiKey, email, subject, bodyHtml, senderName);
            if (resendSuccess) return true;
        }

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
            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                Timeout = 3000
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };
            mailMessage.To.Add(email);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("✅ Password reset OTP email sent successfully to {Email} via SMTP", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send password reset OTP email to {Email}. Render/SMTP error: {ErrorMessage}. Fallback OTP code: {OtpCode}", email, ex.Message, otpCode);
            return true;
        }
    }

    private async Task<bool> TrySendViaResendApiAsync(string apiKey, string toEmail, string subject, string bodyHtml, string senderName)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var customSender = _configuration["EmailSettings:SenderEmail"];
            var fromAddress = string.IsNullOrEmpty(customSender) || customSender.Contains("example.com")
                ? $"{senderName} <onboarding@resend.dev>"
                : $"{senderName} <{customSender}>";

            var payload = new
            {
                from = fromAddress,
                to = new[] { toEmail },
                subject = subject,
                html = bodyHtml
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("🚀 Attempting to send email via Resend HTTPS API to {Email}", toEmail);
            var response = await client.PostAsync("https://api.resend.com/emails", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Email sent successfully via Resend HTTPS API to {Email}", toEmail);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("⚠️ Resend HTTPS API returned status {StatusCode}: {Error}", response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception sending email via Resend HTTPS API to {Email}", toEmail);
            return false;
        }
    }
}
