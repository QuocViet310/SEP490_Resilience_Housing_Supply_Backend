# 🏠 RHS Backend — Tổng Quan Hệ Thống

> **Intelligent Social Housing Coordination & Vetting Platform**
> Cập nhật: 2026-07-13

---

## 🏗️ Kiến Trúc

Clean Architecture với 4 layers:

```
RHS.API              → Presentation Layer (13 Controllers)
RHS.Application      → Business Logic Layer (DTOs, Interfaces)
RHS.Infrastructure   → Data Access Layer (Repositories, Services, DbContext, External APIs)
RHS.Domain           → Domain Layer (22 Entities, 6 Constants)
```

---

## 🛠️ Tech Stack

| Thành phần | Công nghệ |
|---|---|
| Framework | .NET 8 Web API |
| ORM | Entity Framework Core 8 (Code First) |
| Database | SQL Server |
| Auth | JWT + BCrypt + Google OAuth 2.0 |
| File Storage | Cloudinary (images + PDFs) |
| Payment | VNPay (HMAC-SHA512) |
| eKYC | FPT AI (OCR, Face Match, Liveness Detection) |
| AI Verification | Google Gemini (document scanning — trợ lý CĐT) |
| PDF Generation | QuestPDF (receipts, principle agreements) |
| Background Jobs | Quartz.NET (scheduled tasks) |

---

## 📊 Database — 22 Entities

### 1. User
Quản lý người dùng hệ thống (Applicant, Staff, Admin).

| Property | Type |
|---|---|
| Id | Guid |
| RoleId | Guid |
| FullName | string |
| Email | string |
| PhoneNumber | string? |
| PasswordHash | string? |
| CitizenId | string? |
| DateOfBirth | DateTime? |
| Address | string? |
| Status | string (Active/Inactive/Suspended) |
| CreatedAt | DateTime |
| IsEmailVerified | bool |
| GoogleId | string? |
| ProfileImageUrl | string? |
| UpdatedAt | DateTime? |
| LastLoginAt | DateTime? |

### 2. Role
Phân quyền người dùng (6 roles được seed sẵn).

| Role ID | RoleName |
|---|---|
| 1111...-1111 | Guest |
| 2222...-2222 | Applicant |
| 3333...-3333 | Housing Authority Officer |
| 4444...-4444 | System Administrator |
| 5555...-5555 | Department Of Construction (SXD) |
| 6666...-6666 | Housing Developer (CĐT) |

### 3. RefreshToken
JWT refresh token, 7 ngày hết hạn, hỗ trợ rotation & revocation.

### 4. OtpVerification
Mã OTP 6 số, 5 phút hết hạn, dùng cho email verification & reset password.

### 5. HousingProject
Dự án nhà ở xã hội.

| Property | Type | Ghi chú |
|---|---|---|
| Id | Guid | |
| ProjectName | string | |
| Description | string | |
| Province, District, Street, Ward | string | Địa chỉ phân cấp |
| LotteryDate | DateTime? | Ngày bốc thăm |
| LotteryLocation | string? | |
| DepositAmount | decimal | Tiền đặt cọc |
| MinPrice, MaxPrice | decimal | |
| MinArea, MaxArea | double | |
| AvailableUnits | int | Số căn còn để phân bổ qua bốc thăm (Hướng A: chỉ trừ khi WON/PRIORITY_WON) |
| ThumbnailUrl | string? | |
| HousingProjectStatusId | Guid | FK → HousingProjectStatus |
| DecisionNumber | string? | Số quyết định phê duyệt |
| ApprovalDate | DateTime? | |
| IsConfirmed | bool | |
| ApplicationOpenDate, ApplicationCloseDate | DateTime? | |
| PublicAnnounceAt | DateTime? | Thời điểm công bố (Đ38.1.b) |
| RejectReason | string? | |
| DeveloperId | Guid? | FK → User (CĐT) |
| IsDeleted | bool | Soft delete |

### 6. HousingProjectStatus
Trạng thái dự án (6 statuses được seed).

