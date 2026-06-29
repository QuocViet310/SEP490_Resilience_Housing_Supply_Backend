# Tích hợp Gemini API — Quét PDF xác minh thông tin người dùng

## Bối cảnh

Dự án Resilience Housing Supply cần tự động quét các file PDF mà người dùng upload lên (`HOUSING_CONDITION_PROOF` - Giấy xác nhận điều kiện nhà ở, hoặc `POVERTY_HOUSEHOLD_CERTIFICATE` - Giấy chứng nhận hộ nghèo) để trích xuất thông tin (CCCD, ngày sinh, địa chỉ, họ tên) và so sánh trực tiếp với profile của người dùng trong hệ thống.

Để triển khai nhanh chóng, tiết kiệm chi phí và **không yêu cầu thẻ Visa để đăng ký**, dự án sẽ sử dụng **Gemini 1.5 Flash API (Google AI Studio)** thay thế cho Azure AI Document Intelligence. Gemini 1.5 Flash hỗ trợ đầu vào dạng PDF trực tiếp (Multimodal) và có khả năng đọc hiểu tiếng Việt cực kỳ tốt, tự động so khớp thông tin và trả về kết quả dạng cấu trúc JSON định sẵn mà không cần viết các hàm Regex hay Fuzzy matching phức tạp ở Backend.

### Các thông tin cần so khớp:
Hệ thống sẽ gửi file PDF cùng thông tin profile của User sang Gemini để đối chiếu:
* `CitizenId` (Số CCCD/CMND) - Yêu cầu khớp chính xác.
* `DateOfBirth` (Ngày sinh) - Yêu cầu khớp chính xác.
* `PermanentAddress` / `CurrentResidence` (Địa chỉ thường trú / Nơi ở hiện tại) - Chấp nhận sai lệch viết tắt thông dụng (ví dụ: "Q.10" và "Quận 10", "TP.HCM" và "Thành phố Hồ Chí Minh").
* `FullName` (Họ và tên) - Chấp nhận sai lệch viết tắt hoặc không dấu ở mức độ nhẹ.

---

## Luồng Nghiệp Vụ Xác Minh AI

1. **Upload tài liệu**: Người dùng upload tài liệu PDF thành công → Hệ thống tự động trigger gọi API xác minh async/background (hoặc sync tùy chọn).
2. **So khớp thông tin qua Gemini**:
   * Gửi file PDF dưới dạng base64 (MimeType: `application/pdf`) kèm theo thông tin Profile của User hiện tại qua prompt của Gemini 1.5 Flash.
   * Yêu cầu Gemini phân tích, so sánh các trường dữ liệu và trả về kết quả định dạng JSON chuẩn.
3. **Xử lý kết quả trả về từ Gemini**:
   * **Trường hợp KHỚP hoàn toàn (MATCH)**:
     * Cập nhật `VerificationStatus` của tài liệu (`ApplicationDocument`) thành `"VERIFIED"`.
     * Cho phép hồ sơ chuyển sang bước tiếp theo cho Officer kiểm tra thủ công các giấy tờ khác như luồng cũ.
   * **Trường hợp KHÔNG KHỚP hoặc THIẾU thông tin (MISMATCH / MISSING)**:
     * Cập nhật `VerificationStatus` của tài liệu thành `"REJECTED"`.
     * Lưu chi tiết lý do lệch thông tin (ví dụ: *"Số CCCD không trùng khớp với profile"*, *"Không tìm thấy thông tin Ngày sinh trên giấy xác nhận"*) vào bảng `AIVerificationResults` để hiển thị/báo lại cho User sửa.
     * Chặn luồng xử lý, không chuyển tiếp hồ sơ này cho Officer duyệt.

---

## Proposed Changes

### Component 1: Cấu hình Gemini API

> Cấu hình options để kết nối với Google AI Studio (Gemini API) sử dụng `HttpClient` thuần của .NET để tối giản dependency.

#### [NEW] [GeminiAiOptions.cs](file:///d:/SEP490_Resilience_Housing_Supply_Backend/RHS.Infrastructure/Configurations/GeminiAiOptions.cs)
```csharp
namespace RHS.Infrastructure.Configurations;

public sealed class GeminiAiOptions
{
    public const string SectionName = "GeminiAi";

    /// <summary>API Key lấy từ Google AI Studio</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Model sử dụng. Mặc định: gemini-1.5-flash</summary>
    public string ModelName { get; init; } = "gemini-1.5-flash";

    /// <summary>Endpoint API của Gemini</summary>
    public string ApiUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta";

    /// <summary>Timeout (giây) cho mỗi request. Mặc định: 30s</summary>
    public int TimeoutSeconds { get; init; } = 30;
}
```

