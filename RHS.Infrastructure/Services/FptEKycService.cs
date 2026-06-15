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

        // Bước 4: Kiểm tra isBothImgIDCard (chỉ áp dụng nếu endpoint flat trả về field này)
        // Endpoint /dmp/checkface/v1 không trả về field này ở root → bỏ qua khi rỗng.
        var hasBothImgField = !string.IsNullOrEmpty(rawResponse.IsBothImgIDCard);
        if (hasBothImgField && !ParseBoolString(rawResponse.IsBothImgIDCard))
        {
            _logger.LogWarning(
                "FPT AI FaceMatch: Không phát hiện đủ 2 khuôn mặt trong ảnh. " +
                "isBothImgIDCard='{IsBothImgIDCard}'.", rawResponse.IsBothImgIDCard);

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
        // Bước 1: Validate VIDEO + ảnh khuôn mặt (cmnd) — FPT AI Liveness API v3 yêu cầu cả hai
        await _fileValidator.ValidateVideoAsync(request.VideoFile, nameof(request.VideoFile));
        await _fileValidator.ValidateAsync(request.CmndImage, nameof(request.CmndImage));

        _logger.LogInformation(
            "Bắt đầu gọi FPT AI Liveness Detection API v3: video='{VideoName}' ({VideoSize} bytes), face='{FaceName}' ({FaceSize} bytes).",
            request.VideoFile.FileName, request.VideoFile.Length,
            request.CmndImage.FileName, request.CmndImage.Length);

        // Bước 2: Build multipart/form-data với field name "video" và "cmnd" (theo FPT AI docs v3)
        using var content = await BuildLivenessFormDataAsync(request.VideoFile, request.CmndImage);

        // Bước 3: Gọi FPT AI Liveness endpoint
        var rawResponse = await PostToFptAiAsync<FptLivenessRawResponse>(
            endpoint:          _options.LivenessEndpoint,
            content:           content,
            operationName:     "Liveness",
            cancellationToken: cancellationToken);

        // Bước 4: Map raw response → application DTO
        var result = MapToLivenessResponse(rawResponse);

        _logger.LogInformation(
            "FPT AI Liveness Detection hoàn tất. IsLive={IsLive}, SpoofProb={SpoofProb}, Code={Code}.",
            result.IsLive, result.SpoofProbability, result.Code);

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

    /// <summary>
    /// Xây dựng multipart/form-data gửi video + ảnh khuôn mặt lên FPT AI Liveness v3.
    /// FPT AI Liveness v3 yêu cầu field name "video" cho video và "cmnd" cho ảnh khuôn mặt.
    /// </summary>
    private static async Task<MultipartFormDataContent> BuildLivenessFormDataAsync(
        IFormFile videoFile,
        IFormFile cmndImage)
    {
        var formData = new MultipartFormDataContent();

        // Video: field name "video"
        var videoStream = new MemoryStream();
        await videoFile.CopyToAsync(videoStream);
        videoStream.Position = 0;
        var videoContent = new StreamContent(videoStream);
        videoContent.Headers.ContentType = new MediaTypeHeaderValue(videoFile.ContentType);
        formData.Add(videoContent, "video", videoFile.FileName);

        // Ảnh khuôn mặt: field name "cmnd"
        var cmndStream = new MemoryStream();
        await cmndImage.CopyToAsync(cmndStream);
        cmndStream.Position = 0;
        var cmndContent = new StreamContent(cmndStream);
        cmndContent.Headers.ContentType = new MediaTypeHeaderValue(cmndImage.ContentType);
        formData.Add(cmndContent, "cmnd", cmndImage.FileName);

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
    /// Ưu tiên data nested từ /dmp/checkface/v1, fallback sang flat format (endpoint cũ).
    /// FPT AI dùng "isMatch" (boolean) trong nested data, không phải "match".
    /// </summary>
    private static FaceMatchResponse MapToFaceMatchResponse(FptFaceMatchRawResponse raw)
    {
        bool   isMatch;
        double similarity;
        bool   isBothImgIdCard;

        if (raw.Data is not null)
        {
            // Endpoint mới /dmp/checkface/v1: dữ liệu trong nested "data" object
            isMatch        = raw.Data.IsMatch ?? raw.Data.Match ?? false;
            similarity     = raw.Data.Similarity;
            isBothImgIdCard = raw.Data.IsBothImgIDCard ?? false;
        }
        else
        {
            // Endpoint cũ /vision/faceapi/facematch: flat format
            isMatch        = raw.Match ?? ParseBoolString(raw.IsMatch);
            similarity     = raw.Similarity;
            isBothImgIdCard = ParseBoolString(raw.IsBothImgIDCard);
        }

        return new()
        {
            Code           = raw.Code,
            IsMatch        = isMatch,
            Similarity     = similarity,
            IsBothImgIdCard = isBothImgIdCard,
            FptMessage     = raw.Message
        };
    }

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
    /// 
    /// Endpoint mới trả về cấu trúc NESTED:
    /// <code>
    /// { "code": "200", "data": { "match": true, "similarity": 99.87 } }
    /// </code>
    /// Giữ lại các field flat (isMatch, match) để tương thích với endpoint cũ.
    /// </summary>
    private sealed record FptFaceMatchRawResponse
    {
        [JsonPropertyName("code")]    public string Code    { get; init; } = string.Empty;
        [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;

        // Nested format: /dmp/checkface/v1 trả về data lồng trong "data" object
        [JsonPropertyName("data")] public FptFaceMatchData? Data { get; init; }

        // Flat format cũ (/vision/faceapi/facematch)
        [JsonPropertyName("isMatch")]        public string IsMatch        { get; init; } = string.Empty;
        [JsonPropertyName("match")]          public bool?  Match          { get; init; }
        [JsonPropertyName("similarity")]     public double Similarity     { get; init; }
        [JsonPropertyName("isBothImgIDCard")] public string IsBothImgIDCard { get; init; } = string.Empty;
    }

    /// <summary>
    /// Dữ liệu kết quả nằm trong field "data" của FPT AI /dmp/checkface/v1.
    /// FPT AI docs: isMatch, similarity, isBothImgIDCard.
    /// </summary>
    private sealed record FptFaceMatchData
    {
        [JsonPropertyName("isMatch")]        public bool?  IsMatch        { get; init; }
        [JsonPropertyName("match")]          public bool?  Match          { get; init; } // dự phòng alias
        [JsonPropertyName("similarity")]     public double Similarity     { get; init; }
        // Theo docs: "isBothImgIDCard" — không phải "isBothFace"
        [JsonPropertyName("isBothImgIDCard")] public bool? IsBothImgIDCard { get; init; }
    }

    /// <summary>
    /// Raw JSON model cho FPT AI Liveness Detection response.
    /// 
    /// Cấu trúc nested theo docs:
    /// <code>
    /// {
    ///   "code": "200", "message": "request successful",
    ///   "liveness": { "is_live": "true", "spoof_prob": "0.35", "need_to_review": "false",
    ///                  "is_deepfake": "N/A", "deepfake_prob": "N/A", "warning": "" },
    ///   "face_match": { ... }  // optional
    /// }
    /// </code>
    /// </summary>
    private sealed record FptLivenessRawResponse
    {
        [JsonPropertyName("code")]     public string          Code      { get; init; } = string.Empty;
        [JsonPropertyName("message")] public string          Message   { get; init; } = string.Empty;
        [JsonPropertyName("liveness")] public FptLivenessData? Liveness { get; init; }
    }

    /// <summary>
    /// Nested object "liveness" trong FPT AI Liveness response.
    /// Tất cả các field dạng string (kể cả bool và float) vì FPT AI có thể trả về "N/A".
    /// </summary>
    private sealed record FptLivenessData
    {
        [JsonPropertyName("code")]           public string Code          { get; init; } = string.Empty;
        [JsonPropertyName("message")]        public string Message       { get; init; } = string.Empty;
        [JsonPropertyName("is_live")]        public string IsLive        { get; init; } = string.Empty; // "true"/"false"
        [JsonPropertyName("spoof_prob")]     public string SpoofProb     { get; init; } = string.Empty; // "0.3587" or "N/A"
        [JsonPropertyName("need_to_review")] public string NeedToReview  { get; init; } = string.Empty;
        [JsonPropertyName("is_deepfake")]    public string IsDeepfake    { get; init; } = string.Empty; // "true"/"false"/"N/A"
        [JsonPropertyName("deepfake_prob")]  public string DeepfakeProbability { get; init; } = string.Empty;
        [JsonPropertyName("warning")]        public string Warning       { get; init; } = string.Empty;
    }

    /// <summary>Map raw FPT AI Liveness response → application DTO.</summary>
    private static LivenessDetectionResponse MapToLivenessResponse(FptLivenessRawResponse raw)
    {
        var l = raw.Liveness; // nested liveness object
        return new()
        {
            Code            = raw.Code,
            FptMessage      = raw.Message,
            IsLive          = ParseBoolString(l?.IsLive),
            SpoofProbability = TryParseDouble(l?.SpoofProb),
            NeedToReview    = ParseBoolString(l?.NeedToReview),
            IsDeepfake      = ParseBoolString(l?.IsDeepfake),
            Warning         = l?.Warning ?? string.Empty,
            LivenessCode    = l?.Code    ?? string.Empty,
            LivenessMessage = l?.Message ?? string.Empty
        };
    }

    /// <summary>Parse string → double an toàn. Trả về 0 nếu không parse được (ví dụ "N/A").</summary>
    private static double TryParseDouble(string? value)
        => double.TryParse(value, System.Globalization.NumberStyles.Float,
               System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
}
