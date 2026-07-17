using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RHS.Application.DTOs.EKyc;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Exceptions;
using RHS.Infrastructure.Validators;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Triển khai <see cref="IEKycService"/> bằng cách gọi VNPT eKYC REST API.
/// <para>
/// VNPT eKYC sử dụng kiến trúc 2-step:
/// <list type="number">
///   <item>Upload file lên <c>/file-service/v1/addFile</c> → nhận <c>hash</c>.</item>
///   <item>Gọi AI service (OCR/FaceCompare) bằng <c>hash</c> từ bước 1.</item>
/// </list>
/// </para>
/// <para>
/// Authentication: 3 headers bắt buộc — <c>Authorization: Bearer {access_token}</c>,
/// <c>Token-id</c>, <c>Token-key</c>.
/// </para>
/// <para>
/// ⚠️ <b>AccessToken hết hạn sau 8 tiếng.</b>
/// Khi nhận HTTP 401 từ VNPT, service sẽ throw <see cref="EKycIntegrationException"/>
/// với mã <c>EKYC_TOKEN_EXPIRED</c> để thông báo admin cần lấy token mới từ Dashboard VNPT.
/// </para>
/// </summary>
public sealed class VnptEKycService : IEKycService
{
    /// <summary>Tên của named HttpClient được đăng ký trong DI.</summary>
    public const string HttpClientName = "VnptEKycHttpClient";