#### [MODIFY] [appsettings.Example.json](file:///d:/SEP490_Resilience_Housing_Supply_Backend/RHS.API/appsettings.Example.json)
- Thêm cấu hình cho Gemini API:
```json
"GeminiAi": {
  "ApiKey": "YOUR_GEMINI_API_KEY_HERE",
  "ModelName": "gemini-1.5-flash",
  "ApiUrl": "https://generativelanguage.googleapis.com/v1beta",
  "TimeoutSeconds": 30
}
```

---

### Component 2: Domain Layer — Mở rộng AIVerificationResult

> Mở rộng thực thể `AIVerificationResult` để lưu chi tiết kết quả so khớp và lý do từ chối để báo lại cho người dùng.

#### [MODIFY] [AIVerificationResult.cs](file:///d:/SEP490_Resilience_Housing_Supply_Backend/RHS.Domain/Entities/AIVerificationResult.cs)
```csharp
public class AIVerificationResult
{
    public Guid VerificationId { get; set; }
    public Guid DocumentId { get; set; }
    
    // ── Existing fields ──────────────────────────────────────
    public string ExtractedText { get; set; } = string.Empty;
    public decimal FaceMatchScore { get; set; }
    public decimal RiskScore { get; set; }
    public string ValidationResult { get; set; } = string.Empty;    // MATCH, MISMATCH, ERROR
    public DateTime VerifiedAt { get; set; }

    // ── NEW: Chi tiết so khớp từng field ─────────────────────
    /// <summary>Tên trích xuất từ PDF</summary>
    public string? ExtractedFullName { get; set; }
    
    /// <summary>Số CCCD trích xuất từ PDF</summary>
    public string? ExtractedCitizenId { get; set; }
    
    /// <summary>Địa chỉ trích xuất từ PDF</summary>
    public string? ExtractedAddress { get; set; }
    
    /// <summary>Ngày sinh trích xuất từ PDF</summary>
    public string? ExtractedDateOfBirth { get; set; }
    
    /// <summary>Lỗi cụ thể hoặc lý do lệch thông tin để báo lại cho User</summary>
    public string? ErrorDetails { get; set; }
    
    /// <summary>Model AI đã sử dụng (ví dụ: "gemini-1.5-flash")</summary>
    public string? AiModelUsed { get; set; }

    // Navigation properties
    public ApplicationDocument Document { get; set; } = null!;
}
```

#### [MODIFY] [AIVerificationResultConfiguration.cs](file:///d:/SEP490_Resilience_Housing_Supply_Backend/RHS.Infrastructure/Configurations/AIVerificationResultConfiguration.cs)
- Cập nhật cấu hình EF Core cho các cột mới thêm vào database.

---

### Component 3: Application Layer — Interfaces & DTOs

#### [NEW] [IDocumentVerificationService.cs](file:///d:/SEP490_Resilience_Housing_Supply_Backend/RHS.Application/Interfaces/IDocumentVerificationService.cs)
```csharp
public interface IDocumentVerificationService
{
    /// <summary>
    /// Gửi file PDF của tài liệu lên Gemini API để phân tích và so khớp với thông tin profile của User.
    /// </summary>
    /// <param name="documentId">ID của ApplicationDocument</param>
    /// <param name="cancellationToken">Token hủy</param>
    Task<DocumentVerificationResultDto> VerifyDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
```

#### [NEW] [DocumentVerificationResultDto.cs](file:///d:/SEP490_Resilience_Housing_Supply_Backend/RHS.Application/DTOs/DocumentVerification/DocumentVerificationResultDto.cs)
```csharp
namespace RHS.Application.DTOs.DocumentVerification;

public class DocumentVerificationResultDto
{
    public Guid VerificationId { get; set; }
    public Guid DocumentId { get; set; }
    public string ValidationResult { get; set; } = string.Empty;  // MATCH, MISMATCH, ERROR
    public string? ErrorDetails { get; set; } // Lý do cụ thể để hiển thị cho User sửa
    
    // Thông tin trích xuất được
    public string? ExtractedFullName { get; set; }
    public string? ExtractedCitizenId { get; set; }
    public string? ExtractedAddress { get; set; }
    public string? ExtractedDateOfBirth { get; set; }
    
    public DateTime VerifiedAt { get; set; }
}
```

