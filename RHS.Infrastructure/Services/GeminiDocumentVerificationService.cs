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
using RHS.Domain.Entities;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Services;

public class GeminiDocumentVerificationService : IDocumentVerificationService
{
    private readonly HttpClient _geminiClient;
    private readonly GeminiAiOptions _options;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<GeminiDocumentVerificationService> _logger;

    public GeminiDocumentVerificationService(
        HttpClient geminiClient,
        IOptions<GeminiAiOptions> options,
        AppDbContext dbContext,
        ILogger<GeminiDocumentVerificationService> logger)
    {
        _geminiClient = geminiClient;
        _options = options.Value;
        _dbContext = dbContext;
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

        // 1. Load ApplicationDocument kèm theo User profile
        var document = await _dbContext.ApplicationDocuments
            .Include(d => d.UploadedByUser)
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
            // 2. Tải file PDF từ FileUrl (Cloudinary)
            byte[] pdfBytes;
            using (var downloadClient = new HttpClient())
            {
                downloadClient.Timeout = TimeSpan.FromSeconds(30);
                // Thêm User-Agent giả lập trình duyệt để tránh bị chặn bởi Cloudinary/WAF
                downloadClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                pdfBytes = await downloadClient.GetByteArrayAsync(document.FileUrl, cancellationToken);
            }

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                throw new InvalidOperationException("Không thể tải file PDF hoặc file trống");
            }

            string base64Pdf = Convert.ToBase64String(pdfBytes);

            // 3. Chuẩn bị prompt đối chiếu thông tin
            string userDobFormatted = user.DateOfBirth?.ToString("yyyy-MM-dd") ?? "Không có thông tin";
            string prompt = $@"
Đọc tài liệu PDF đính kèm (là Giấy xác nhận điều kiện nhà ở hoặc Giấy chứng nhận hộ nghèo). 
Hãy trích xuất các thông tin sau từ tài liệu và đối chiếu chúng với thông tin Profile của User:

[Thông tin Profile của User]
- Họ và tên (FullName): {user.FullName}
- Số CCCD/CMND (CitizenId): {user.CitizenId ?? "Không có thông tin"}
- Ngày sinh (DateOfBirth): {userDobFormatted}
- Địa chỉ (Address): {user.Address ?? "Không có thông tin"}

[Quy tắc so khớp]:
1. Số CCCD/CMND (citizenIdMatch): Yêu cầu khớp chính xác tuyệt đối (độ dài 9 hoặc 12 số). Nếu trên tài liệu không có CCCD hoặc bị sai lệch số thì coi là không khớp.
2. Ngày sinh (dateOfBirthMatch): Yêu cầu khớp chính xác ngày, tháng, năm. Định dạng trên tài liệu có thể là dd/MM/yyyy hoặc dd-MM-yyyy, hãy chuẩn hóa về yyyy-MM-dd để so sánh.
3. Địa chỉ (addressMatch): Chấp nhận các lỗi viết tắt hoặc định dạng địa chỉ thông thường (ví dụ: 'Q.' = 'Quận', 'TP. HCM' = 'Thành phố Hồ Chí Minh', 'P.' = 'Phường'). Nếu địa chỉ trên giấy tờ khớp phần lớn hoặc chỉ khác cách viết tắt thì vẫn coi là khớp.
4. Họ tên (fullNameMatch): Chấp nhận viết tắt nhẹ hoặc không dấu.

Kết quả tổng quan 'isMatch' sẽ là true nếu cả 3 thông tin quan trọng (CCCD, Ngày sinh, Địa chỉ) đều trùng khớp với Profile của User. 
Nếu có bất kỳ thông tin nào trong 3 thông tin này bị lệch (mismatch) hoặc thiếu (missing - không trích xuất được từ giấy tờ), đặt 'isMatch' là false và ghi rõ chi tiết trường nào bị thiếu/sai lệch và nội dung sai lệch vào 'mismatchDetails' bằng tiếng Việt để báo lại cho người dùng sửa.
";

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