| StatusCode | StatusName |
|---|---|
| PENDING | Chờ duyệt |
| UPCOMING | Sắp mở |
| OPEN | Đang mở |
| CLOSED | Đã đóng |
| FULL | Đã đầy |
| REJECTED | Bị từ chối |

### 7. HousingApplication
Hồ sơ đăng ký mua nhà ở xã hội. Có 10 trạng thái (xem `ApplicationStatusConstants`).

| Property | Type |
|---|---|
| ApplicationId | Guid |
| ApplicantId | Guid |
| ProjectId | Guid |
| OfficerId | Guid? |
| ApplicationStatus | string |
| SubmittedAt | DateTime |
| PriorityScore | decimal |
| FinalDecisionDate | DateTime? |
| SlotCode | string? |
| FullName, CitizenId | string |
| Occupation, WorkPlace | string? |
| CurrentResidence, PermanentAddress | string |
| HousingStatus | string |
| MaritalStatus | string? |
| HouseholdMembersCount | int |
| PriorityGroup | string? |
| ReceiptUrl | string? |

**10 Trạng Thái Hồ Sơ:**
DRAFT → SUBMITTED → REVIEWING → NEED_MORE_DOCUMENTS → PENDING_SXD_REVIEW → APPROVED → DEPOSIT_PAID
(ngoài ra: REJECTED, CANCELED, EXPIRED)

### 8. ApplicationStatusHistory
Lịch sử thay đổi trạng thái hồ sơ (có Action, OldStatus, NewStatus, Note, ChangedBy).

### 9. ApplicationDocument
Hai loại PDF **bắt buộc** khi nộp: `HOUSING_CONDITION_PROOF` (giấy xác nhận nhà ở — Đ29) và `POVERTY_HOUSEHOLD_CERTIFICATE` (giấy hộ nghèo/cận nghèo — Đ30.3).  
`VerificationStatus`: PENDING / VERIFIED / REJECTED. AI (Gemini) là **trợ lý thẩm định cho CĐT** (trigger thủ công khi tiếp nhận); không chặn người dân nộp.

### 10. AIVerificationResult
Kết quả Gemini theo từng tài liệu: extract FullName, CitizenId, Address, DOB; `ValidationResult` MATCH/MISMATCH/ERROR; `ErrorDetails`.

### 11. Appointment
Lịch hẹn liên quan đến hồ sơ (ngày, địa điểm, trạng thái).

### 12. ProjectImage
Ảnh dự án (URL, DisplayOrder).

### 13. HousingQuota
Hạn ngạch ưu tiên theo nhóm đối tượng (PriorityGroup, AllocatedSlots, RemainingSlots) — cập nhật khi bốc thăm.

### 14. Payment
Giao dịch thanh toán VNPay (OrderId, Amount, Status, VnpResponseCode, VnpTransactionNo...).

### 15. PrincipleAgreement
Hợp đồng nguyên tắc (ApplicationId, PdfUrl). Sinh sau `DEPOSIT_PAID`.  
**Ý nghĩa:** cam kết tham gia phân suất/bốc thăm — **không** đồng nghĩa đã được phân căn.

### 16. EligibilityAssessment
Đánh giá Đ29–30 trên form khi nộp (`Eligible`, `EstimatedScore`, `ReasonsJson`). Không đọc PDF.

### 17. Notification
Thông báo in-app (Title, Content, NotificationType, IsRead).

### 18. Message
Tin nhắn giữa các user (SenderId, ReceiverId, Content).

### 19. AuditLog
Nhật ký hệ thống (Action, EntityName, OldValues/NewValues JSON, IpAddress). Tự động ghi log INSERT/UPDATE/DELETE trên tất cả entity.

### 20. PolicyConfig
Tham số nghị định/hành chính: tacit days, deposit hours, announce days, max m²/người, one-app rule…

### 21. IssueReport
Báo cáo lỗi/vấn đề từ người dùng (Title, Description, IssueType, Status, ScreenshotUrl).

### 22. Wishlist
Danh sách yêu thích dự án của Applicant.