---

### Component 4: Infrastructure Layer — Gemini Verification Implementation

> Core implementation: tải tài liệu PDF, chuyển đổi sang base64, định nghĩa prompt so khớp nghiêm ngặt, gọi API Gemini với Response Schema định dạng JSON và xử lý lưu kết quả/cập nhật trạng thái tài liệu.

#### [NEW] [GeminiDocumentVerificationService.cs](file:///d:/SEP490_Resilience_Housing_Supply_Backend/RHS.Infrastructure/Services/GeminiDocumentVerificationService.cs)

**Logic hoạt động chính:**
1. **Load `ApplicationDocument`** cùng thông tin `User` (Profile) và `HousingApplication` liên quan từ DB.
2. **Download PDF** từ `FileUrl` (Cloudinary URL) sang byte array.
3. **Chuẩn bị Payload gửi Gemini**:
   * File PDF được chuyển sang base64 inline data: `{ "mimeType": "application/pdf", "data": "BASE64_STRING" }`.
   * Tạo prompt chi tiết yêu cầu Gemini đọc tài liệu và so sánh với thông tin profile được cung cấp sẵn (cccd, ngày sinh, địa chỉ, họ tên).
   * Cấu hình `responseMimeType` là `"application/json"` và truyền kèm **JSON Schema** mong muốn để nhận lại kết quả dạng cấu trúc chính xác.
4. **Gọi Gemini API** qua `HttpClient`.
5. **Parse JSON response** nhận được từ Gemini:
   * Nếu kết quả phân tích là không khớp (hoặc thiếu trường dữ liệu):
     * Cập nhật trạng thái `VerificationStatus` của tài liệu thành `"REJECTED"`.
     * Lưu lý do cụ thể (từ trường `mismatchDetails` của JSON nhận về) vào cột `ErrorDetails` để hiển thị lại cho User.
   * Nếu khớp hoàn toàn:
     * Cập nhật `VerificationStatus` thành `"VERIFIED"`.
6. **Lưu thông tin xác minh vào bảng `AIVerificationResults`**.

**Ví dụ JSON Schema truyền cho Gemini:**
```json
{
  "type": "OBJECT",
  "properties": {
    "isMatch": { "type": "BOOLEAN" },
    "mismatchDetails": { "type": "STRING", "description": "Lý do chi tiết nếu thông tin bị thiếu hoặc không khớp để User biết và sửa" },
    "extractedFullName": { "type": "STRING" },
    "extractedCitizenId": { "type": "STRING" },
    "extractedAddress": { "type": "STRING" },
    "extractedDateOfBirth": { "type": "STRING", "description": "Định dạng yyyy-MM-dd" }
  },
  "required": ["isMatch", "mismatchDetails"]
}
```

---

### Component 5: DI Registration

#### [NEW] [DocumentVerificationServiceCollectionExtensions.cs](file:///d:/SEP490_Resilience_Housing_Supply_Backend/RHS.Infrastructure/Extensions/DocumentVerificationServiceCollectionExtensions.cs)
```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RHS.Application.Interfaces;
using RHS.Infrastructure.Configurations;
using RHS.Infrastructure.Services;

namespace RHS.Infrastructure.Extensions;

public static class DocumentVerificationServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentVerificationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GeminiAiOptions>(
            configuration.GetSection(GeminiAiOptions.SectionName));

        // Đăng ký HttpClient cho Gemini Service
        services.AddHttpClient<IDocumentVerificationService, GeminiDocumentVerificationService>();

        return services;
    }
}
```

#### [MODIFY] [Program.cs](file:///d:/SEP490_Resilience_Housing_Supply_Backend/RHS.API/Program.cs)
- Đăng ký service xác minh tài liệu:
  `builder.Services.AddDocumentVerificationServices(builder.Configuration);`

---

### Component 6: Tích hợp vào luồng tải tài liệu (Auto-trigger)

#### [MODIFY] [DocumentService.cs](file:///d:/SEP490_Resilience_Housing_Supply_Backend/RHS.Infrastructure/Services/DocumentService.cs)
- Inject `IDocumentVerificationService` vào `DocumentService`.
- Sau khi upload và lưu metadata tài liệu thành công, gọi `VerifyDocumentAsync(documentId)` để tiến hành quét AI.
- Nếu xảy ra lỗi kết nối API hoặc xử lý ngoại lệ từ phía Gemini, đặt trạng thái `"REJECTED"` kèm ghi chú lỗi hệ thống để không làm nghẽn luồng upload.

