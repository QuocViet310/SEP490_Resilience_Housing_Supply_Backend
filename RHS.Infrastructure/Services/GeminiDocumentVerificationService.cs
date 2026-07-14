using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RHS.Application.DTOs.DocumentVerification;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class GeminiDocumentVerificationService : IDocumentVerificationService
{
    private readonly HttpClient _geminiClient;
    private readonly GeminiAiOptions _options;
    private readonly AppDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<GeminiDocumentVerificationService> _logger;

    public GeminiDocumentVerificationService(
        HttpClient geminiClient,
        IOptions<GeminiAiOptions> options,
        AppDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<GeminiDocumentVerificationService> logger)
    {
        _geminiClient = geminiClient;
        _options = options.Value;
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _logger = logger;

        // Cấu hình HttpClient cho Gemini
        if (!string.IsNullOrEmpty(_options.ApiUrl))
        {
            _geminiClient.BaseAddress = new Uri(_options.ApiUrl.TrimEnd('/') + "/");
        }
        _geminiClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<DocumentVerificationResultDto> VerifyDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Bắt đầu AI verification cho tài liệu {DocumentId}", documentId);

        // 1. Load ApplicationDocument kèm User + hồ sơ (để biết DocumentType / HousingStatus)
        var document = await _dbContext.ApplicationDocuments
            .Include(d => d.UploadedByUser)
            .Include(d => d.HousingApplication)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId, cancellationToken);

        if (document == null)
        {
            _logger.LogError("Không tìm thấy tài liệu {DocumentId}", documentId);
            throw new ArgumentException($"Không tìm thấy tài liệu với ID {documentId}");
        }

        var user = document.UploadedByUser;
        if (user == null)
        {
            _logger.LogError("Tài liệu {DocumentId} không có thông tin người dùng upload", documentId);
            throw new InvalidOperationException("Không tìm thấy thông tin profile của User upload tài liệu");
        }

        // Tạo sẵn DTO kết quả lỗi mặc định
        var resultDto = new DocumentVerificationResultDto
        {
            VerificationId = Guid.NewGuid(),
            DocumentId = documentId,
            ValidationResult = "ERROR",
            VerifiedAt = DateTime.UtcNow
        };

        try
        {
            // 2. Tải file PDF từ FileUrl (sử dụng FileStorageService hỗ trợ Signed URL)
            byte[] pdfBytes = await _fileStorageService.DownloadFileAsync(document.FileUrl);

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                throw new InvalidOperationException("Không thể tải file PDF hoặc file trống");
            }

            string base64Pdf = Convert.ToBase64String(pdfBytes);

            // 3. Chuẩn bị prompt đối chiếu theo loại giấy tờ + thực trạng nhà ở đã khai
            string userDobFormatted = user.DateOfBirth?.ToString("yyyy-MM-dd") ?? "Không có thông tin";
            var housingStatus = document.HousingApplication?.HousingStatus ?? "";
            var priorityGroup = document.HousingApplication?.PriorityGroup ?? "";
            var averageArea = document.HousingApplication?.AverageHousingAreaPerPerson;
            string prompt = BuildVerificationPrompt(
                document.DocumentType,
                housingStatus,
                priorityGroup,
                averageArea,
                user.FullName ?? "",
                user.CitizenId,
                userDobFormatted,
                user.Address);

            // 4. Xây dựng payload request gửi sang Gemini API
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = "application/pdf",
                                    data = base64Pdf
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            isMatch = new { type = "BOOLEAN" },
                            mismatchDetails = new { type = "STRING", description = "Mô tả chi tiết những thông tin bị lệch hoặc thiếu so với Profile bằng tiếng Việt để báo lại cho user sửa. Ví dụ: 'Số CCCD trên tài liệu (123456789012) không khớp với profile (987654321098)' hoặc 'Không tìm thấy thông tin Ngày sinh trên giấy tờ'." },
                            extractedFullName = new { type = "STRING", description = "Họ tên đầy đủ trích xuất được từ giấy tờ" },
                            extractedCitizenId = new { type = "STRING", description = "Số CCCD/CMND trích xuất được từ giấy tờ" },
                            extractedAddress = new { type = "STRING", description = "Địa chỉ thường trú hoặc nơi ở trích xuất được từ giấy tờ" },
                            extractedDateOfBirth = new { type = "STRING", description = "Ngày sinh trích xuất được dưới định dạng yyyy-MM-dd" }
                        },
                        required = new[] { "isMatch", "mismatchDetails" }
                    }
                }
            };

            // 5. Gọi Gemini API
            string geminiUrl = $"models/{_options.ModelName}:generateContent?key={_options.ApiKey}";
            var response = await _geminiClient.PostAsJsonAsync(geminiUrl, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gọi Gemini API thất bại. HTTP Status: {Status}, Content: {Content}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Gemini API error: {response.StatusCode} - {errorContent}");
            }

            var geminiResponseDto = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
            string? jsonText = geminiResponseDto?.Candidates?[0]?.Content?.Parts?[0]?.Text;

            if (string.IsNullOrEmpty(jsonText))
            {
                throw new InvalidOperationException("Không nhận được kết quả phân tích từ Gemini API");
            }

            // Parse kết quả JSON do Gemini trả về
            var parseOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var geminiResult = JsonSerializer.Deserialize<GeminiAnalysisResult>(jsonText, parseOptions);

            if (geminiResult == null)
            {
                throw new InvalidOperationException("Không thể parse kết quả JSON trả về từ Gemini");
            }

            // 6. Cập nhật kết quả & trạng thái tài liệu
            bool finalMatch = geminiResult.IsMatch;
            
            resultDto.ValidationResult = finalMatch ? "MATCH" : "MISMATCH";
            resultDto.ErrorDetails = finalMatch ? null : geminiResult.MismatchDetails;
            resultDto.ExtractedFullName = geminiResult.ExtractedFullName;
            resultDto.ExtractedCitizenId = geminiResult.ExtractedCitizenId;
            resultDto.ExtractedAddress = geminiResult.ExtractedAddress;
            resultDto.ExtractedDateOfBirth = geminiResult.ExtractedDateOfBirth;

            // Cập nhật trạng thái tài liệu: Khớp -> VERIFIED, Lệch -> REJECTED
            document.VerificationStatus = finalMatch ? "VERIFIED" : "REJECTED";
            _dbContext.Entry(document).State = EntityState.Modified;

            _logger.LogInformation("AI Verification hoàn tất cho {DocumentId}. Kết quả: {Result}, Trạng thái tài liệu cập nhật thành: {Status}", 
                documentId, resultDto.ValidationResult, document.VerificationStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình AI Verification cho tài liệu {DocumentId}", documentId);
            resultDto.ValidationResult = "ERROR";
            resultDto.ErrorDetails = "Lỗi hệ thống khi phân tích tài liệu: " + ex.Message;
            
            // Nếu có lỗi hệ thống, đặt trạng thái tài liệu là REJECTED để chặn lỗi và yêu cầu kiểm tra lại
            document.VerificationStatus = "REJECTED";
            _dbContext.Entry(document).State = EntityState.Modified;
        }

        // 7. Lưu AIVerificationResult vào Database
        var aiResultEntity = new AIVerificationResult
        {
            VerificationId = resultDto.VerificationId,
            DocumentId = resultDto.DocumentId,
            ExtractedText = $"Full Name: {resultDto.ExtractedFullName} | Citizen ID: {resultDto.ExtractedCitizenId} | Address: {resultDto.ExtractedAddress} | DOB: {resultDto.ExtractedDateOfBirth}",
            FaceMatchScore = 0,
            RiskScore = 0,
            ValidationResult = resultDto.ValidationResult,
            VerifiedAt = resultDto.VerifiedAt,
            ExtractedFullName = resultDto.ExtractedFullName,
            ExtractedCitizenId = resultDto.ExtractedCitizenId,
            ExtractedAddress = resultDto.ExtractedAddress,
            ExtractedDateOfBirth = resultDto.ExtractedDateOfBirth,
            ErrorDetails = resultDto.ErrorDetails,
            AiModelUsed = _options.ModelName
        };

        // Xóa kết quả xác minh cũ của tài liệu này (nếu có) để tránh lỗi trùng lặp One-to-One
        var existingResult = await _dbContext.AIVerificationResults
            .FirstOrDefaultAsync(r => r.DocumentId == documentId, cancellationToken);
        if (existingResult != null)
        {
            _dbContext.AIVerificationResults.Remove(existingResult);
        }

        await _dbContext.AIVerificationResults.AddAsync(aiResultEntity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return resultDto;
    }

    private static string BuildVerificationPrompt(
        string documentType,
        string housingStatus,
        string priorityGroup,
        decimal? averageAreaPerPerson,
        string fullName,
        string? citizenId,
        string dateOfBirth,
        string? address)
    {
        var profileBlock = $@"
[Thông tin Profile của User — đã eKYC]
- Họ và tên (FullName): {fullName}
- Số CCCD/CMND (CitizenId): {citizenId ?? "Không có thông tin"}
- Ngày sinh (DateOfBirth): {dateOfBirth}
- Địa chỉ (Address): {address ?? "Không có thông tin"}
";

        var identityRules = @"
[Quy tắc so khớp danh tính]
1. CCCD/CMND: khớp chính xác tuyệt đối (9 hoặc 12 số). Thiếu hoặc sai → không khớp.
2. Ngày sinh: khớp ngày/tháng/năm (chuẩn hóa về yyyy-MM-dd).
3. Địa chỉ: chấp nhận viết tắt thông thường (Q./Quận, P./Phường, TP./Thành phố...).
4. Họ tên: chấp nhận không dấu / viết tắt nhẹ.

isMatch = true chỉ khi CCCD + Ngày sinh + Địa chỉ đều khớp (hoặc giấy không ghi DOB thì CCCD + Địa chỉ + Họ tên phải khớp).
Nếu lệch/thiếu: isMatch = false và ghi rõ bằng tiếng Việt vào mismatchDetails.
";

        if (string.Equals(documentType, DocumentTypeConstants.PovertyHouseholdCertificate, StringComparison.OrdinalIgnoreCase))
        {
            var expectedPoverty = priorityGroup switch
            {
                PriorityGroupConstants.UrbanPoor => "hộ nghèo (đô thị)",
                PriorityGroupConstants.UrbanNearPoor => "hộ cận nghèo (đô thị)",
                _ => "hộ nghèo hoặc hộ cận nghèo"
            };

            return $@"
Đọc PDF đính kèm. Đây phải là GIẤY CHỨNG NHẬN HỘ NGHÈO / HỘ CẬN NGHÈO do địa phương cấp.
Người nộp đã khai đối tượng: {expectedPoverty}.

Nhiệm vụ:
1) Xác nhận giấy có nội dung chứng nhận hộ nghèo hoặc hộ cận nghèo (không phải giấy tờ khác).
2) Trích xuất họ tên, CCCD, ngày sinh, địa chỉ (nếu có trên giấy).
3) Đối chiếu danh tính với Profile User bên dưới.
4) Nếu giấy không phải chứng nhận hộ nghèo/cận nghèo, hoặc nội dung mâu thuẫn đối tượng đã khai → isMatch = false.

