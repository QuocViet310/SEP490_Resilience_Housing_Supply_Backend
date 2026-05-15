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
- 🎫 **JWT Access Token** & **Refresh Token**
- 👥 **Role-based Authorization** (Applicant, Officer, Admin)
- 🔒 **BCrypt Password Hashing**
- 🔄 **Token Refresh & Revocation**

### 🔜 Tính Năng Sắp Tới

- 📍 **Khám Phá Dự Án**: Dashboard thống nhất với bản đồ tương tác
- 📝 **Đăng Ký Trực Tuyến**: Nộp đơn và upload tài liệu số
- 🤖 **Xác Minh AI**: OCR và xác thực tài liệu tự động
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

### Documentation
- **Swagger/OpenAPI** - API documentation

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

Xem hướng dẫn chi tiết trong [AUTH_SETUP_GUIDE.md](AUTH_SETUP_GUIDE.md)

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

- **[QUICK_START.md](QUICK_START.md)** - Bắt đầu nhanh trong 5 phút
- **[AUTH_SETUP_GUIDE.md](AUTH_SETUP_GUIDE.md)** - Hướng dẫn cài đặt Authentication chi tiết
- **[PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)** - Cấu trúc dự án và kiến trúc
- **[Swagger UI](https://localhost:7000/swagger)** - API documentation tương tác

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
- Thông tin người dùng
- Email, Password (hashed), Role
- Google OAuth integration

### RefreshTokens Table
- JWT refresh tokens
- Token rotation & revocation

### OtpCodes Table
- OTP verification codes
- Purpose-based (Registration, PasswordReset)

Chi tiết: [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)

---

## 🔐 API Endpoints

### Public Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/register` | Đăng ký tài khoản |
| POST | `/api/auth/verify-otp` | Xác thực OTP |
| POST | `/api/auth/login` | Đăng nhập |
| POST | `/api/auth/google-login` | Đăng nhập Google |
| POST | `/api/auth/refresh-token` | Refresh access token |
| POST | `/api/auth/resend-otp` | Gửi lại OTP |

### Protected Endpoints (Require JWT)

| Method | Endpoint | Description | Role |
|--------|----------|-------------|------|
| GET | `/api/users/profile` | Lấy thông tin profile | All |
| GET | `/api/users/test-auth` | Test authentication | All |
| GET | `/api/users/admin-only` | Admin endpoint | Admin |
| GET | `/api/users/officer-only` | Officer endpoint | Officer |
| POST | `/api/auth/logout` | Đăng xuất | All |

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