---

### Component 8: Khắc phục lỗi HTTP 401 (Unauthorized) khi tải file từ Cloudinary

> **Nguyên nhân:** Cloudinary chặn việc tải trực tiếp (HTTP GET) các file dạng "raw" nếu thiết lập bảo mật của tài khoản người dùng chặn phân phối tài nguyên thô (Restricted raw media delivery).
> **Giải pháp:** 
> 1. Chuyển cơ chế upload PDF từ `RawUploadParams` sang `ImageUploadParams`. Cloudinary hỗ trợ lưu trữ và phân phối định dạng PDF như một loại tài nguyên `image`, không bị giới hạn quyền truy cập mặc định và tương thích tốt với các phương thức xóa/kiểm tra định dạng sẵn có.
> 2. Bổ sung phương thức `DownloadFileAsync(string fileUrl)` vào `IFileStorageService`. Phương thức này tự động kiểm tra xem URL có phải là của Cloudinary hay không. Nếu có, nó sẽ sử dụng Cloudinary SDK và API credentials để tạo link download có chữ ký xác thực (Signed URL), giải quyết triệt để lỗi 401 cho cả các file cũ đã tải lên trước đó.
> 3. Cấu hình `GeminiDocumentVerificationService` gọi `IFileStorageService.DownloadFileAsync()` thay vì tự tải trực tiếp qua `HttpClient`.

#### [MODIFY] [IFileStorageService.cs](file:///d:/Đồ%20Án%20Tốt%20Nghiệp/RHS.Application/Interfaces/IFileStorageService.cs)
- Bổ sung định nghĩa phương thức tải file:
  `Task<byte[]> DownloadFileAsync(string fileUrl);`

#### [MODIFY] [FileStorageService.cs](file:///d:/Đồ%20Án%20Tốt%20Nghiệp/RHS.Infrastructure/Services/FileStorageService.cs)
- Chuyển `RawUploadParams` sang `ImageUploadParams` trong cả `UploadPdfAsync` và `UploadPdfFromBytesAsync`.
- Implement `DownloadFileAsync` sử dụng `_cloudinary.Api.UrlImgUp` kèm thiết lập `.Signed(true)` để ký URL trước khi tải về.

#### [MODIFY] [GeminiDocumentVerificationService.cs](file:///d:/Đồ%20Án%20Tốt%20Nghiệp/RHS.Infrastructure/Services/GeminiDocumentVerificationService.cs)
- Inject `IFileStorageService` vào constructor.
- Thay thế đoạn code tự khởi tạo `HttpClient` tải file bằng cách gọi `_fileStorageService.DownloadFileAsync(document.FileUrl)`.

---


## Verification Plan

### Automated Tests
* Build giải pháp để kiểm tra xem có lỗi cú pháp hoặc build lỗi không:
  `dotnet build`

### Manual Verification
1. **Kiểm tra luồng tải lên có thông tin KHỚP hoàn toàn**:
   * Chuẩn bị file PDF chứa thông tin trùng khớp hoàn toàn với CitizenId, ngày sinh, địa chỉ của User trên hệ thống.
   * Upload tài liệu qua API.
   * Kiểm tra bảng `ApplicationDocuments` → `VerificationStatus` phải chuyển sang `"VERIFIED"`.
   * Hồ sơ ở trạng thái sẵn sàng để Officer duyệt thủ công.
2. **Kiểm tra luồng thông tin SAI LỆCH (MISMATCH)**:
   * Upload file PDF chứa số CCCD khác với profile User.
   * Kiểm tra `VerificationStatus` của tài liệu chuyển thành `"REJECTED"`.
   * API/Giao diện hiển thị chi tiết lý do từ chối (ví dụ: *"Số CCCD trên tài liệu không trùng khớp với thông tin profile"*).
3. **Kiểm tra luồng THIẾU thông tin (MISSING)**:
   * Upload file PDF bị mờ hoặc thiếu phần Ngày sinh.
   * Kiểm tra trạng thái tài liệu chuyển thành `"REJECTED"` kèm thông báo *"Không tìm thấy thông tin Ngày sinh trên tài liệu"*.