### 23. LotteryDraw (bổ sung nghiệp vụ)
Lưu lần bốc thăm: TotalUnits, PriorityAllocated, RandomAllocated, RandomSeed, ResultJson.  
Kết quả gắn trên `HousingApplication.LotteryResult`: PENDING / WON / LOST / PRIORITY_WON.

---

## 🌐 13 Controllers — 70+ API Endpoints

### AuthController (`/api/Auth`)
Đăng ký, đăng nhập (email + Google OAuth), OTP, refresh token, logout, quên/reset/đổi mật khẩu.
→ **10 endpoints**

### UsersController (`/api/Users`)
Xem/cập nhật profile, upload/xóa ảnh đại diện, xóa tài khoản.
→ **7 endpoints**

### AdminController (`/api/Admin`)
Quản lý tài khoản staff: tạo, danh sách (phân trang + search + filter), chi tiết, cập nhật, phân quyền, khóa/mở khóa, reset password.
→ **8 endpoints**

### HousingProjectsController (`/api/HousingProjects`)
CRUD dự án + phân trang/search/filter (FE-01, FE-02), phê duyệt/từ chối dự án bởi SXD.
→ **6 endpoints**

### HousingProjectStatusesController (`/api/housing-project-statuses`)
Tra cứu danh sách trạng thái dự án.
→ **1 endpoint**

### HousingApplicationsController (`/api/housing-applications`)
Toàn bộ vòng đời hồ sơ: tạo, xem danh sách (phân trang + filter), chi tiết, submit, CĐT review, SXD review, hủy, dashboard cho CĐT và SXD.
→ **11 endpoints**

### HousingDeveloperController (`/api/housing-developer`)
CĐT nộp danh sách đã duyệt lên SXD, lấy danh sách cuối cùng (DEPOSIT_PAID).
→ **2 endpoints**

### DocumentsController (`/api/housing-applications/{id}/documents`)
Upload PDF tài liệu, xóa, xem kết quả AI verification, trigger verify thủ công.
→ **4 endpoints**

### EKycController (`/api/EKyc`)
Check trùng CitizenId, OCR CCCD, face-match, liveness detection (FPT AI).
→ **4 endpoints**

### PaymentController (`/api/Payment`)
Tạo URL VNPay, callback, tra cứu kết quả đặt cọc, xem chi tiết giao dịch, lịch sử thanh toán, tải hợp đồng nguyên tắc PDF.
→ **6 endpoints**

### NotificationController (`/api/Notification`)
Danh sách thông báo (phân trang), đếm chưa đọc, đánh dấu đã đọc (từng cái / tất cả).
→ **4 endpoints**

### IssueReportsController (`/api/issue-reports` + `/api/admin/issue-reports`)
Tạo báo cáo, xem danh sách của mình. Admin: danh sách (phân trang + search + filter), chi tiết, cập nhật trạng thái.
→ **5 endpoints**

### WishlistController (`/api/wishlist`)
Thêm/xóa dự án khỏi wishlist, danh sách (phân trang), kiểm tra trạng thái.
→ **5 endpoints**

---

## 🔄 Các Module Đã Hoàn Thành

### Authentication & Authorization
- Email/Password + Google OAuth 2.0
- OTP xác thực email (6 số, 5 phút)
- JWT Access Token (60 phút) + Refresh Token (7 ngày)
- Role-based Authorization (6 roles)
- BCrypt hashing

### Profile Management
- CRUD profile, upload/delete ảnh (Cloudinary), soft delete account

### eKYC (FPT AI)
- OCR CCCD, Face Match, Liveness Detection
- File validation (magic bytes, MIME, ≤5MB)
- Check trùng CitizenId

### FE-01 & FE-02 — Khám Phá & Lọc Dự Án
- Phân trang, search (tên, tỉnh, quận)
- Filter (giá, diện tích, trạng thái)
- Phân quyền hiển thị (public chỉ thấy UPCOMING/OPEN/CLOSED/FULL; SXD/CĐT thấy cả PENDING/REJECTED)

