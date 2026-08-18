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

### 11 Tài khoản người dân đã xác minh danh tính (Dùng test hệ thống)

Tất cả đều có mật khẩu: `123456`, trạng thái `Active`, đã có thông tin CCCD, Ngày sinh, Địa chỉ, SĐT, chưa gắn hồ sơ nào (có thể dùng để test nộp hồ sơ vào bất kỳ dự án nào):

| Email | Họ và tên | Số CCCD | Số điện thoại | Ngày sinh | Địa chỉ |
|---|---|---|---|---|---|
| `dan.test01@rhs.local` | Nguyễn Văn An | `079095000001` | `0908000001` | 15/01/1995 | 123 Nguyễn Thị Minh Khai, P. Bến Thành, Q.1, TP.HCM |
| `dan.test02@rhs.local` | Trần Thị Bình | `079093000002` | `0908000002` | 20/04/1993 | 456 Lê Duẩn, P. Bến Nghé, Q.1, TP.HCM |
| `dan.test03@rhs.local` | Lê Hoàng Cường | `079090000003` | `0908000003` | 10/09/1990 | 789 Điện Biên Phủ, P. 25, Bình Thạnh, TP.HCM |
| `dan.test04@rhs.local` | Phạm Thị Dung | `079096000004` | `0908000004` | 05/12/1996 | 101 Võ Văn Ngân, P. Linh Chiểu, TP. Thủ Đức, TP.HCM |
| `dan.test05@rhs.local` | Hoàng Văn Em | `079088000005` | `0908000005` | 25/03/1988 | 202 Quang Trung, P. 10, Gò Vấp, TP.HCM |
| `dan.test06@rhs.local` | Võ Thị Hạnh | `079097000006` | `0908000006` | 18/06/1997 | 303 Cách Mạng Tháng 8, P. 12, Q.10, TP.HCM |
| `dan.test07@rhs.local` | Đặng Quốc Hùng | `079091000007` | `0908000007` | 30/11/1991 | 404 Hoàng Văn Thụ, P. 4, Tân Bình, TP.HCM |
| `dan.test08@rhs.local` | Bùi Mai Linh | `079094000008` | `0908000008` | 14/08/1994 | 505 Nguyễn Văn Linh, P. Tân Phong, Q.7, TP.HCM |
| `dan.test09@rhs.local` | Ngô Thanh Nam | `079092000009` | `0908000009` | 28/02/1992 | 606 Kinh Dương Vương, P. An Lạc, Bình Tân, TP.HCM |
| `dan.test10@rhs.local` | Đỗ Phương Oanh | `079099000010` | `0908000010` | 08/10/1999 | 707 Nguyễn Oanh, P. 17, Gò Vấp, TP.HCM |
| `dan.test11@rhs.local` | Trần Quốc Phong | `079098000011` | `0908000011` | 20/05/1998 | 808 Phạm Văn Đồng, P. 1, Gò Vấp, TP.HCM |

## Code

- `RHS.Infrastructure/Seed/DemoDataSeeder.cs`
- Gọi từ `RHS.API/Program.cs` sau migrate + PolicyConfig