    private readonly IHttpClientFactory         _httpClientFactory;
    private readonly VnptEKycOptions            _options;
    private readonly EKycFileValidator          _fileValidator;
    private readonly ILogger<VnptEKycService>   _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public VnptEKycService(
        IHttpClientFactory          httpClientFactory,
        IOptions<VnptEKycOptions>   options,
        EKycFileValidator           fileValidator,
        ILogger<VnptEKycService>    logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _fileValidator     = fileValidator;
        _logger            = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  OCR — Trích xuất thông tin từ ảnh CCCD
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public async Task<OcrIdCardResponse> ExtractIdCardAsync(
        OcrIdCardRequest  request,
        CancellationToken cancellationToken = default)
    {
        // Bước 1: Validate file ảnh
        await _fileValidator.ValidateAsync(request.Image, nameof(request.Image));

        _logger.LogInformation(
            "VNPT OCR: Bắt đầu xử lý file '{FileName}' ({Size} bytes).",
            request.Image.FileName, request.Image.Length);

        // Bước 2: Upload ảnh CCCD lên VNPT → nhận hash
        var fileHash = await UploadFileAsync(request.Image, "OCR", cancellationToken);

        // Bước 3: Gọi VNPT OCR endpoint
        // Theo tài liệu VNPT API #6:
        //   type: -1 = CMND/CCCD, 5 = Hộ chiếu, 6 = Bằng lái xe, 7 = CM quân đội
        //   crop_param: bắt buộc, tỉ lệ crop ảnh (VD: "0,0" = không crop)
        var ocrRequestBody = new
        {
            img_front      = fileHash,
            img_back       = (string?)null,  // Không yêu cầu ảnh mặt sau
            client_session = Guid.NewGuid().ToString(),
            type           = -1,             // -1 = CMND cũ/mới, CCCD
            crop_param     = "0,0",          // Không crop ảnh
            token          = Guid.NewGuid().ToString()
        };
        var rawResponse = await PostJsonToVnptAsync<VnptOcrRawResponse>(
            endpoint:      _options.OcrEndpoint,
            requestBody:   ocrRequestBody,
            operationName: "OCR",
            cancellationToken: cancellationToken);

        // Bước 4: Kiểm tra response
        if (!IsSuccessMessage(rawResponse.Message))
        {
            _logger.LogWarning(
                "VNPT OCR trả về lỗi: message='{Message}'.",
                rawResponse.Message);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeApiError,
                $"VNPT OCR API lỗi: {rawResponse.Message}");
        }

        // Bước 5: Map raw response → application DTO
        _logger.LogInformation(
            "VNPT OCR thành công. CCCD ID='{Id}', Họ tên='{Name}'.",
            rawResponse.Object?.Id, rawResponse.Object?.Name);

        return MapToOcrResponse(rawResponse);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Face Match — So khớp khuôn mặt
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public async Task<FaceMatchResponse> MatchFaceAsync(
        FaceMatchRequest  request,
        CancellationToken cancellationToken = default)
    {
        // Bước 1: Validate cả hai file ảnh
        await _fileValidator.ValidateAsync(request.FaceImage,   nameof(request.FaceImage));
        await _fileValidator.ValidateAsync(request.IdCardImage, nameof(request.IdCardImage));

        _logger.LogInformation(
            "VNPT FaceCompare: selfie='{FaceName}', CCCD='{IdName}'.",
            request.FaceImage.FileName, request.IdCardImage.FileName);

        // Bước 2: Upload cả hai ảnh → nhận 2 hash
        var hashFace = await UploadFileAsync(request.FaceImage,   "FaceCompare-Selfie", cancellationToken);
        var hashId   = await UploadFileAsync(request.IdCardImage, "FaceCompare-CCCD",   cancellationToken);

        // Bước 3: Gọi VNPT Face Compare endpoint
        // Theo tài liệu VNPT API #7: img_front = hash ảnh CCCD, img_face = hash ảnh selfie
        var compareRequestBody = new
        {
            img_front      = hashId,
            img_face       = hashFace,
            client_session = Guid.NewGuid().ToString(),
            token          = Guid.NewGuid().ToString()
        };

        var rawResponse = await PostJsonToVnptAsync<VnptFaceCompareRawResponse>(
            endpoint:      _options.FaceCompareEndpoint,
            requestBody:   compareRequestBody,
            operationName: "FaceCompare",
            cancellationToken: cancellationToken);

        // Bước 4: Map raw response → application DTO
        var result = MapToFaceMatchResponse(rawResponse);

        _logger.LogInformation(
            "VNPT FaceCompare hoàn tất. IsMatch={IsMatch}, Similarity={Similarity:F2}%.",
            result.IsMatch, result.Similarity);

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Liveness Detection — Đã loại bỏ (VNPT yêu cầu SDK)
    // ═══════════════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">
    /// VNPT Liveness Detection yêu cầu tích hợp SDK phía client.
    /// REST API backend không hỗ trợ tính năng này.
    /// </exception>
    public Task<LivenessDetectionResponse> DetectLivenessAsync(
        LivenessDetectionRequest request,
        CancellationToken        cancellationToken = default)
    {
        throw new NotSupportedException(
            "Liveness Detection không được hỗ trợ qua VNPT REST API. " +
            "Tính năng này yêu cầu tích hợp VNPT eKYC SDK phía client (Mobile/Web).");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Private: Upload file lên VNPT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Upload file ảnh lên VNPT <c>/file-service/v1/addFile</c> và trả về <c>hash</c>.
    /// Hash này được dùng làm tham số cho các API service tiếp theo (OCR, FaceCompare).
    /// </summary>
    private async Task<string> UploadFileAsync(
        IFormFile         file,
        string            operationName,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "VNPT Upload ({Op}): Đang upload '{FileName}' ({Size} bytes).",
            operationName, file.FileName, file.Length);

        // Xây dựng multipart/form-data
        using var formData = new MultipartFormDataContent();
        var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        var streamContent = new StreamContent(memoryStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        formData.Add(streamContent, "file", file.FileName);

        // VNPT Upload API yêu cầu thêm title và description (theo tài liệu API #1)
        formData.Add(new StringContent(operationName), "title");
        formData.Add(new StringContent($"eKYC {operationName} upload"), "description");

        // Gọi upload endpoint
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await httpClient.PostAsync(
                _options.UploadEndpoint, formData, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "VNPT Upload ({Op}) timeout sau {Timeout}s.",
                operationName, _options.TimeoutSeconds);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeTimeout,
                $"VNPT Upload {operationName} timeout sau {_options.TimeoutSeconds} giây.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không thể kết nối tới VNPT Upload API ({Op}).", operationName);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeHttpError,
                $"Không thể kết nối tới VNPT Upload API: {ex.Message}",
                ex);
        }

        // Kiểm tra token hết hạn (HTTP 401)
        CheckForExpiredToken(httpResponse, $"Upload-{operationName}");

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "VNPT Upload ({Op}) trả về HTTP {StatusCode}. Body: {Body}",
                operationName, (int)httpResponse.StatusCode, body);

