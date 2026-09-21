# Hướng Dẫn Test Chức Năng AI Check Đúng Tên & Đúng Loại Giấy Tờ (Housing Application AI Document Verification)

Tài liệu này hướng dẫn chi tiết cách kiểm thử (test) tính năng **AI Gemini Check Đúng Tên & Đúng Loại Giấy Tờ** cho cả **Người nộp hồ sơ (Applicant)** và **Chủ đầu tư / Cán bộ quản lý**.

---

## 1. Tổng Quan Tính Năng

Hệ thống tích hợp AI (Gemini) để tự động kiểm tra giấy tờ đính kèm trong hồ sơ đăng ký nhà ở xã hội (NOXH):
1. **Check Đúng Tên (`isNameMatch`)**: Trích xuất Họ tên & CCCD trên PDF và đối chiếu trực tiếp với Profile người nộp.
2. **Check Đúng Loại Giấy Tờ (`isDocumentTypeMatch`)**: Kiểm tra xem file PDF upload có đúng loại biểu mẫu/giấy tờ yêu cầu (ví dụ: Giấy xác nhận thu nhập thấp, Mẫu 03 xác nhận nhà ở, Giấy chứng nhận hộ nghèo...) hay bị nộp nhầm giấy tờ khác.
3. **Phân quyền cho Applicant**: Người nộp hồ sơ được phép tự bấm **Kích hoạt AI Check từng giấy tờ** và **AI Audit toàn bộ bộ hồ sơ** của chính họ trước khi chính thức bấm Nộp (`Submit`).

---

## 2. Các Endpoint Phục Vụ Test

| STT | Phương thức | Endpoint | Phân quyền | Mô tả |
|---|---|---|---|---|
| 1 | `POST` | `/api/housing-applications/{appId}/documents` | `Applicant` | Upload file PDF giấy tờ |
| 2 | `POST` | `/api/housing-applications/{appId}/documents/{docId}/verify` | `Applicant`, `HousingDeveloper`, `SystemAdministrator`, `HousingAuthorityOfficer` | Kích hoạt AI Check thủ công 1 giấy tờ (Kiểm tra Đúng Tên & Đúng Loại Giấy Tờ) |
| 3 | `GET` | `/api/housing-applications/{appId}/documents/{docId}/verification` | `Authorize` | Lấy kết quả AI Check đã lưu từ DB |
| 4 | `POST` | `/api/housing-applications/{appId}/documents/audit` | `Applicant`, `HousingDeveloper`, `SystemAdministrator`, `HousingAuthorityOfficer` | Kích hoạt AI Audit kiểm tra toàn bộ danh mục giấy tờ (Đủ/Thiếu, Đúng Tên, Đúng Loại) |

---

## 3. Kịch Bản Test Chi Tiết (Test Cases)

### 📋 Chuẩn bị trước khi test:
- **Tài khoản Test**: 1 tài khoản Applicant (ví dụ: `nguyenvana@gmail.com`).
- **Profile người dùng**: Đã cập nhật Họ tên (VD: `Nguyễn Văn A`) và Số CCCD (VD: `012345678901`).
- **Hồ sơ mẫu**: 1 hồ sơ ở trạng thái `DRAFT` hoặc `NEED_MORE_DOCUMENTS`.
- **File PDF mẫu**:
  - File PDF A: Giấy tờ có ghi đúng tên `Nguyễn Văn A`, số CCCD `012345678901` và đúng mẫu giấy tờ (VD: Giấy chứng nhận hộ nghèo).
  - File PDF B: Giấy tờ có ghi tên người khác (VD: `Trần Văn B`) hoặc số CCCD khác.
  - File PDF C: File PDF không phải là mẫu giấy tờ yêu cầu (VD: Hóa đơn tiền điện hoặc Bằng lái xe).

---

### Kịch bản 1: Applicant tự bấm AI Check 1 Giấy tờ (Đúng Tên & Đúng Loại)

**Mục tiêu**: Xác nhận API trả về `isNameMatch: true` và `isDocumentTypeMatch: true`.

1. **Upload Giấy tờ**:
   - Call `POST /api/housing-applications/{applicationId}/documents` với:
     - `DocumentType`: `POVERTY_HOUSEHOLD_CERTIFICATE`
     - `File`: File PDF A (Đúng tên `Nguyễn Văn A`, đúng loại hộ nghèo)
   - Nhận về `DocumentId`.

2. **Applicant kích hoạt AI Check**:
   - Call `POST /api/housing-applications/{applicationId}/documents/{documentId}/verify`
   - Header: `Authorization: Bearer <Applicant_Token>`

3. **Kỳ vọng Response**:
   ```json
   {
     "verificationId": "...",
     "documentId": "...",
     "validationResult": "MATCH",
     "isNameMatch": true,
     "isDocumentTypeMatch": true,
     "nameCheckDetails": "Họ tên và CCCD trùng khớp với thông tin người nộp.",
     "documentTypeCheckDetails": "File PDF nộp đúng loại/biểu mẫu giấy tờ yêu cầu.",
     "extractedFullName": "NGUYỄN VĂN A",
     "extractedCitizenId": "012345678901",
     "extractedAddress": "Thành phố Hà Nội",
     "extractedDateOfBirth": "1990-01-01",
     "errorDetails": null,
     "verifiedAt": "2026-09-21T13:00:00Z"
   }
   ```

---

### Kịch bản 2: AI phát hiện Giấy tờ Sai Tên / Khác người

**Mục tiêu**: Xác nhận AI phát hiện giấy tờ không khớp thông tin người nộp (`isNameMatch: false`).

