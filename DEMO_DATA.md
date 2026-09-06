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

### 10 Tài khoản người dân mẫu đã xác minh danh tính đầy đủ (Dùng test hệ thống)

Tất cả đều có mật khẩu: `123456`, trạng thái `Active`, **đã hoàn tất eKYC & xác thực CCCD đầy đủ** (họ tên, CCCD, ngày sinh, giới tính, quê quán, địa chỉ, nghề nghiệp, tình trạng hôn nhân, thu nhập, nhóm ưu tiên), **chưa gắn hồ sơ nào** (có thể dùng để test nộp hồ sơ trực tiếp vào bất kỳ dự án nào):

| Email | Họ và tên | Giới tính | Số CCCD | Số ĐT | Ngày sinh | Quê quán | Địa chỉ thường trú | Nghề nghiệp | Thu nhập/tháng | Nhóm ưu tiên |
|---|---|---|---|---|---|---|---|---|---|---|
| `dan.test01@rhs.local` | Nguyễn Văn An | Nam | `079095000001` | `0908000001` | 15/01/1995 | Hà Nội | 123 Nguyễn Thị Minh Khai, P. Bến Thành, Q.1, TP.HCM | Công nhân | 8.500.000 đ | Hộ nghèo đô thị |
| `dan.test02@rhs.local` | Trần Thị Bình | Nữ | `079093000002` | `0908000002` | 20/04/1993 | Nam Định | 456 Lê Duẩn, P. Bến Nghé, Q.1, TP.HCM | Nhân viên văn phòng | 9.000.000 đ | Hộ cận nghèo |
| `dan.test03@rhs.local` | Lê Hoàng Cường | Nam | `079090000003` | `0908000003` | 10/09/1990 | Đà Nẵng | 789 Điện Biên Phủ, P. 25, Bình Thạnh, TP.HCM | Kỹ thuật viên | 9.500.000 đ | Thu nhập thấp đô thị |
| `dan.test04@rhs.local` | Phạm Thị Dung | Nữ | `079096000004` | `0908000004` | 05/12/1996 | Nghệ An | 101 Võ Văn Ngân, P. Linh Chiểu, TP. Thủ Đức, TP.HCM | Công nhân KCN | 10.000.000 đ | Công nhân / NLĐ KCN |
| `dan.test05@rhs.local` | Hoàng Văn Em | Nam | `079088000005` | `0908000005` | 25/03/1988 | Thanh Hóa | 202 Quang Trung, P. 10, Gò Vấp, TP.HCM | Lao động tự do | 7.800.000 đ | Hộ nghèo đô thị |
| `dan.test06@rhs.local` | Võ Thị Hạnh | Nữ | `079097000006` | `0908000006` | 18/06/1997 | Long An | 303 Cách Mạng Tháng 8, P. 12, Q.10, TP.HCM | Nhân viên bán hàng | 8.900.000 đ | Thu nhập thấp đô thị |
| `dan.test07@rhs.local` | Đặng Quốc Hùng | Nam | `079091000007` | `0908000007` | 30/11/1991 | Bình Định | 404 Hoàng Văn Thụ, P. 4, Tân Bình, TP.HCM | Kỹ sư công nghệ | 10.500.000 đ | Công nhân / NLĐ KCN |
| `dan.test08@rhs.local` | Bùi Mai Linh | Nữ | `079094000008` | `0908000008` | 14/08/1994 | Tiền Giang | 505 Nguyễn Văn Linh, P. Tân Phong, Q.7, TP.HCM | Kế toán viên | 9.200.000 đ | Hộ cận nghèo |
| `dan.test09@rhs.local` | Ngô Thanh Nam | Nam | `079092000009` | `0908000009` | 28/02/1992 | Cần Thơ | 606 Kinh Dương Vương, P. An Lạc, Bình Tân, TP.HCM | Tài xế công nghệ | 8.000.000 đ | Hộ nghèo đô thị |
| `dan.test10@rhs.local` | Đỗ Phương Oanh | Nữ | `079099000010` | `0908000010` | 08/10/1999 | Đồng Nai | 707 Nguyễn Oanh, P. 17, Gò Vấp, TP.HCM | Dược sĩ | 8.300.000 đ | Thu nhập thấp đô thị |

## Code

- `RHS.Infrastructure/Seed/DemoDataSeeder.cs`
- Gọi từ `RHS.API/Program.cs` sau migrate + PolicyConfig