### FE-20 — Quản Lý Vòng Đời Dự Án
- PATCH status (APPROVE: PENDING→UPCOMING, REJECT: PENDING→REJECTED)
- Validation: IsConfirmed, DecisionNumber, ngày mở/đóng
- Worker: UPCOMING→OPEN (kèm Đ38.1.b announce days), OPEN→CLOSED; tacit theo PolicyConfig

### Housing Application — Maker-Checker Flow
- Tạo hồ sơ (Applicant), submit (DRAFT→SUBMITTED) khi đủ 2 giấy tờ + đủ điều kiện Đ29–30 trên form
- CĐT (Maker): tiếp nhận REVIEWING; AI Gemini = **trợ lý** quét PDF (nút verify); gửi batch → PENDING_SXD_REVIEW
- SXD (Checker): APPROVED / REJECTED; đối soát CCCD Đ38.1.đ
- Tacit approval theo `TACIT_APPROVAL_DAYS`
- **Hướng A:** APPROVED **không** trừ `AvailableUnits`

### Document Upload & AI Verification
- Bắt buộc: `HOUSING_CONDITION_PROOF` + `POVERTY_HOUSEHOLD_CERTIFICATE`
- Gemini: đối chiếu PDF ↔ eKYC + loại giấy + NO_HOUSE/SMALL_HOUSE / hộ nghèo-cận nghèo (Đ29–30)
- Vai trò: hỗ trợ CĐT; **không** thay SXD; **không** bắt buộc VERIFIED mới nộp

### Payment (VNPay)
- DEPOSIT_PAID → SlotCode + PrincipleAgreement (cam kết tham gia bốc thăm)
- Quá hạn → EXPIRED (**không** hoàn AvailableUnits — Hướng A)

### Lottery & Đ44
- Trần suất = `AvailableUnits`; trừ khi WON / PRIORITY_WON
- Đ44: chỉ công bố người trúng (+ HĐ nguyên tắc)

### Scheduled Background Jobs
- Mở/đóng dự án + chặn OPEN nếu chưa đủ ngày công bố (`PUBLIC_ANNOUNCE_MIN_DAYS`)
- Tacit approval; payment timeout (`DEPOSIT_PAYMENT_HOURS`)

> **Chi tiết đầy đủ:** xem [`BUSINESS_FLOW.md`](./BUSINESS_FLOW.md).

### Notification
- In-app notification (tự động tạo khi trạng thái hồ sơ thay đổi)
- Phân trang, badge unread count, mark as read

### Issue Report
- Người dùng báo cáo lỗi (kèm screenshot URL)
- Admin quản lý: xem, filter, cập nhật trạng thái

### Wishlist
- Applicant thêm/xóa dự án yêu thích
- Kiểm tra trạng thái wishlist

### Staff Management (Admin)
- Tạo/sửa/khóa/mở tài khoản staff (CĐT, SXD)
- Phân quyền, reset password
- Danh sách phân trang + search + filter

### Audit Logging
- Tự động ghi log mọi INSERT/UPDATE/DELETE
- Lưu OldValues/NewValues dạng JSON
- Không log AuditLog, RefreshToken, OtpVerification

---

## 📚 External Integrations

| Dịch vụ | Mục đích |
|---|---|
| **Cloudinary** | Lưu trữ ảnh + PDF (signed URL download) |
| **VNPay** | Cổng thanh toán (HMAC-SHA512) |
| **FPT AI** | eKYC: OCR, Face Match, Liveness Detection |
| **Google Gemini 1.5 Flash** | AI document verification (quét PDF, so khớp thông tin) |
| **Google OAuth 2.0** | Đăng nhập bằng Google |
| **SMTP Email** | Gửi OTP verification |
| **Quartz.NET** | Scheduled background jobs |

---

## 📁 Cấu Trúc Thư Mục Chính