1. **Upload Giấy tờ**:
   - Call `POST /api/housing-applications/{applicationId}/documents` với `File` PDF B (Tên: `Trần Văn B`, CCCD khác).
2. **Kích hoạt AI Check**:
   - Call `POST /api/housing-applications/{applicationId}/documents/{documentId}/verify`.
3. **Kỳ vọng Response**:
   - `validationResult`: `"MISMATCH"`
   - `isNameMatch`: `false`
   - `nameCheckDetails`: Chứa thông báo tiếng Việt nêu rõ lệch tên/CCCD.
   - `errorDetails`: Hiển thị chi tiết ví dụ: `"Số CCCD trên tài liệu (987654321098) không khớp với profile (012345678901)"`.

---

### Kịch bản 3: AI phát hiện Giấy tờ Nộp Nhầm Loại (Sai Form mẫu)

**Mục tiêu**: Xác nhận AI phát hiện file PDF nộp không đúng loại giấy tờ đã chọn (`isDocumentTypeMatch: false`).

1. **Upload Giấy tờ**:
   - Chọn `DocumentType`: `LOW_INCOME_CERTIFICATE` (Giấy xác nhận thu nhập thấp).
   - Tải lên File PDF C (Nộp nhầm Hóa đơn điện hoặc Giấy xác nhận nhà ở).
2. **Kích hoạt AI Check**:
   - Call `POST /api/housing-applications/{applicationId}/documents/{documentId}/verify`.
3. **Kỳ vọng Response**:
   - `validationResult`: `"MISMATCH"`
   - `isDocumentTypeMatch`: `false`
   - `documentTypeCheckDetails`: Chứa thông báo nêu rõ file nộp không phải Giấy xác nhận thu nhập thấp.

---

### Kịch bản 4: Applicant bấm AI Audit toàn bộ bộ hồ sơ

**Mục tiêu**: Applicant chủ động kiểm tra danh mục tất cả giấy tờ trong hồ sơ trước khi nộp.

1. Call `POST /api/housing-applications/{applicationId}/documents/audit` với Token Applicant.
2. **Kỳ vọng Response**:
   ```json
   {
     "applicationId": "...",
     "priorityGroup": "URBAN_POOR",
     "housingStatus": "NO_HOUSE",
     "isComplete": false,
     "overallStatus": "INCOMPLETE",
     "statusName": "Thiếu giấy tờ",
     "passedCount": 1,
     "totalCount": 2,
     "checkedDocuments": [
       {
         "documentId": "...",
         "documentType": "POVERTY_HOUSEHOLD_CERTIFICATE",
         "documentTypeName": "Giấy chứng nhận hộ nghèo/cận nghèo",
         "fileUrl": "...",
         "isCorrectForm": true,
         "isNameMatch": true,
         "isDocumentTypeMatch": true,
         "nameCheckDetails": "Họ tên và CCCD trùng khớp với thông tin người nộp.",
         "documentTypeCheckDetails": "File PDF nộp đúng loại/biểu mẫu giấy tờ yêu cầu.",
         "formMatchStatus": "MATCH",
         "details": "Giấy tờ khớp đúng Form mẫu quy định và đúng tên người nộp."
       },
       {
         "documentId": "00000000-0000-0000-0000-000000000000",
         "documentType": "INCOME_CERTIFICATE",
         "documentTypeName": "Giấy xác nhận thu nhập",
         "fileUrl": "",
         "isCorrectForm": false,
         "isNameMatch": false,
         "isDocumentTypeMatch": false,
         "nameCheckDetails": "Giấy tờ chưa được nộp trong hồ sơ",
         "documentTypeCheckDetails": "Giấy tờ chưa được nộp trong hồ sơ",
         "formMatchStatus": "MISSING",
         "details": "⚠️ Giấy tờ chưa được nộp trong hồ sơ (Giấy xác nhận thu nhập)."
       }
     ],
     "missingDocumentTypes": ["INCOME_CERTIFICATE"],
     "missingDocumentNames": ["Giấy xác nhận thu nhập"],
     "summaryNote": "⚠️ THIẾU 1 loại giấy tờ bắt buộc: [Giấy xác nhận thu nhập]. Cần bổ sung trước khi nộp."
   }
   ```

---

### Kịch bản 5: Kiểm tra Bảo mật Phân quyền (Security Test)

**Mục tiêu**: Ngăn chặn Applicant A tự ý bấm verify hoặc audit trên hồ sơ của Applicant B.

1. Lấy Token của **Applicant B**.
2. Gọi `POST /api/housing-applications/{ApplicationId_Của_Applicant_A}/documents/audit` hoặc `verify`.
3. **Kỳ vọng Response**: `403 Forbidden`.

---

## 4. Gợi Ý Hiển Thị Lên Giao Diện (Frontend Integration)

Khi kết nối với giao diện React/Vue/Mobile App:
- Ở danh sách giấy tờ của Hồ sơ, hiển thị 2 Badge trạng thái:
  - Badge 1 (Đúng chính chủ): `✅ Đúng tên người nộp` (nếu `isNameMatch == true`) hoặc `❌ Sai tên/CCCD` (nếu `isNameMatch == false`).
  - Badge 2 (Đúng biểu mẫu): `✅ Đúng loại giấy tờ` (nếu `isDocumentTypeMatch == true`) hoặc `❌ Sai loại giấy tờ` (nếu `isDocumentTypeMatch == false`).
- Nút **"Kiểm tra bằng AI"** cho từng giấy tờ và nút **"Kiểm tra toàn bộ hồ sơ bằng AI"** trước khi bấm gửi Nộp hồ sơ.
