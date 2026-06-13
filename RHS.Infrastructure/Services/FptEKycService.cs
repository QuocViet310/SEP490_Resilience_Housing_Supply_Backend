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
        PropertyNameCaseInsensitive = true,
        // FPT AI đôi khi trả về số dưới dạng string (vd: "errorCode": "0")
        // AllowReadingFromString xử lý cả hai trường hợp an toàn.
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
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

    // ── Commit 6: Face Match Implementation ─────────────────────────────

    /// <inheritdoc/>
    public async Task<FaceMatchResponse> MatchFaceAsync(
        FaceMatchRequest  request,
        CancellationToken cancellationToken = default)
    {
        // Bước 1: Validate cả hai file ảnh trước khi gọi API
        await _fileValidator.ValidateAsync(request.FaceImage,   nameof(request.FaceImage));
        await _fileValidator.ValidateAsync(request.IdCardImage, nameof(request.IdCardImage));

        _logger.LogInformation(
            "Bắt đầu gọi FPT AI Face Match API: selfie='{FaceName}', CCCD='{IdName}'.",
            request.FaceImage.FileName, request.IdCardImage.FileName);

        // Bước 2: Xây dựng multipart/form-data với 2 ảnh dưới key "file[]"
        using var content = await BuildFaceMatchFormDataAsync(request.FaceImage, request.IdCardImage);

        // Bước 3: Gọi FPT AI Face Match endpoint
        var rawResponse = await PostToFptAiAsync<FptFaceMatchRawResponse>(
            endpoint:          _options.FaceMatchEndpoint,
            content:           content,
            operationName:     "FaceMatch",
            cancellationToken: cancellationToken);

        // Bước 4: Kiểm tra isBothFace (chỉ áp dụng nếu endpoint trả về field này)
        // Endpoint /dmp/checkface/v1 không trả về "isBothFace" → bỏ qua check khi field rỗng.
        // Endpoint cũ /vision/faceapi/facematch có trả về → vẫn validate bình thường.
        var hasBothFaceField = !string.IsNullOrEmpty(rawResponse.IsBothFace);
        if (hasBothFaceField && !ParseBoolString(rawResponse.IsBothFace))
        {
            _logger.LogWarning(
                "FPT AI FaceMatch: Không phát hiện đủ 2 khuôn mặt trong ảnh. " +
                "isBothFace='{IsBothFace}'.", rawResponse.IsBothFace);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeApiError,
                "Một hoặc cả hai ảnh không chứa khuôn mặt hợp lệ. " +
                "Vui lòng chụp lại ảnh rõ hơn.");
        }

        // Bước 5: Map raw response → application DTO
        var result = MapToFaceMatchResponse(rawResponse);

        _logger.LogInformation(
            "FPT AI Face Match hoàn tất. IsMatch={IsMatch}, Similarity={Similarity:F4}.",
            result.IsMatch, result.Similarity);

        return result;
    }

    // ── Commit 3 (Liveness): Liveness Detection Implementation ───────────

    /// <inheritdoc/>
    public async Task<LivenessDetectionResponse> DetectLivenessAsync(
        LivenessDetectionRequest request,
        CancellationToken        cancellationToken = default)
    {
        // Bước 1: Validate file ảnh selfie (tái sử dụng EKycFileValidator đã có)
        await _fileValidator.ValidateAsync(request.FaceImage, nameof(request.FaceImage));

        _logger.LogInformation(
            "Bắt đầu gọi FPT AI Liveness Detection API cho file '{FileName}' ({Size} bytes).",
            request.FaceImage.FileName, request.FaceImage.Length);

        // Bước 2: Build multipart/form-data (tái sử dụng BuildImageFormDataAsync đã có)
        using var content = await BuildImageFormDataAsync(request.FaceImage, "image");

        // Bước 3: Gọi FPT AI Liveness endpoint (tái sử dụng PostToFptAiAsync đã có)
        var rawResponse = await PostToFptAiAsync<FptLivenessRawResponse>(
            endpoint:          _options.LivenessEndpoint,
            content:           content,
            operationName:     "Liveness",
            cancellationToken: cancellationToken);

        // Bước 4: Map raw response → application DTO
        var result = MapToLivenessResponse(rawResponse);

        _logger.LogInformation(
            "FPT AI Liveness Detection hoàn tất. IsLive={IsLive}, Score={Score:F4}.",
            result.IsLive, result.LivenessScore);

        return result;
    }

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

    // ── Private helpers (Face Match) ──────────────────────────────────────

    /// <summary>
    /// Xây dựng multipart/form-data gửi 2 ảnh lên FPT AI Face Match.
    /// FPT AI yêu cầu cả 2 ảnh đều dùng key "file[]".
    /// </summary>
    private static async Task<MultipartFormDataContent> BuildFaceMatchFormDataAsync(
        IFormFile faceImage,
        IFormFile idCardImage)
    {
        var formData = new MultipartFormDataContent();

        // Ảnh thứ 1: selfie
        var faceStream = new MemoryStream();
        await faceImage.CopyToAsync(faceStream);
        faceStream.Position = 0;
        var faceContent = new StreamContent(faceStream);
        faceContent.Headers.ContentType = new MediaTypeHeaderValue(faceImage.ContentType);
        formData.Add(faceContent, "file[]", faceImage.FileName);

        // Ảnh thứ 2: ảnh chân dung trên CCCD
        var idStream = new MemoryStream();
        await idCardImage.CopyToAsync(idStream);
        idStream.Position = 0;
        var idContent = new StreamContent(idStream);
        idContent.Headers.ContentType = new MediaTypeHeaderValue(idCardImage.ContentType);
        formData.Add(idContent, "file[]", idCardImage.FileName);

        return formData;
    }

    /// <summary>Map raw FPT AI Face Match response → application DTO.
    /// Hỗ trợ cả 2 format: isMatch (string) từ endpoint cũ và match (bool) từ /dmp/checkface/v1.
    /// </summary>
    private static FaceMatchResponse MapToFaceMatchResponse(FptFaceMatchRawResponse raw)
        => new()
        {
            Code    = raw.Code,
            // Ưu tiên field "match" (bool) nếu có, fallback sang "isMatch" (string)
            IsMatch    = raw.Match.HasValue
                ? raw.Match.Value
                : ParseBoolString(raw.IsMatch),
            Similarity = raw.Similarity,
            IsBothFace = ParseBoolString(raw.IsBothFace),
            RequestId  = raw.RequestId
        };

    /// <summary>
    /// FPT AI trả về "true"/"false" dạng string thay vì boolean.
    /// Method này parse an toàn, mặc định false nếu giá trị không hợp lệ.
    /// </summary>
    private static bool ParseBoolString(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    // ── Raw JSON models (private: chi tiết triển khai, không lộ ra ngoài) ─

    private sealed record FptOcrRawResponse
    {
        [JsonPropertyName("errorCode")]    public int                 ErrorCode    { get; init; }
        [JsonPropertyName("errorMessage")] public string              ErrorMessage { get; init; } = string.Empty;
        [JsonPropertyName("data")]         public List<FptOcrRawData> Data         { get; init; } = [];
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

    /// <summary>
    /// Raw JSON model cho FPT AI Face Match response (endpoint: /dmp/checkface/v1).
    /// - Endpoint mới trả về "match" dạng boolean và "similarity" dạng số.
    /// - Giữ lại các field cũ (isMatch, isBothFace) để tương thích ngược.
    /// </summary>
    private sealed record FptFaceMatchRawResponse
    {
        [JsonPropertyName("code")]       public string  Code       { get; init; } = string.Empty;

        // Format cũ (/vision/faceapi/facematch): string "true"/"false"
        [JsonPropertyName("isMatch")]    public string  IsMatch    { get; init; } = string.Empty;

        // Format mới (/dmp/checkface/v1): JSON boolean true/false
        [JsonPropertyName("match")]      public bool?   Match      { get; init; }

        [JsonPropertyName("similarity")] public double  Similarity { get; init; }
        [JsonPropertyName("isBothFace")] public string  IsBothFace { get; init; } = string.Empty;
        [JsonPropertyName("requestId")]  public string  RequestId  { get; init; } = string.Empty;
    }

    /// <summary>
    /// Raw JSON model cho FPT AI Liveness Detection response.
    /// Lưu ý: "liveness" là string "true"/"false" — tương tự pattern của FaceMatch.
    /// </summary>
    private sealed record FptLivenessRawResponse
    {
        [JsonPropertyName("code")]     public string Code     { get; init; } = string.Empty;
        [JsonPropertyName("liveness")] public string IsLive   { get; init; } = string.Empty;
        [JsonPropertyName("score")]    public double Score    { get; init; }
        [JsonPropertyName("message")] public string Message  { get; init; } = string.Empty;
    }

    /// <summary>Map raw FPT AI Liveness response → application DTO.</summary>
    private static LivenessDetectionResponse MapToLivenessResponse(FptLivenessRawResponse raw)
        => new()
        {
            Code          = raw.Code,
            IsLive        = ParseBoolString(raw.IsLive),
            LivenessScore = raw.Score,
            Message       = raw.Message
        };
}
