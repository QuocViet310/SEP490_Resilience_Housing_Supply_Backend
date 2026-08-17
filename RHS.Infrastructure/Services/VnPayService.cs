using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RHS.Application.Interfaces;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Triển khai IVnPayService chuẩn theo VnPayLibrary chính thức của VNPay (.NET)
/// </summary>
public class VnPayService : IVnPayService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<VnPayService> _logger;

    private const string VnpVersion  = "2.1.0";
    private const string VnpCommand  = "pay";
    private const string VnpCurrCode = "VND";
    private const string VnpLocale   = "vn";

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

        var tmnCode    = CleanSecret(rawTmnCode);
        var hashSecret = CleanSecret(rawHashSecret);
        var baseUrl    = _configuration["VnPay:BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
        var returnUrl  = ResolveReturnUrl(context);

        if (string.IsNullOrWhiteSpace(tmnCode) || string.IsNullOrWhiteSpace(hashSecret) ||
            tmnCode.StartsWith("YOUR_VNP", StringComparison.OrdinalIgnoreCase) ||
            hashSecret.StartsWith("YOUR_VNP", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("VNPay Error: VnPay:TmnCode hoặc VnPay:HashSecret chưa được cấu hình.");
            throw new InvalidOperationException("VnPay TmnCode hoặc HashSecret chưa được cấu hình hợp lệ.");
        }

        var now = GetVietnamNow();
        var safeOrderInfo = string.IsNullOrWhiteSpace(request.OrderInfo)
            ? $"Thanh_toan_{request.OrderId}"
            : RemoveDiacritics(request.OrderInfo).Replace(" ", "_");

        var vnpay = new VnPayLibrary();
        vnpay.AddRequestData("vnp_Version", VnpVersion);
        vnpay.AddRequestData("vnp_Command", VnpCommand);
        vnpay.AddRequestData("vnp_TmnCode", tmnCode);
        vnpay.AddRequestData("vnp_Amount", ((long)(request.Amount * 100)).ToString());
        vnpay.AddRequestData("vnp_CreateDate", now.ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", VnpCurrCode);
        vnpay.AddRequestData("vnp_IpAddr", GetClientIpAddress(context));
        vnpay.AddRequestData("vnp_Locale", VnpLocale);
        vnpay.AddRequestData("vnp_OrderInfo", safeOrderInfo);
        vnpay.AddRequestData("vnp_OrderType", "other");
        vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);
        vnpay.AddRequestData("vnp_TxnRef", request.OrderId);

        var paymentUrl = vnpay.CreateRequestUrl(baseUrl, hashSecret);

        _logger.LogInformation(
            "VNPay URL Generated: TmnCode={TmnCode}, OrderId={OrderId}, PaymentUrl={PaymentUrl}",
            tmnCode, request.OrderId, paymentUrl);

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
        var vnpSecureHash = queryParams["vnp_SecureHash"].ToString();

        if (string.IsNullOrEmpty(vnpSecureHash))
            return false;

        var vnpay = new VnPayLibrary();
        foreach (var (key, value) in queryParams)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
            {
                vnpay.AddResponseData(key, value.ToString());
            }
        }

        bool isValid = vnpay.ValidateSignature(vnpSecureHash, hashSecret);

        if (!isValid)
        {
            _logger.LogWarning("VNPay Signature Mismatch for incoming callback/IPN!");
        }
        else
        {
            _logger.LogInformation("VNPay Signature Verified Successfully!");
        }

        return isValid;
    }

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

    private static string RemoveDiacritics(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Thanh_toan";

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

    private string ResolveReturnUrl(HttpContext context)
    {
        var configuredUrl = _configuration["VnPay:ReturnUrl"]
                         ?? _configuration["VnPay__ReturnUrl"]
                         ?? _configuration["VNPAY_RETURNURL"];

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

    private static string CleanSecret(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        return Regex.Replace(raw, @"\s+", "");
    }
}

/// <summary>
/// Thư viện chuẩn VnPayLibrary theo official SDK của VNPAY cho C# .NET
/// </summary>
public class VnPayLibrary
{
    private readonly SortedList<string, string> _requestData = new SortedList<string, string>(new VnPayCompare());
    private readonly SortedList<string, string> _responseData = new SortedList<string, string>(new VnPayCompare());

    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _requestData[key] = value;
        }
    }

    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _responseData[key] = value;
        }
    }

    public string GetResponseData(string key)
    {
        return _responseData.TryGetValue(key, out var retValue) ? retValue : string.Empty;
    }

    public string CreateRequestUrl(string baseUrl, string vnp_HashSecret)
    {
        var data = new StringBuilder();
        foreach (var (key, value) in _requestData)
        {
            if (!string.IsNullOrEmpty(value))
            {
                data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
            }
        }

        var queryString = data.ToString();
        var signData = queryString;
        if (signData.Length > 0)
        {
            signData = signData.Remove(data.Length - 1, 1);
        }

        var vnp_SecureHash = HmacSHA512(vnp_HashSecret, signData);
        return baseUrl + "?" + queryString + "vnp_SecureHash=" + vnp_SecureHash;
    }

    public bool ValidateSignature(string inputHash, string secretKey)
    {
        var rspRaw = GetResponseData();
        var myChecksum = HmacSHA512(secretKey, rspRaw);
        return myChecksum.Equals(inputHash, StringComparison.OrdinalIgnoreCase);
    }

    private string GetResponseData()
    {
        var data = new StringBuilder();
        if (_responseData.ContainsKey("vnp_SecureHashType"))
        {
            _responseData.Remove("vnp_SecureHashType");
        }
        if (_responseData.ContainsKey("vnp_SecureHash"))
        {
            _responseData.Remove("vnp_SecureHash");
        }
        foreach (var (key, value) in _responseData)
        {
            if (!string.IsNullOrEmpty(value))
            {
                data.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(value) + "&");
            }
        }
        if (data.Length > 0)
        {
            data.Remove(data.Length - 1, 1);
        }
        return data.ToString();
    }

    public static string HmacSHA512(string key, string inputData)
    {
        var hash = new StringBuilder();
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] inputBytes = Encoding.UTF8.GetBytes(inputData);
        using var hmac = new HMACSHA512(keyBytes);
        byte[] hashValue = hmac.ComputeHash(inputBytes);
        foreach (var theByte in hashValue)
        {
            hash.Append(theByte.ToString("x2"));
        }
        return hash.ToString();
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