            throw new EKycIntegrationException(
                (int)httpResponse.StatusCode,
                $"VNPT Upload API trả về HTTP {(int)httpResponse.StatusCode}: {body}");
        }

        // Parse response để lấy hash
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        VnptUploadResponse? uploadResult;
        try
        {
            uploadResult = JsonSerializer.Deserialize<VnptUploadResponse>(responseBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Không parse được JSON response từ VNPT Upload ({Op}). Body: {Body}",
                operationName, responseBody);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeInvalidResponse,
                $"Không parse được JSON response từ VNPT Upload API.",
                ex);
        }

        var hash = uploadResult?.Object?.Hash;
        if (string.IsNullOrWhiteSpace(hash))
        {
            _logger.LogError(
                "VNPT Upload ({Op}) trả về response nhưng không có hash. Body: {Body}",
                operationName, responseBody);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeInvalidResponse,
                "VNPT Upload API trả về response nhưng không chứa file hash.");
        }

        _logger.LogDebug("VNPT Upload ({Op}) thành công. Hash='{Hash}'.", operationName, hash);
        return hash;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Private: POST JSON tới VNPT AI service
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gọi VNPT AI service endpoint bằng POST application/json.
    /// Xử lý tập trung: timeout, HTTP errors, token expired, JSON parse.
    /// </summary>
    private async Task<TResponse> PostJsonToVnptAsync<TResponse>(
        string            endpoint,
        object            requestBody,
        string            operationName,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestBody, JsonOptions),
            Encoding.UTF8,
            "application/json");

        // VNPT OCR/FaceCompare yêu cầu header mac-address (theo tài liệu API #6, #7)
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = jsonContent
        };
        request.Headers.Add("mac-address", _options.MacAddress);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "VNPT {Op} API timeout sau {Timeout}s.",
                operationName, _options.TimeoutSeconds);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeTimeout,
                $"VNPT {operationName} API timeout sau {_options.TimeoutSeconds} giây.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không thể kết nối tới VNPT {Op} API.", operationName);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeHttpError,
                $"Không thể kết nối tới VNPT {operationName} API: {ex.Message}",
                ex);
        }

        // Kiểm tra token hết hạn (HTTP 401)
        CheckForExpiredToken(httpResponse, operationName);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "VNPT {Op} API trả về HTTP {StatusCode}. Body: {Body}",
                operationName, (int)httpResponse.StatusCode, body);

            throw new EKycIntegrationException(
                (int)httpResponse.StatusCode,
                $"VNPT {operationName} API trả về HTTP {(int)httpResponse.StatusCode}: {body}");
        }

        // Deserialize JSON response
        var responseBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions);
            if (result is null)
                throw new EKycIntegrationException(
                    EKycIntegrationException.CodeInvalidResponse,
                    $"VNPT {operationName} API trả về response rỗng hoặc null.");

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Không parse được JSON response từ VNPT {Op} API. Body: {Body}",
                operationName, responseBody);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeInvalidResponse,
                $"Không parse được JSON response từ VNPT {operationName} API.",
                ex);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Private: Token expired detection
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kiểm tra HTTP 401 Unauthorized — dấu hiệu AccessToken đã hết hạn (8 tiếng).
    /// Throw exception với thông báo rõ ràng để admin biết cần làm gì.
    /// </summary>
    private void CheckForExpiredToken(HttpResponseMessage response, string operationName)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogError(
                "VNPT {Op}: HTTP 401 Unauthorized — AccessToken có thể đã hết hạn. " +
                "Vào Dashboard VNPT (ekyc.vnpt.vn) → Quản lý Token → sao chép Access Token mới " +
                "→ cập nhật VnptEKyc__AccessToken trong file .env → restart app.",
                operationName);

            throw new EKycIntegrationException(
                EKycIntegrationException.CodeTokenExpired,
                "AccessToken VNPT đã hết hạn (thời hạn 8 tiếng). " +
                "Vui lòng vào Dashboard VNPT (ekyc.vnpt.vn) → Quản lý Token → " +
                "sao chép Access Token mới → cập nhật VnptEKyc__AccessToken trong .env → restart app.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Private: Response mapping
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Kiểm tra message code thành công từ VNPT (IDG-00000000 hoặc chứa "success").</summary>
    private static bool IsSuccessMessage(string? message)
        => !string.IsNullOrWhiteSpace(message)
           && (message.Contains("IDG-00000000", StringComparison.OrdinalIgnoreCase)
               || message.Contains("success", StringComparison.OrdinalIgnoreCase));

    /// <summary>Map VNPT OCR response → application DTO.</summary>
    private static OcrIdCardResponse MapToOcrResponse(VnptOcrRawResponse raw)
    {
        var obj = raw.Object;
        return new OcrIdCardResponse
        {
            ErrorCode    = IsSuccessMessage(raw.Message) ? 0 : -1,
            ErrorMessage = raw.Message ?? string.Empty,
            Data         = obj is null ? null : new OcrIdCardData
            {
                Id           = obj.Id              ?? string.Empty,
                Name         = obj.Name            ?? string.Empty,
                Dob          = obj.BirthDay         ?? string.Empty,
                Sex          = obj.Gender           ?? string.Empty,
                Nationality  = obj.Nationality      ?? string.Empty,
                Home         = obj.OriginLocation   ?? string.Empty,
                Address      = obj.RecentLocation   ?? string.Empty,
                Doe          = obj.ValidDate         ?? string.Empty,
                IssueDate    = obj.IssueDate         ?? string.Empty,
                IssueLoc     = obj.IssuePlace        ?? string.Empty,
                Type         = obj.CardType          ?? string.Empty,
                OverallScore = obj.NameProb ?? 1.0  // Dùng name_prob như confidence proxy
            }
        };
    }

    /// <summary>Map VNPT Face Compare response → application DTO.</summary>
    private static FaceMatchResponse MapToFaceMatchResponse(VnptFaceCompareRawResponse raw)
    {
        // VNPT trả về: object.prob = % similarity, object.msg = "MATCH" hoặc "NOMATCH"
        var similarity = raw.Object?.Prob ?? 0;
        var isMatch    = string.Equals(raw.Object?.Msg, "MATCH", StringComparison.OrdinalIgnoreCase);

        return new FaceMatchResponse
        {
            Code            = string.Empty,
            IsMatch         = isMatch,
            Similarity      = similarity,
            IsBothImgIdCard = false,
            ProviderMessage = raw.Object?.Result ?? raw.Message ?? string.Empty
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Private: Raw JSON models (VNPT response)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Response từ VNPT Upload API (/file-service/v1/addFile).</summary>
    private sealed record VnptUploadResponse
    {
        [JsonPropertyName("message")] public string?          Message { get; init; }
        [JsonPropertyName("object")]  public VnptUploadObject? Object { get; init; }
    }

    private sealed record VnptUploadObject
    {
        [JsonPropertyName("hash")]         public string? Hash         { get; init; }
        [JsonPropertyName("fileName")]     public string? FileName     { get; init; }
        [JsonPropertyName("title")]        public string? Title        { get; init; }
        [JsonPropertyName("description")]  public string? Description  { get; init; }
        [JsonPropertyName("tokenId")]      public string? TokenId      { get; init; }
        [JsonPropertyName("fileType")]     public string? FileType     { get; init; }
        [JsonPropertyName("uploadedDate")] public string? UploadedDate { get; init; }
        [JsonPropertyName("storageType")]  public string? StorageType  { get; init; }
    }

    /// <summary>
    /// Response từ VNPT OCR API (/ai/v1/ocr/id).
    /// Field names theo tài liệu VNPT: id, name, birth_day, gender,
    /// nationality, origin_location, recent_location, valid_date,
    /// issue_date, issue_place, card_type, etc.
    /// </summary>
    private sealed record VnptOcrRawResponse
    {
        [JsonPropertyName("message")] public string?        Message { get; init; }
        [JsonPropertyName("object")]  public VnptOcrObject? Object  { get; init; }
    }

    private sealed record VnptOcrObject
    {
        // ── Thông tin chính ────────────────────────────────────────
        [JsonPropertyName("id")]                public string? Id              { get; init; }
        [JsonPropertyName("name")]              public string? Name            { get; init; }
        [JsonPropertyName("birth_day")]         public string? BirthDay        { get; init; }
        [JsonPropertyName("gender")]            public string? Gender          { get; init; }
        [JsonPropertyName("nationality")]       public string? Nationality     { get; init; }
        [JsonPropertyName("origin_location")]   public string? OriginLocation  { get; init; }
        [JsonPropertyName("recent_location")]   public string? RecentLocation  { get; init; }
        [JsonPropertyName("valid_date")]        public string? ValidDate       { get; init; }
        [JsonPropertyName("issue_date")]        public string? IssueDate       { get; init; }
        [JsonPropertyName("issue_place")]       public string? IssuePlace      { get; init; }
        [JsonPropertyName("card_type")]         public string? CardType        { get; init; }
        [JsonPropertyName("citizen_id")]        public string? CitizenId       { get; init; }

        // ── Label fields ───────────────────────────────────────────
        [JsonPropertyName("name_label")]             public string? NameLabel            { get; init; }
        [JsonPropertyName("birth_day_label")]         public string? BirthDayLabel        { get; init; }
        [JsonPropertyName("origin_location_label")]   public string? OriginLocationLabel  { get; init; }
        [JsonPropertyName("recent_location_label")]   public string? RecentLocationLabel  { get; init; }

        // ── Probability / confidence scores ────────────────────────
        [JsonPropertyName("name_prob")]              public double? NameProb           { get; init; }
        [JsonPropertyName("birth_day_prob")]          public double? BirthDayProb       { get; init; }
        [JsonPropertyName("origin_location_prob")]    public double? OriginLocationProb { get; init; }
        [JsonPropertyName("recent_location_prob")]    public double? RecentLocationProb { get; init; }
        [JsonPropertyName("valid_date_prob")]         public double? ValidDateProb      { get; init; }
        [JsonPropertyName("issue_date_prob")]         public double? IssueDateProb      { get; init; }
        [JsonPropertyName("issue_place_prob")]        public double? IssuePlaceProb     { get; init; }
        [JsonPropertyName("gender_prob")]             public double? GenderProb         { get; init; }
        [JsonPropertyName("nationality_prob")]        public double? NationalityProb    { get; init; }
        [JsonPropertyName("citizen_id_prob")]         public double? CitizenIdProb      { get; init; }
        [JsonPropertyName("id_fake_prob")]            public double? IdFakeProb         { get; init; }

        // ── Metadata ───────────────────────────────────────────────
        [JsonPropertyName("type_id")]            public int?    TypeId          { get; init; }
        [JsonPropertyName("back_type_id")]       public int?    BackTypeId      { get; init; }
        [JsonPropertyName("id_probs")]           public string? IdProbs         { get; init; }
        [JsonPropertyName("id_fake_warning")]    public string? IdFakeWarning   { get; init; }
        [JsonPropertyName("expire_warning")]     public string? ExpireWarning   { get; init; }
        [JsonPropertyName("back_expire_warning")] public string? BackExpireWarning { get; init; }
        [JsonPropertyName("msg")]                public string? Msg             { get; init; }
        [JsonPropertyName("msg_back")]           public string? MsgBack         { get; init; }
        [JsonPropertyName("nation_policy")]      public string? NationPolicy    { get; init; }
        [JsonPropertyName("nation_slogan")]      public string? NationSlogan    { get; init; }
    }

    /// <summary>
    /// Response từ VNPT Face Compare API (/ai/v1/face/compare).
    /// Field names: object.result, object.msg ("MATCH"/"NOMATCH"), object.prob (%).
    /// </summary>
    private sealed record VnptFaceCompareRawResponse
    {
        [JsonPropertyName("message")]        public string?                Message       { get; init; }
        [JsonPropertyName("server_version")] public string?                ServerVersion { get; init; }
        [JsonPropertyName("object")]         public VnptFaceCompareObject? Object        { get; init; }
    }

    private sealed record VnptFaceCompareObject
    {
        /// <summary>Text mô tả kết quả (VD: "Khuôn mặt khớp 99,7%").</summary>
        [JsonPropertyName("result")] public string? Result { get; init; }

        /// <summary>"MATCH" hoặc "NOMATCH".</summary>
        [JsonPropertyName("msg")]    public string? Msg    { get; init; }

        /// <summary>Phần trăm similarity (VD: 58.26).</summary>
        [JsonPropertyName("prob")]   public double? Prob   { get; init; }
    }
}
