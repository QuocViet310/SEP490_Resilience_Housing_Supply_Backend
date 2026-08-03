# 🏠 Resilience Housing Supply - Backend API

**Intelligent Social Housing Coordination & Vetting Platform**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoft-sql-server)](https://www.microsoft.com/sql-server)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## 📖 Tài liệu nghiệp vụ

Chi tiết luồng hồ sơ, Hướng A (`AvailableUnits`), AI trợ lý CĐT, Đ29–30 / Đ38 / Đ44:

→ **[`BUSINESS_FLOW.md`](./BUSINESS_FLOW.md)**  
→ Tổng quan entity: [`Entity.md`](./Entity.md)

---

## 📖 Giới Thiệu

**Resilience Housing Supply (RHS)** là nền tảng công nghệ chuyên biệt phát triển hạ tầng số cho công bằng xã hội. Chúng tôi tạo ra "Nền tảng Điều phối & Thẩm định Nhà ở Xã hội Thông minh" kết nối Sở Xây dựng, chủ đầu tư và các đối tượng được mua nhà ở xã hội theo Luật Nhà ở 2023.

### 🎯 Sứ Mệnh

Chuyển đổi quy trình phân bổ nhà ở truyền thống, thiếu minh bạch thành một hệ sinh thái dựa trên dữ liệu, minh bạch và hiệu quả.

### ⭐ Giá Trị Cốt Lõi

- **Minh Bạch**: Đảm bảo quy trình "hộp thủy tinh" cho tất cả người thụ hưởng
- **Hiệu Quả**: Giảm thiểu tắc nghẽn hành chính thông qua tự động hóa AI
- **Công Bằng**: Ưu tiên nhà ở cho những người cần nhất dựa trên dữ liệu đã xác minh
- **Toàn Vẹn**: Duy trì hồ sơ kiểm toán an toàn và bất biến cho mọi đơn đăng ký

---

## 🚀 Tính Năng Chính

### ✅ Authentication & Authorization (Đã Hoàn Thành)

- 🔐 **Đăng ký/Đăng nhập** bằng Email & Password
- 🔐 **Đăng nhập Google OAuth 2.0**
- 📧 **Xác thực OTP** qua Email
- 🎫 **JWT Access Token** (60 phút) & **Refresh Token** (7 ngày)
- 👥 **Role-based Authorization** (Applicant, Housing Developer, Department Of Construction, System Administrator, Housing Authority Officer)
- 🔒 **BCrypt Password Hashing**
- 🔄 **Token Refresh & Revocation**
- 🔑 **Forgot Password** & **Reset Password** với OTP
- 🔐 **Change Password** (authenticated users)

### ✅ Profile Management (Đã Hoàn Thành)

- 👤 **Get Profile** - Xem thông tin cá nhân
- ✏️ **Update Profile** - Cập nhật fullName, phoneNumber, dateOfBirth, address (sau eKYC: chỉ cho đổi SĐT)
- 📸 **Upload Profile Image** - Upload ảnh đại diện lên Cloudinary
- 🗑️ **Delete Profile Image** - Xóa ảnh đại diện
- ❌ **Delete Account** - Xóa tài khoản (soft delete; giải phóng CCCD/email)

### ✅ eKYC — Xác Minh Danh Tính Điện Tử (Đã Hoàn Thành)

Luồng đang dùng trên Web/Mobile: **OCR CCCD → kiểm tra trùng CCCD → Face Match → lưu profile**.

- 🪪 **OCR Căn cước công dân** — Trích xuất thông tin từ ảnh CCCD (**VNPT eKYC**)
- 🤝 **Face Match** — So khớp khuôn mặt selfie với ảnh trên CCCD (**VNPT eKYC**)
- 🔁 **Verify Identity (one-shot)** — OCR + check CCCD + Face Match + auto-save profile (`POST /api/EKyc/verify-identity`)
- 🛡️ **File Validation** — Kiểm tra magic bytes, MIME type, dung lượng ≤ 5 MB
- ⚠️ **Liveness** — Endpoint `POST /api/EKyc/liveness` còn trong API nhưng **không hỗ trợ** qua VNPT REST (cần SDK client); **không dùng trong flow sản phẩm**

### ✅ Nghiệp vụ nhà ở xã hội (Đã Hoàn Thành)

- 📍 **Khám phá dự án** — Tìm kiếm, lọc, chi tiết dự án + căn hộ
- 📝 **Hồ sơ đăng ký mua** — Draft → Submit → Maker–Checker (CĐT → SXD)
- 📋 **Đối tượng Đ76** — Nhiều nhóm đối tượng mua NOXH + giấy tờ theo nhóm
- ✅ **Rule engine Đ29–Đ30** — Điều kiện nhà ở + thu nhập / chuẩn nghèo
- 🤖 **AI trợ lý CĐT (Gemini)** — Quét/đối chiếu giấy tờ; **không** phê duyệt cuối
- ✍️ **Hợp đồng nguyên tắc** — Ký online sau khi được phân vào luồng ký HĐ
- 💳 **VNPay** — Đặt cọc + thanh toán các đợt (installment)
- 🎲 **Bốc thăm online** — Lịch, lobby OTP, live (SignalR), công bố kết quả
- 📢 **Thông báo in-app** + thông báo công khai / hậu kiểm công khai
- ⚙️ **PolicyConfig** — Tham số nghị định (tacit approval, hạn cọc, trần thu nhập, …)
- 🕒 **Background workers** — Mở/đóng dự án, tacit SXD, hết hạn cọc, quá hạn đợt thanh toán

---

## 🏗️ Kiến Trúc

Dự án sử dụng **Clean Architecture** với 4 layers:

```
┌─────────────────────────────────────────┐
│         RHS.API (Presentation)          │  ← Controllers, Middleware
├─────────────────────────────────────────┤
│      RHS.Application (Business)         │  ← DTOs, Interfaces, Services
├─────────────────────────────────────────┤
│    RHS.Infrastructure (Data Access)     │  ← Repositories, DbContext
├─────────────────────────────────────────┤
│        RHS.Domain (Core Entities)       │  ← Domain Models
└─────────────────────────────────────────┘
```

---

## 🛠️ Tech Stack

### Backend
- **.NET 8.0** - Latest LTS framework
- **ASP.NET Core Web API** - RESTful API
- **Entity Framework Core 8.0** - ORM
- **SQL Server** - Database

### Security & Authentication
- **JWT Bearer Authentication**
- **BCrypt.Net** - Password hashing
- **Google.Apis.Auth** - Google OAuth

### Cloud Services & Integrations
- **Cloudinary** — Image / PDF storage & CDN
- **SMTP Email** — OTP verification
- **VNPT eKYC** — OCR CCCD + Face Compare (liveness qua REST **không** hỗ trợ)
- **Google Gemini** — AI document verification (trợ lý CĐT)
- **VNPay** — Deposit & installment payments
- **QuestPDF** — Receipt / principle-agreement / report PDFs
- **SignalR** — Live lottery lobby
- **Quartz.NET** — Background automation workers

### Documentation
- **Swagger/OpenAPI** — API documentation

---

## 📦 Cài Đặt

### Yêu Cầu Hệ Thống

- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server 2019+** hoặc **SQL Server Express**
- **Visual Studio 2022** hoặc **VS Code**
- **Git**

### Clone Repository

```bash
git clone https://github.com/your-org/SEP490_Resilience_Housing_Supply_Backend.git
cd SEP490_Resilience_Housing_Supply_Backend
```

### Restore Dependencies

```bash
dotnet restore
```

### Cấu Hình Database

1. Cập nhật connection string trong `RHS.API/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=RHS_Database;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
}
```

2. Chạy migration:

```bash
dotnet ef database update --project RHS.Infrastructure --startup-project RHS.API
```

### Cấu Hình Email (OTP)

Xem hướng dẫn chi tiết trong [SETUP.md](SETUP.md)

### Cấu Hình Cloudinary (Image Storage)

1. Tạo tài khoản miễn phí tại [Cloudinary](https://cloudinary.com/users/register_free)
2. Lấy Cloud Name, API Key, API Secret từ Dashboard
3. Thêm vào `appsettings.json`:

```json
"Cloudinary": {
  "CloudName": "your-cloud-name",
  "ApiKey": "your-api-key",
  "ApiSecret": "your-api-secret"
}
```

Xem hướng dẫn chi tiết: [QUICK_START_CLOUDINARY.md](QUICK_START_CLOUDINARY.md)

### Chạy Application

```bash
dotnet run --project RHS.API
```

Hoặc nhấn **F5** trong Visual Studio.

Application sẽ chạy tại:
- **HTTPS**: https://localhost:7000
- **HTTP**: http://localhost:5000
- **Swagger**: https://localhost:7000/swagger

---

## 📚 Documentation

### 🚀 Quick Start
- **[QUICK_START_CLOUDINARY.md](QUICK_START_CLOUDINARY.md)** - Setup Cloudinary trong 5 phút
- **[SETUP.md](SETUP.md)** - Hướng dẫn cài đặt project

### 📖 Implementation Guides
- **[IMPLEMENTATION_COMPLETE.md](IMPLEMENTATION_COMPLETE.md)** - ⭐ Tổng quan toàn bộ hệ thống
- **[CLOUDINARY_SETUP.md](CLOUDINARY_SETUP.md)** - Hướng dẫn Cloudinary chi tiết
- **[CLOUDINARY_MIGRATION_SUMMARY.md](CLOUDINARY_MIGRATION_SUMMARY.md)** - Chi tiết migration

### 📋 API Documentation
- **[PROFILE_IMAGE_AND_DELETE_ACCOUNT_API.md](PROFILE_IMAGE_AND_DELETE_ACCOUNT_API.md)** - Upload/Delete APIs
- **[Swagger UI](https://localhost:7000/swagger)** - API documentation tương tác

### 🗄️ Database
- **[MIGRATION_GUIDE.md](MIGRATION_GUIDE.md)** - Database migration guide
- **[SCHEMA_UPDATE_SUMMARY.md](SCHEMA_UPDATE_SUMMARY.md)** - Schema changes

---

## 🧪 Testing

### Swagger UI

1. Mở https://localhost:7000/swagger
2. Test các endpoints trực tiếp
3. Sử dụng nút **Authorize** để thêm JWT token

### Postman

Import collection: `RHS_Authentication_API.postman_collection.json`

```bash
# Collection tự động lưu tokens vào variables
# Test theo thứ tự: Register → Verify OTP → Login → Protected Endpoints
```

---

## 📊 Database Schema

### Users Table
- Thông tin người dùng đầy đủ
- Email, Password (hashed), FullName, PhoneNumber
- CitizenId, DateOfBirth, Address
- RoleId (FK), ProfileImageUrl (Cloudinary)
- Google OAuth integration
- Status (Active/Deleted)

### Roles Table
- Roles: Applicant, Housing Developer, Department Of Construction, System Administrator, Housing Authority Officer
- Seeded automatically on database creation

### RefreshTokens Table
- JWT refresh tokens
- Token rotation & revocation
- 7 days expiration

### OtpVerifications Table
- OTP verification codes (6 digits)
- 5 minutes expiration
- Email verification & password reset

Chi tiết entity: [`Entity.md`](./Entity.md) · luồng nghiệp vụ: [`BUSINESS_FLOW.md`](./BUSINESS_FLOW.md)

---

## 🔐 API Endpoints (tóm tắt)

> Danh sách đầy đủ theo Swagger. Auth OTP verify dùng `POST /api/auth/verify-otp` (không phải `verify-email`).

### Public / Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Đăng ký tài khoản với email/password |
| POST | `/api/auth/verify-otp` | Xác thực email với OTP |
| POST | `/api/auth/resend-otp` | Gửi lại OTP |
| POST | `/api/auth/login` | Đăng nhập email/password |
| POST | `/api/auth/google-login` | Đăng nhập Google OAuth |
| POST | `/api/auth/refresh-token` | Refresh access token |
| POST | `/api/auth/forgot-password` | Quên mật khẩu (gửi OTP) |
| POST | `/api/auth/reset-password` | Reset mật khẩu với OTP |
| POST | `/api/auth/change-password` | Đổi mật khẩu (JWT) |
| POST | `/api/auth/logout` | Đăng xuất |

### Profile

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users/profile` | Lấy thông tin profile |
| PUT | `/api/users/profile` | Cập nhật profile |
| POST | `/api/users/profile/image` | Upload ảnh đại diện |
| DELETE | `/api/users/profile/image` | Xóa ảnh đại diện |
| POST | `/api/users/delete-account` | Soft-delete tài khoản |

### eKYC — Xác minh danh tính (VNPT)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/EKyc/check-citizen-id` | Kiểm tra CCCD còn dùng được không |
| POST | `/api/EKyc/ocr` | OCR CCCD (`image`) |
| POST | `/api/EKyc/face-match` | Face match (`faceImage`, `idCardImage`) |
| POST | `/api/EKyc/verify-identity` | One-shot: OCR + check CCCD + face match + lưu profile |
| POST | `/api/EKyc/liveness` | **Không hỗ trợ qua VNPT REST** (endpoint tồn tại, ném lỗi) |

### Nghiệp vụ chính (nhóm controller)

| Controller | Prefix | Phạm vi |
|---|---|---|
| HousingProjects | `/api/HousingProjects` | CRUD / search / phê duyệt dự án, decision CĐT |
| HousingApplications | `/api/housing-applications` | Hồ sơ, dashboard CĐT/SXD, members, assign apartment, flag violation |
| Documents | `/api/housing-applications/{id}/documents` | Upload PDF, AI verify / audit |
| HousingDeveloper | `/api/housing-developer` | Submit batch lên SXD, final list |
| ContractSign | `/api/contract-sign` | Ký HĐ nguyên tắc |
| Payment | `/api/Payment` | VNPay cọc + installment, download PDF HĐ |
| Lottery | `/api/projects/{id}/lottery` | Schedule, live session, draw, publish |
| Lookup | `/api/lookup` | Document types, priority groups |
| Announcement | `/api/announcements` | Thông báo công khai |
| PublicPostCheck | `/api/public/post-check-list` | Hậu kiểm công khai |
| Beneficiaries | `/api/beneficiaries` | Danh sách thụ hưởng |
| PolicyConfig | `/api/PolicyConfig` | Tham số nghị định (Admin) |
| Admin | `/api/Admin` | Staff accounts |
| Notification | `/api/Notification` | Thông báo in-app |
| Wishlist | `/api/wishlist` | Quan tâm dự án |
| IssueReports | `/api/issue-reports` | Báo cáo sự cố (+ admin) |
| Reports | `/api/Reports` | Excel/PDF export |

**Controllers trong `RHS.API`: 21.** Chi tiết Swagger tại runtime.

---

## 👥 Team

**Group**: GSU26SE51

| Name | Role | Email |
|------|------|-------|
| Tôn Thất Hoàng Minh | Lecturer | MinhTTH5@fe.edu.vn |
| Lý Thế Vinh | Leader | vinhltse182829@fpt.edu.vn |
| Nguyễn Minh Toàn | Member | toannmse170238@fpt.edu.vn |
| Nguyễn Nhật Quang | Member | quangnnse181766@fpt.edu.vn |
| Nguyễn Quốc Việt | Member | vietnqse182548@fpt.edu.vn |

---

## 📝 License

This project is licensed under the MIT License.

---

## 🤝 Contributing

Contributions are welcome! Please read our contributing guidelines first.

---

## 📞 Support

Nếu gặp vấn đề, vui lòng liên hệ:
- **Email**: vinhltse182829@fpt.edu.vn
- **Issues**: [GitHub Issues](https://github.com/your-org/SEP490_Resilience_Housing_Supply_Backend/issues)

---

**Made with ❤️ by GSU26SE51 Team**