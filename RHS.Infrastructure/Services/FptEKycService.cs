using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RHS.Application.DTOs.EKyc;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Exceptions;
using RHS.Infrastructure.Validators;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Triển khai <see cref="IEKycService"/> bằng cách gọi FPT AI REST API.
/// Sử dụng <see cref="IHttpClientFactory"/> (named client "FptAiHttpClient")
/// để tái sử dụng connection pool và tránh socket exhaustion.
/// </summary>
public sealed class FptEKycService : IEKycService
{
    /// <summary>Tên của named HttpClient được đăng ký ở Commit 7.</summary>
    public const string HttpClientName = "FptAiHttpClient";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FptAiOptions       _options;
    private readonly EKycFileValidator  _fileValidator;
    private readonly ILogger<FptEKycService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FptEKycService(
        IHttpClientFactory          httpClientFactory,
        IOptions<FptAiOptions>      options,
        EKycFileValidator           fileValidator,
        ILogger<FptEKycService>     logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _fileValidator     = fileValidator;
        _logger            = logger;
    }

    // ── Commit 5: OCR Implementation ─────────────────────────────────────

    /// <inheritdoc/>
    public async Task<OcrIdCardResponse> ExtractIdCardAsync(
        OcrIdCardRequest  request,
        CancellationToken cancellationToken = default)
    {
        // Bước 1: Validate file ảnh trước khi gọi API
        await _fileValidator.ValidateAsync(request.Image, nameof(request.Image));

        _logger.LogInformation(
            "Bắt đầu gọi FPT AI OCR API cho file '{FileName}' ({Size} bytes).",
            request.Image.FileName, request.Image.Length);

        // Bước 2: Xây dựng multipart/form-data body
        using var content = await BuildImageFormDataAsync(request.Image, "image");

        // Bước 3: Gọi FPT AI OCR endpoint
        var rawResponse = await PostToFptAiAsync<FptOcrRawResponse>(
            endpoint:          _options.OcrEndpoint,
            content:           content,
            operationName:     "OCR",
            cancellationToken: cancellationToken);

        // Bước 4: Kiểm tra business-level error code từ FPT AI
        if (rawResponse.ErrorCode != 0)
        {
            _logger.LogWarning(
                "FPT AI OCR trả về lỗi: errorCode={Code}, message='{Message}'.",
                rawResponse.ErrorCode, rawResponse.ErrorMessage);

            throw new EKycIntegrationException(
                fptAiErrorCode:    rawResponse.ErrorCode,
                fptAiErrorMessage: rawResponse.ErrorMessage,
                isFptAiCode:       true);
        }

        // Bước 5: Map raw response → application DTO
        var firstResult = rawResponse.Data.FirstOrDefault();

        _logger.LogInformation(
            "FPT AI OCR thành công. CCCD ID='{Id}', loại thẻ='{Type}', độ tin cậy={Score:P1}.",
            firstResult?.Id, firstResult?.Type, firstResult?.OverallScore);

        return MapToOcrResponse(rawResponse);
    }

    // ── Commit 6: Face Match stub (sẽ hoàn thiện ở commit tiếp theo) ─────

    /// <inheritdoc/>
    public Task<FaceMatchResponse> MatchFaceAsync(
        FaceMatchRequest  request,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException(
            "MatchFaceAsync sẽ được triển khai ở Commit 6.");

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Gọi FPT AI endpoint bằng POST multipart/form-data, xử lý tập trung mọi loại lỗi HTTP:
    /// timeout, lỗi HTTP status, và lỗi parse JSON.
    /// </summary>
    private async Task<TResponse> PostToFptAiAsync<TResponse>(
        string                   endpoint,
        MultipartFormDataContent content,
        string                   operationName,
        CancellationToken        cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await httpClient.PostAsync(endpoint, content, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout (không phải do caller hủy)
            _logger.LogError(ex, "FPT AI {Op} API timeout sau {Timeout} giây.",
                operationName, _options.TimeoutSeconds);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeTimeout,
                $"Yêu cầu tới FPT AI {operationName} API bị timeout sau {_options.TimeoutSeconds} giây.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không thể kết nối tới FPT AI {Op} API.", operationName);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeHttpError,
                $"Không thể kết nối tới FPT AI {operationName} API: {ex.Message}",
                ex);
        }