{profileBlock}
{identityRules}
";
        }

        // HOUSING_CONDITION_PROOF
        var housingExpectation = housingStatus switch
        {
            HousingStatusConstants.NoHouse =>
                "Người nộp khai CHƯA CÓ nhà ở thuộc sở hữu (NO_HOUSE). Giấy phải xác nhận chưa có nhà/không có nhà thuộc sở hữu.",
            HousingStatusConstants.SmallHouse =>
                $"Người nộp khai CÓ nhà nhưng diện tích bình quân < 15 m²/người (SMALL_HOUSE). " +
                $"Diện tích đã khai trên hồ sơ: {(averageAreaPerPerson.HasValue ? $"{averageAreaPerPerson.Value:0.##} m²/người" : "chưa ghi số")}. " +
                "Giấy phải xác nhận còn nhà với diện tích bình quân đầu người dưới 15 m².",
            _ => "Giấy xác nhận nhà ở theo Đ29: chưa có nhà, hoặc có nhà dưới 15 m²/người."
        };

        return $@"
Đọc PDF đính kèm. Đây phải là GIẤY XÁC NHẬN NHÀ Ở / xác nhận thực trạng nhà ở.
{housingExpectation}

Nhiệm vụ:
1) Xác nhận đúng loại giấy xác nhận nhà ở (không nhầm với giấy hộ nghèo).
2) Kiểm tra nội dung giấy có khớp với thực trạng đã khai ở trên (chưa có nhà HOẶC có nhà < 15 m²/người).
3) Trích xuất họ tên, CCCD, ngày sinh, địa chỉ (nếu có).
4) Đối chiếu danh tính với Profile User.
5) Nếu loại giấy sai, hoặc nội dung mâu thuẫn thực trạng đã khai → isMatch = false.

{profileBlock}
{identityRules}
";
    }

    #region Helper Classes for API Parsing
    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public Candidate[]? Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public Part[]? Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class GeminiAnalysisResult
    {
        [JsonPropertyName("isMatch")]
        public bool IsMatch { get; set; }

        [JsonPropertyName("mismatchDetails")]
        public string? MismatchDetails { get; set; }

        [JsonPropertyName("extractedFullName")]
        public string? ExtractedFullName { get; set; }

        [JsonPropertyName("extractedCitizenId")]
        public string? ExtractedCitizenId { get; set; }

        [JsonPropertyName("extractedAddress")]
        public string? ExtractedAddress { get; set; }

        [JsonPropertyName("extractedDateOfBirth")]
        public string? ExtractedDateOfBirth { get; set; }
    }
    #endregion
}