```
RHS.API/
  Controllers/          # 13 controllers
  Middleware/           # Exception handling middleware
  Program.cs            # DI, pipeline, auth config

RHS.Application/
  Interfaces/           # 22+ service & repository interfaces
  DTOs/                 # Auth, Admin, User, HousingProjects, HousingApplications,
                        #   EKyc, Payment, DocumentVerification, IssueReports,
                        #   Notification, Wishlist

RHS.Infrastructure/
  Data/                 # AppDbContext (22 DbSets, audit logging)
  Configurations/       # 18+ Fluent API entity configurations
  Repositories/         # 13 repository implementations
  Services/             # 21 service implementations (business logic + external APIs)
  Extensions/           # DI registration extensions (EKyc, DocumentVerification, Quartz)
  Migrations/           # 17 migration snapshots

RHS.Domain/
  Entities/             # 22 entity classes
  Constants/            # 6 string constant classes (statuses, document types, roles, actions)
```

---

## 🔐 Constants (thay vì Enums)

Hệ thống dùng string constants thay vì C# enum:

| File | Giá trị |
|---|---|
| `ApplicationStatusConstants` | DRAFT, SUBMITTED, REVIEWING, NEED_MORE_DOCUMENTS, PENDING_SXD_REVIEW, APPROVED, DEPOSIT_PAID, REJECTED, CANCELED, EXPIRED |
| `DocumentTypeConstants` | HOUSING_CONDITION_PROOF, POVERTY_HOUSEHOLD_CERTIFICATE |
| `HousingStatusConstants` | NO_HOUSE, SMALL_HOUSE |
| `NotificationTypeConstants` | APPLICATION_SUBMITTED, APPROVED, REJECTED, NEED_MORE_DOCS, DEPOSIT_PAID, EXPIRED, PENDING_SXD_REVIEW, CANCELED |
| `ReviewActionConstants` | APPROVE, REJECT, REQUEST_MORE_DOCUMENTS, PROPOSE, ASSIGN_OFFICER, SUBMIT, SAVE_DRAFT, PAYMENT_TIMEOUT, DEPOSIT_PAYMENT, CANCEL, SUBMIT_TO_DEPARTMENT, TACIT_APPROVAL |
| `RoleConstants` | Applicant, Housing Authority Officer, System Administrator, Department Of Construction, Housing Developer |

---

## 🗺️ Business Flow — Vòng Đời Hồ Sơ

> Nguồn đầy đủ: [`BUSINESS_FLOW.md`](./BUSINESS_FLOW.md) (Hướng A, AI trợ lý CĐT, Đ29–30 / Đ38 / Đ44).

```
Applicant tạo hồ sơ (DRAFT) + đủ 2 giấy tờ
  → Submit (SUBMITTED) → Eligibility Đ29–30 + PDF biên nhận
  → CĐT tiếp nhận (REVIEWING) → có thể chạy AI verify PDF (trợ lý)
  → CĐT yêu cầu bổ sung (NEED_MORE_DOCUMENTS) ↔ REVIEWING
  → CĐT gửi SXD (PENDING_SXD_REVIEW)
  → SXD duyệt (APPROVED) hoặc từ chối (REJECTED)
     → Im lặng TACIT_APPROVAL_DAYS → tự APPROVED (không trừ AvailableUnits)
  → Đặt cọc VNPay
     → DEPOSIT_PAID → SlotCode + HĐ nguyên tắc (cam kết bốc thăm)
     → Quá hạn → EXPIRED (không hoàn suất)
  → Bốc thăm: WON/PRIORITY_WON (trừ AvailableUnits) | LOST
  → Đ44 công bố người trúng
```

## 🗺️ Business Flow — Duyệt Dự Án

```
CĐT tạo dự án (PENDING)
  → SXD phê duyệt (UPCOMING)
  → Worker: UPCOMING → OPEN (đủ ApplicationOpenDate + PUBLIC_ANNOUNCE_MIN_DAYS)
  → Worker: OPEN → CLOSED (qua ApplicationCloseDate)
  → SXD có thể từ chối (REJECTED + RejectReason)
```

---

## 📊 Tổng Kết

| Thành phần | Số lượng |
|---|---|
| Entities | 22 |
| Controllers | 13 |
| API Endpoints | ~70 |
| Roles | 6 |
| Repositories | 13 |
| Services | 21 |
| Migrations | 17 |
| External Integrations | 7 |
| Background Jobs | 2 (Quartz.NET) |
