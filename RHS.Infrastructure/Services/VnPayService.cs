using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RHS.Application.Interfaces;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Triển khai IVnPayService – xử lý toàn bộ logic giao tiếp với VNPay:
/// - Tạo URL thanh toán có chữ ký HMAC-SHA512 chuẩn VNPay SDK
/// - Xác minh chữ ký trong callback/IPN
/// </summary>
public class VnPayService : IVnPayService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<VnPayService> _logger;

    // ── Hằng số VNPay ────────────────────────────────────────────────────
    private const string VnpVersion    = "2.1.0";
    private const string VnpCommand    = "pay";
    private const string VnpCurrCode   = "VND";
    private const string VnpLocale     = "vn";

    public VnPayService(IConfiguration configuration, ILogger<VnPayService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string CreatePaymentUrl(HttpContext context, VnPaymentRequest request)
    {
        var rawTmnCode = _configuration["VnPay:TmnCode"]
                      ?? _configuration["VnPay__TmnCode"]
                      ?? _configuration["VNPAY_TMNCODE"]
                      ?? _configuration["VNPAY__TMNCODE"]
                      ?? string.Empty;

        var rawHashSecret = _configuration["VnPay:HashSecret"]
                         ?? _configuration["VnPay__HashSecret"]
                         ?? _configuration["VNPAY_HASHSECRET"]
                         ?? _configuration["VNPAY__HASHSECRET"]
                         ?? string.Empty;

        // Loại bỏ triệt để ký tự xuống dòng (\r, \n), khoảng trắng, tab do copy-paste
        var tmnCode    = CleanSecret(rawTmnCode);
        var hashSecret = CleanSecret(rawHashSecret);

        var baseUrl   = _configuration["VnPay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var returnUrl = ResolveReturnUrl(context);

        if (string.IsNullOrWhiteSpace(tmnCode) || string.IsNullOrWhiteSpace(hashSecret) ||
            tmnCode.StartsWith("YOUR_VNP", StringComparison.OrdinalIgnoreCase) ||
            hashSecret.StartsWith("YOUR_VNP", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("VNPay Error: VnPay:TmnCode hoặc VnPay:HashSecret chưa được cấu hình. Vui lòng cập nhật trong cấu hình.");
            throw new InvalidOperationException("VnPay TmnCode hoặc HashSecret chưa được cấu hình hợp lệ.");
        }

        // VNPay yêu cầu CreateDate/ExpireDate theo giờ Việt Nam (GMT+7).
        var now = GetVietnamNow();
        var expireDate = now.AddMinutes(30);

        // Sanitize OrderInfo: Không dấu, không khoảng trắng, chỉ dùng ký tự an toàn
        var safeOrderInfo = RemoveDiacritics(request.OrderInfo).Replace(" ", "_");

        // ── Build Dictionary tham số vnp_* theo chuẩn so sánh VnPayCompare ──
        var vnpParams = new SortedDictionary<string, string>(new VnPayCompare())
        {
            ["vnp_Version"]    = VnpVersion,
            ["vnp_Command"]    = VnpCommand,
            ["vnp_TmnCode"]    = tmnCode,
            ["vnp_Amount"]     = ((long)(request.Amount * 100)).ToString(),   // VNPay nhân 100
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"]   = VnpCurrCode,
            ["vnp_IpAddr"]     = GetClientIpAddress(context),
            ["vnp_Locale"]     = VnpLocale,
            ["vnp_OrderInfo"]  = safeOrderInfo,
            ["vnp_OrderType"]  = "other",
            ["vnp_ReturnUrl"]  = returnUrl,
            ["vnp_TxnRef"]     = request.OrderId,
            ["vnp_ExpireDate"] = expireDate.ToString("yyyyMMddHHmmss"),
        };

        // ── Tạo chuỗi query & chữ ký HMAC-SHA512 ────────────────────────
        var queryString = BuildQueryString(vnpParams);
        var signature   = HmacSha512(hashSecret, queryString);

        _logger.LogInformation(
            "VNPay URL Generated: TmnCode={TmnCode}, OrderId={OrderId}, Signature={Signature}",
            tmnCode, request.OrderId, signature);

        // ── Build URL cuối cùng ───────────────────────────────────────────
        var paymentUrl = $"{baseUrl}?{queryString}&vnp_SecureHash={signature}";

        return paymentUrl;
    }

    /// <inheritdoc/>
    public bool ValidateSignature(IQueryCollection queryParams)
    {
        var rawHashSecret = _configuration["VnPay:HashSecret"]
                         ?? _configuration["VnPay__HashSecret"]
                         ?? _configuration["VNPAY_HASHSECRET"]
                         ?? _configuration["VNPAY__HASHSECRET"]
                         ?? string.Empty;

        var hashSecret = CleanSecret(rawHashSecret);

        // Lấy chữ ký VNPay gửi về
        var vnpSecureHash = queryParams["vnp_SecureHash"].ToString();

        if (string.IsNullOrEmpty(vnpSecureHash))
            return false;

        // Thu thập tất cả params vnp_* (trừ vnp_SecureHash và vnp_SecureHashType)
        var vnpParams = new SortedDictionary<string, string>(new VnPayCompare());
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

        var rawData      = BuildQueryString(vnpParams);
        var expectedHash = HmacSha512(hashSecret, rawData);

        bool isValid = string.Equals(expectedHash, vnpSecureHash, StringComparison.OrdinalIgnoreCase);

        if (!isValid)
        {
            _logger.LogWarning(
                "VNPay Signature Mismatch! Received={Received}, Expected={Expected}, RawData={RawData}",
                vnpSecureHash, expectedHash, rawData);
        }
        else
        {
            _logger.LogInformation("VNPay Signature Verified Successfully for Order: RawData={RawData}", rawData);
        }

        return isValid;
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
            return DateTime.UtcNow.AddHours(7);
        }
    }

    /// <summary>
    /// Chuỗi dùng làm query string và tính chữ ký — URL-encode theo chuẩn VNPay.
    /// </summary>
    private static string BuildQueryString(SortedDictionary<string, string> vnpParams)
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
            var ip = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(ip) && ip != "::1")
                return ip;
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrEmpty(remoteIp) || remoteIp == "::1")
            return "127.0.0.1";

        return remoteIp;
    }

    /// <summary>
    /// Loại bỏ dấu tiếng Việt để OrderInfo luôn là chuỗi ASCII an toàn cho VNPay.
    /// </summary>
    private static string RemoveDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Thanh toan";

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace("đ", "d")
            .Replace("Đ", "D");
    }

    /// <summary>
    /// Tự động xác định ReturnUrl cho VNPay:
    /// 1. Nếu VnPay:ReturnUrl được cấu hình bằng domain production thực tế, dùng cấu hình đó.
    /// 2. Nếu thiếu hoặc chứa localhost/ngrok: tự động xây dựng URL động từ Scheme + Host.
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

    /// <summary>
    /// Làm sạch triệt để secret/tmnCode: loại bỏ khoảng trắng, \r, \n, \t do paste giao diện Render.
    /// </summary>
    private static string CleanSecret(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(raw, @"\s+", "");
    }
}

/// <summary>
/// Bộ so sánh chuỗi theo chuẩn của VNPay (Ordinal compareInfo en-US).
/// </summary>
public class VnPayCompare : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        var vnpCompare = CompareInfo.GetCompareInfo("en-US");
        return vnpCompare.Compare(x, y, CompareOptions.Ordinal);
    }
}
