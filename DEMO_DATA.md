# Demo data (seed khi chạy API)

Mỗi lần start `RHS.API`, `DemoDataSeeder` chạy **idempotent** (không nhân đôi).

## Tài khoản staff

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Housing Developer (CĐT) | `cdt.demo@rhs.local` | `Demo@123456` |
| Department Of Construction (SXD) | `sxd.demo@rhs.local` | `Demo@123456` |

Người dân: đăng ký tài khoản mới trên mobile rồi eKYC + nộp hồ sơ vào dự án **OPEN**.

## Dự án (chỉ **Thành phố Hồ Chí Minh**)

Format địa chỉ khớp `assets/tinh_thanh.json` + `quan_huyen.json`:
- `Province` = `Thành phố Hồ Chí Minh`
- `District` = `name_with_type` (vd `Thành phố Thủ Đức`, `Quận Bình Tân`, `Huyện Nhà Bè`)

| Tên | Status | District | Mục đích |
|---|---|---|---|
| NOXH Bình Minh — Thủ Đức | OPEN | Thành phố Thủ Đức | 80 suất |
| NOXH An Phú — Thủ Đức | OPEN | Thành phố Thủ Đức | 120 suất |
| NOXH Bình Tân — An Lạc | OPEN | Quận Bình Tân | 50 suất |
| NOXH Phước Long B — Thủ Đức | OPEN | Thành phố Thủ Đức | 30 suất (oversubscribe) |
| NOXH Tân Phú — Sắp mở | UPCOMING | Quận Tân Phú | Đ38.1.b |
| NOXH Nhà Bè — Đã đóng | CLOSED | Huyện Nhà Bè | Đã đóng |

Tất cả dự án gắn CĐT demo, đã `IsConfirmed`, có `PublicAnnounceAt` đủ ngày (trừ UPCOMING), có `HousingQuota` cho `URBAN_POOR` / `URBAN_NEAR_POOR`.

## Code

- `RHS.Infrastructure/Seed/DemoDataSeeder.cs`
- Gọi từ `RHS.API/Program.cs` sau migrate + PolicyConfig
