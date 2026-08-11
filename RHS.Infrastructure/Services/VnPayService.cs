using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using RHS.Application.Interfaces;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Triển khai IVnPayService – xử lý toàn bộ logic giao tiếp với VNPay:
/// - Tạo URL thanh toán có chữ ký HMAC-SHA512
/// - Xác minh chữ ký trong callback
/// </summary>
public class VnPayService : IVnPayService
{
    private readonly IConfiguration _configuration;

    // ── Hằng số VNPay ────────────────────────────────────────────────────
    private const string VnpVersion    = "2.1.0";
    private const string VnpCommand    = "pay";
    private const string VnpCurrCode   = "VND";
    private const string VnpLocale     = "vn";

    public VnPayService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public string CreatePaymentUrl(HttpContext context, VnPaymentRequest request)
    {
        var tmnCode    = _configuration["VnPay:TmnCode"]!;
        var hashSecret = _configuration["VnPay:HashSecret"]!;
        var baseUrl    = _configuration["VnPay:BaseUrl"]!;
        var returnUrl  = ResolveReturnUrl(context);

        // VNPay yêu cầu CreateDate/ExpireDate theo giờ Việt Nam (GMT+7).
        // Không dùng DateTime.Now (UTC trên Docker) — sẽ làm ExpireDate quá hạn ngay.
        var now = GetVietnamNow();
        var expireDate = now.AddMinutes(30);

        // ── Build Dictionary tham số vnp_* (sẽ được sort & ký) ──────────
        // IPN URL cấu hình trên Merchant Admin VNPay → /api/payment/payment-ipn (VnPay:IpnUrl)
        var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"]    = VnpVersion,
            ["vnp_Command"]    = VnpCommand,
            ["vnp_TmnCode"]    = tmnCode,
            ["vnp_Amount"]     = ((long)(request.Amount * 100)).ToString(),   // VNPay nhân 100
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"]   = VnpCurrCode,
            ["vnp_IpAddr"]     = GetClientIpAddress(context),
            ["vnp_Locale"]     = VnpLocale,
            ["vnp_OrderInfo"]  = request.OrderInfo,
            ["vnp_OrderType"]  = request.OrderType,
            ["vnp_ReturnUrl"]  = returnUrl,
            ["vnp_TxnRef"]     = request.OrderId,
            ["vnp_ExpireDate"] = expireDate.ToString("yyyyMMddHHmmss"),
        };

        // ── Tạo chuỗi query & chữ ký HMAC-SHA512 ────────────────────────
        var rawData   = BuildRawData(vnpParams);
        var signature = HmacSha512(hashSecret, rawData);

        // ── Build URL cuối cùng ───────────────────────────────────────────
        var paymentUrl = $"{baseUrl}?{rawData}&vnp_SecureHash={signature}";

        return paymentUrl;
    }

    /// <inheritdoc/>
    public bool ValidateSignature(IQueryCollection queryParams)
    {
        var hashSecret = _configuration["VnPay:HashSecret"]!;

        // Lấy chữ ký VNPay gửi về
        var vnpSecureHash = queryParams["vnp_SecureHash"].ToString();

        if (string.IsNullOrEmpty(vnpSecureHash))
            return false;

        // Thu thập tất cả params vnp_* (trừ vnp_SecureHash và vnp_SecureHashType)
        var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in queryParams)
        {
            if (!string.IsNullOrEmpty(key)
                && key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                vnpParams[key] = value.ToString();
            }
        }

        // Tính lại chữ ký để so sánh
        var rawData        = BuildRawData(vnpParams);
        var expectedHash   = HmacSha512(hashSecret, rawData);

        return string.Equals(expectedHash, vnpSecureHash, StringComparison.OrdinalIgnoreCase);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Giờ Việt Nam (GMT+7) — bắt buộc cho vnp_CreateDate / vnp_ExpireDate.
    /// </summary>
    private static DateTime GetVietnamNow()
    {
        try
        {
            var tzId = OperatingSystem.IsWindows()
                ? "SE Asia Standard Time"
                : "Asia/Ho_Chi_Minh";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }
        catch (Exception)
        {
            // Container thiếu tzdata → cộng cố định +7
            return DateTime.UtcNow.AddHours(7);
        }
    }

    /// <summary>
    /// Ghép các cặp key=value theo thứ tự alphabet (SortedDictionary),
    /// encode value theo chuẩn URL của VNPay (thay %20 bằng +).
    /// </summary>
    private static string BuildRawData(SortedDictionary<string, string> vnpParams)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in vnpParams)
        {
            if (!string.IsNullOrEmpty(value))
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(WebUtility.UrlEncode(key));
                sb.Append('=');
                sb.Append(WebUtility.UrlEncode(value));
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Tính chữ ký HMAC-SHA512 theo yêu cầu của VNPay.
    /// Key = HashSecret, Data = chuỗi rawData đã sort.
    /// </summary>
    private static string HmacSha512(string key, string inputData)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(inputData);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes  = hmac.ComputeHash(dataBytes);

        // VNPay yêu cầu lowercase hex string
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Lấy IP thực của client, ưu tiên X-Forwarded-For (khi qua reverse proxy).
    /// </summary>
    private static string GetClientIpAddress(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            // X-Forwarded-For có thể chứa nhiều IP, lấy IP đầu tiên
            return forwarded.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    /// <summary>
    /// Tự động xác định ReturnUrl cho VNPay:
    /// 1. Nếu VnPay:ReturnUrl được cấu hình bằng domain production thực tế (không chứa localhost/ngrok), dùng cấu hình đó.
    /// 2. Nếu thiếu, chứa localhost/ngrok, hoặc là đường dẫn tương đối:
    ///    Tự động xây dựng URL động từ Scheme + Host của HTTP Request thực tế.
    ///    (Tự động chạy đúng trên cả Localhost, Render, hay Custom Domain mà không cần hardcode).
    /// </summary>
    private string ResolveReturnUrl(HttpContext context)
    {
        var configuredUrl = _configuration["VnPay:ReturnUrl"];

        var isDynamicNeeded = string.IsNullOrWhiteSpace(configuredUrl)
                              || configuredUrl.Contains("ngrok", StringComparison.OrdinalIgnoreCase)
                              || configuredUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                              || !configuredUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);

        if (!isDynamicNeeded)
        {
            return configuredUrl!;
        }

        var scheme = context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault()
                     ?? context.Request.Scheme
                     ?? "https";
        var host = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault()
                   ?? context.Request.Host.Value;

        var path = "/api/payment/payment-callback";

        return $"{scheme}://{host}{path}";
    }
}
