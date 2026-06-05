# 🏠 Resilience Housing Supply - Backend API

**Intelligent Social Housing Coordination & Vetting Platform**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoft-sql-server)](https://www.microsoft.com/sql-server)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## 📖 Giới Thiệu

**Resilience Housing Supply (RHS)** là nền tảng công nghệ chuyên biệt phát triển hạ tầng số cho công bằng xã hội. Chúng tôi tạo ra "Nền tảng Điều phối & Thẩm định Nhà ở Xã hội Thông minh" kết nối cơ quan quản lý nhà ở, nhà phát triển tư nhân và công dân có thu nhập thấp.

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
- 👥 **Role-based Authorization** (Guest, Applicant, Officer, Admin)
- 🔒 **BCrypt Password Hashing**
- 🔄 **Token Refresh & Revocation**
- 🔑 **Forgot Password** & **Reset Password** với OTP
- 🔐 **Change Password** (authenticated users)

### ✅ Profile Management (Đã Hoàn Thành)

- 👤 **Get Profile** - Xem thông tin cá nhân
- ✏️ **Update Profile** - Cập nhật fullName, phoneNumber, dateOfBirth, address
- 📸 **Upload Profile Image** - Upload ảnh đại diện lên Cloudinary
- 🗑️ **Delete Profile Image** - Xóa ảnh đại diện
- ❌ **Delete Account** - Xóa tài khoản (soft delete)

### ✅ eKYC — Xác Minh Danh Tính Điện Tử (Đã Hoàn Thành)

- 🪪 **OCR Căn cước công dân** — Trích xuất tự động thông tin từ ảnh CCCD (FPT AI)
- 🤝 **Face Match** — So khớp khuôn mặt selfie với ảnh trên CCCD (FPT AI)
- 👁️ **Liveness Detection** — Phát hiện giả mạo: ảnh in, ảnh màn hình, mặt nạ (FPT AI)
- 🛡️ **File Validation** — Kiểm tra magic bytes, MIME type, dung lượng ≤ 5 MB
- ⚙️ **Clean Architecture** — IOptions, IHttpClientFactory, named HttpClient, custom exceptions

### 🔜 Tính Năng Sắp Tới

- 📍 **Khám Phá Dự Án**: Dashboard thống nhất với bản đồ tương tác
- 📝 **Đăng Ký Trực Tuyến**: Nộp đơn và upload tài liệu số
- 📊 **Theo Dõi Đơn**: Tracking trạng thái real-time
- 🗺️ **Phân Bổ Theo Vùng**: Điều phối cung cấp dựa trên nhu cầu khu vực
- 📨 **Thông Báo Tích Hợp**: OTP và cập nhật real-time

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

### Cloud Services
- **Cloudinary** — Image storage & CDN
- **SMTP Email** — OTP verification
- **FPT AI** — eKYC (OCR, Face Match, Liveness Detection)

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
- 4 roles: Guest, Applicant, Officer, Admin
- Seeded automatically on database creation

### RefreshTokens Table
- JWT refresh tokens
- Token rotation & revocation
- 7 days expiration

### OtpVerifications Table
- OTP verification codes (6 digits)
- 5 minutes expiration
- Email verification & password reset

Chi tiết: [SCHEMA_UPDATE_SUMMARY.md](SCHEMA_UPDATE_SUMMARY.md)

---

## 🔐 API Endpoints

### Public Endpoints (Authentication)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Đăng ký tài khoản với email/password |
| POST | `/api/auth/verify-email` | Xác thực email với OTP |
| POST | `/api/auth/login` | Đăng nhập email/password |
| POST | `/api/auth/google-login` | Đăng nhập Google OAuth |
| POST | `/api/auth/refresh-token` | Refresh access token |
| POST | `/api/auth/forgot-password` | Quên mật khẩu (gửi OTP) |
| POST | `/api/auth/reset-password` | Reset mật khẩu với OTP |

### Protected Endpoints (Require JWT)

#### Profile Management
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users/profile` | Lấy thông tin profile |
| PUT | `/api/users/profile` | Cập nhật profile |
| POST | `/api/users/profile/image` | Upload ảnh đại diện |
| DELETE | `/api/users/profile/image` | Xóa ảnh đại diện |
| POST | `/api/users/delete-account` | Xóa tài khoản |

#### Account Management
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/change-password` | Đổi mật khẩu |
| POST | `/api/auth/logout` | Đăng xuất |

#### Role-Based Endpoints
| Method | Endpoint | Role Required |
|--------|----------|---------------|
| GET | `/api/users/admin-only` | Admin |
| GET | `/api/users/officer-only` | Officer |

#### eKYC — Xác Minh Danh Tính
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/ekyc/ocr` | OCR Căn cước công dân (multipart: `image`) |
| POST | `/api/ekyc/face-match` | So khớp khuôn mặt (multipart: `faceImage`, `idCardImage`) |
| POST | `/api/ekyc/liveness` | Kiểm tra liveness chống spoofing (multipart: `faceImage`) |

#### Payment (VNPay)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/payment/create-payment-url` | Tạo URL thanh toán VNPay |
| GET | `/api/payment/payment-callback` | Callback VNPay (AllowAnonymous) |
| GET | `/api/payment/payment-info/{orderId}` | Tra cứu giao dịch |
| GET | `/api/payment/my-payments` | Lịch sử thanh toán của user |

**Tổng cộng: 21 API endpoints** ✅

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