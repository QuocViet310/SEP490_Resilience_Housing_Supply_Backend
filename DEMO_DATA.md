# Demo data (seed khi chạy API)

Mỗi lần start `RHS.API`, `DemoDataSeeder` chạy **idempotent** (không nhân đôi).

## Tài khoản staff

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Housing Developer (CĐT) | `cdt.demo@rhs.local` | `123456` |
| Department Of Construction (SXD) | `sxd.demo@rhs.local` | `123456` |

Người dân: đăng ký tài khoản mới trên mobile rồi eKYC + nộp hồ sơ vào dự án **OPEN**.

## Dự án (chỉ **Thành phố Hồ Chí Minh**)

Địa giới theo [provinces.open-api.vn API v2](https://provinces.open-api.vn) (Tỉnh → Phường/Xã):

- `Province` = `Thành phố Hồ Chí Minh`
- `District` = `Ward` = tên phường/xã chuẩn v2 (vd `Phường Tân Thuận`, `Phường Thủ Đức`)

| Tên | Status | Phường/Xã (v2) | Mục đích |
|---|---|---|---|
| NOXH Bình Minh — Thủ Đức | OPEN | Phường Thủ Đức | 80 suất |
| NOXH An Phú — Thủ Đức | OPEN | Phường An Phú | 120 suất |
| NOXH Bình Tân — An Lạc | OPEN | Phường An Lạc | 50 suất |
| NOXH Phước Long — Thủ Đức | OPEN | Phường Phước Long | 30 suất (oversubscribe) |
| NOXH Nhà Ở Xã Hội — Tân Thuận | OPEN | Phường Tân Thuận | test filter phường + sort |
| NOXH Nhà Ở Xã Hội — Trung Mỹ Tây | OPEN | Phường Trung Mỹ Tây | giá thấp — test sort |
| NOXH Tân Phú — Sắp mở | UPCOMING | Phường Tân Sơn Nhì | Đ38.1.b |
| NOXH Nhà Bè — Đã đóng | CLOSED | Xã Nhà Bè | Đã đóng |

Tất cả dự án gắn CĐT demo, đã `IsConfirmed`, có `PublicAnnounceAt` đủ ngày (trừ UPCOMING), có `HousingQuota` cho `URBAN_POOR` / `URBAN_NEAR_POOR`.

### Lịch bốc thăm

- **Không seed lịch** trên bất kỳ dự án nào.
- Sau khi chốt hồ sơ / vượt số căn: CĐT đề xuất lịch ONLINE (ngày giờ + link) → Sở duyệt → thông báo người dân.

## Người dân demo (mật khẩu `123456`)

Hồ sơ gắn dự án **NOXH Bình Minh — Thủ Đức** (1 TK = 1 hồ sơ). Tài khoản trống: `dan.free@rhs.local`.

## Code

- `RHS.Infrastructure/Seed/DemoDataSeeder.cs`
- Gọi từ `RHS.API/Program.cs` sau migrate + PolicyConfig