        // Kiểm tra HTTP status code
        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "FPT AI {Op} API trả về HTTP {StatusCode}. Body: {Body}",
                operationName, (int)httpResponse.StatusCode, body);

            throw new EKycIntegrationException(
                (int)httpResponse.StatusCode,
                $"FPT AI {operationName} API trả về HTTP {(int)httpResponse.StatusCode}: {body}");
        }

        // Deserialize JSON response
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions);
            if (result is null)
                throw new EKycIntegrationException(
                    EKycIntegrationException.CodeInvalidResponse,
                    $"FPT AI {operationName} API trả về response rỗng hoặc null.");

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Không parse được JSON response từ FPT AI {Op} API. Body: {Body}",
                operationName, responseBody);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeInvalidResponse,
                $"Không parse được JSON response từ FPT AI {operationName} API.",
                ex);
        }
    }

    /// <summary>
    /// Đọc <see cref="IFormFile"/> thành <see cref="MultipartFormDataContent"/>.
    /// Stream được giải phóng khi <paramref name="content"/> bị Dispose.
    /// </summary>
    private static async Task<MultipartFormDataContent> BuildImageFormDataAsync(
        IFormFile file,
        string    formFieldName)
    {
        var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType =
            new MediaTypeHeaderValue(file.ContentType);

        var formData = new MultipartFormDataContent();
        formData.Add(streamContent, formFieldName, file.FileName);
        return formData;
    }

    /// <summary>Map raw FPT AI OCR response → application DTO.</summary>
    private static OcrIdCardResponse MapToOcrResponse(FptOcrRawResponse raw)
    {
        var first = raw.Data.FirstOrDefault();
        return new OcrIdCardResponse
        {
            ErrorCode    = raw.ErrorCode,
            ErrorMessage = raw.ErrorMessage,
            Data         = first is null ? null : new OcrIdCardData
            {
                Id           = first.Id,
                Name         = first.Name,
                Dob          = first.Dob,
                Sex          = first.Sex,
                Nationality  = first.Nationality,
                Home         = first.Home,
                Address      = first.Address,
                Doe          = first.Doe,
                IssueDate    = first.IssueDate,
                IssueLoc     = first.IssueLoc,
                Type         = first.Type,
                OverallScore = first.OverallScore
            }
        };
    }

    // ── Raw JSON models (private: chi tiết triển khai, không lộ ra ngoài) ─

    private sealed record FptOcrRawResponse
    {
        [JsonPropertyName("errorCode")]    public int               ErrorCode    { get; init; }
        [JsonPropertyName("errorMessage")] public string            ErrorMessage { get; init; } = string.Empty;
        [JsonPropertyName("data")]         public List<FptOcrRawData> Data       { get; init; } = [];
    }

    private sealed record FptOcrRawData
    {
        [JsonPropertyName("id")]            public string Id           { get; init; } = string.Empty;
        [JsonPropertyName("name")]          public string Name         { get; init; } = string.Empty;
        [JsonPropertyName("dob")]           public string Dob          { get; init; } = string.Empty;
        [JsonPropertyName("sex")]           public string Sex          { get; init; } = string.Empty;
        [JsonPropertyName("nationality")]   public string Nationality  { get; init; } = string.Empty;
        [JsonPropertyName("home")]          public string Home         { get; init; } = string.Empty;
        [JsonPropertyName("address")]       public string Address      { get; init; } = string.Empty;
        [JsonPropertyName("doe")]           public string Doe          { get; init; } = string.Empty;
        [JsonPropertyName("issue_date")]    public string IssueDate    { get; init; } = string.Empty;
        [JsonPropertyName("issue_loc")]     public string IssueLoc     { get; init; } = string.Empty;
        [JsonPropertyName("type")]          public string Type         { get; init; } = string.Empty;
        [JsonPropertyName("overall_score")] public double OverallScore { get; init; }
    }
}
