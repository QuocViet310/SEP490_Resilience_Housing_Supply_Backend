using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RHS.Application.Interfaces;
using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

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

        var brevoApiKey = _configuration["EmailSettings:BrevoApiKey"] ?? _configuration["BrevoApiKey"];
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

        // 1️⃣ ƯU TIÊN 1: Dùng Brevo REST API (Nếu có cấu hình BrevoApiKey)
        if (!string.IsNullOrEmpty(brevoApiKey))
        {
            var brevoSuccess = await TrySendViaBrevoApiAsync(brevoApiKey, email, subject, bodyHtml, senderName);
            if (brevoSuccess) return true;
        }

        // 2️⃣ ƯU TIÊN 2: Dùng Resend REST API (Nếu có cấu hình ResendApiKey)
        if (!string.IsNullOrEmpty(resendApiKey))
        {
            var resendSuccess = await TrySendViaResendApiAsync(resendApiKey, email, subject, bodyHtml, senderName);
            if (resendSuccess) return true;
        }

        // 3️⃣ ƯU TIÊN 3: Kiểm tra nếu bật Mock hoặc cấu hình mặc định chưa đổi
        var enableMock = _configuration["EmailSettings:EnableMock"];
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var senderPassword = _configuration["EmailSettings:SenderPassword"];

        if (string.Equals(enableMock, "true", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(senderEmail) ||
            senderEmail.Contains("example.com") ||
            string.IsNullOrEmpty(senderPassword) ||
            senderPassword == "YOUR_EMAIL_APP_PASSWORD")
        {
            _logger.LogInformation("💡 [MOCK OTP MODE] Skipping Email call. OTP code for {Email} is: {OtpCode}", email, otpCode);
            return true;
        }

        // 4️⃣ ƯU TIÊN 4: Gửi qua Gmail / SMTP bằng MailKit (Dùng Cổng 465 SSL mở trên Cloud/Render)
        try
        {
            return await SendViaMailKitAsync(email, subject, bodyHtml, fullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MailKit SMTP error sending to {Email}: {ErrorMessage}. Fallback OTP: {OtpCode}", email, ex.Message, otpCode);
            return true;
        }
    }

    public async Task<bool> SendPasswordResetOtpEmailAsync(string email, string otpCode, string fullName)
    {
        Console.WriteLine($"=================================================");
        Console.WriteLine($"🔑 [RESET OTP GENERATED] Email: {email} | OTP Code: {otpCode}");
        Console.WriteLine($"=================================================");
        _logger.LogInformation("🔑 [RESET OTP GENERATED] Target Email: {Email} | OTP Code: {OtpCode}", email, otpCode);

        var brevoApiKey = _configuration["EmailSettings:BrevoApiKey"] ?? _configuration["BrevoApiKey"];
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

        if (!string.IsNullOrEmpty(brevoApiKey))
        {
            var brevoSuccess = await TrySendViaBrevoApiAsync(brevoApiKey, email, subject, bodyHtml, senderName);
            if (brevoSuccess) return true;
        }

        if (!string.IsNullOrEmpty(resendApiKey))
        {
            var resendSuccess = await TrySendViaResendApiAsync(resendApiKey, email, subject, bodyHtml, senderName);
            if (resendSuccess) return true;
        }

        var enableMock = _configuration["EmailSettings:EnableMock"];
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var senderPassword = _configuration["EmailSettings:SenderPassword"];

        if (string.Equals(enableMock, "true", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(senderEmail) ||
            senderEmail.Contains("example.com") ||
            string.IsNullOrEmpty(senderPassword) ||
            senderPassword == "YOUR_EMAIL_APP_PASSWORD")
        {
            _logger.LogInformation("💡 [MOCK OTP MODE] Skipping Email call for Reset. OTP code for {Email} is: {OtpCode}", email, otpCode);
            return true;
        }

        try
        {
            return await SendViaMailKitAsync(email, subject, bodyHtml, fullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MailKit SMTP error sending to {Email}: {ErrorMessage}. Fallback OTP: {OtpCode}", email, ex.Message, otpCode);
            return true;
        }
    }

    private async Task<bool> SendViaMailKitAsync(string toEmail, string subject, string bodyHtml, string fullName)
    {
        var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
        var portStr = _configuration["EmailSettings:SmtpPort"];
        
        // Mặc định cổng 465 (SSL) nếu dùng Gmail hoặc nếu không chỉ định cổng (Cổng 465 không bị Render chặn)
        int smtpPort = !string.IsNullOrEmpty(portStr) ? int.Parse(portStr) : 465;
        if (smtpServer.Contains("gmail") && smtpPort == 587)
        {
            // Tự động chuyển cổng 587 -> 465 trên Cloud để không bị chặn kết nối
            smtpPort = 465;
        }

        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var senderPassword = _configuration["EmailSettings:SenderPassword"];
        var senderName = _configuration["EmailSettings:SenderName"] ?? "Resilience Housing Supply";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(new MailboxAddress(fullName, toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = bodyHtml };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new MailKit.Net.Smtp.SmtpClient();
        
        SecureSocketOptions options = smtpPort switch
        {
            465 => SecureSocketOptions.SslOnConnect, // Cổng SSL 465 không bị Render chặn
            587 => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.Auto
        };

        _logger.LogInformation("📧 MailKit connecting to {Server}:{Port} ({Options})", smtpServer, smtpPort, options);

        client.Timeout = 10000; // 10s
        await client.ConnectAsync(smtpServer, smtpPort, options);
        await client.AuthenticateAsync(senderEmail, senderPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogInformation("✅ OTP email sent successfully to {Email} via MailKit (Port {Port})", toEmail, smtpPort);
        return true;
    }

    private async Task<bool> TrySendViaResendApiAsync(string apiKey, string toEmail, string subject, string bodyHtml, string senderName)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var resendFrom = _configuration["EmailSettings:ResendFromEmail"];
            string fromAddress;

            if (!string.IsNullOrEmpty(resendFrom) && !resendFrom.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                fromAddress = $"{senderName} <{resendFrom}>";
            }
            else
            {
                fromAddress = $"{senderName} <onboarding@resend.dev>";
            }

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

    private async Task<bool> TrySendViaBrevoApiAsync(string apiKey, string toEmail, string subject, string bodyHtml, string senderName)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("api-key", apiKey);

            var customSender = _configuration["EmailSettings:SenderEmail"];
            var senderEmail = string.IsNullOrEmpty(customSender) || customSender.Contains("example.com")
                ? "no-reply@rhs.local"
                : customSender;

            var payload = new
            {
                sender = new { name = senderName, email = senderEmail },
                to = new[] { new { email = toEmail } },
                subject = subject,
                htmlContent = bodyHtml
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("🚀 Attempting to send email via Brevo HTTPS API to {Email}", toEmail);
            var response = await client.PostAsync("https://api.brevo.com/v3/smtp/email", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Email sent successfully via Brevo HTTPS API to {Email}", toEmail);
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("⚠️ Brevo HTTPS API returned status {StatusCode}: {Error}", response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception sending email via Brevo HTTPS API to {Email}", toEmail);
            return false;
        }
    }
}
