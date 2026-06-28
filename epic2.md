Epic 2: Logic API & Giữ chỗ (Unit Holding)
Task 2.1: API Danh sách Dự án (Home Page Sorting)
-Mô tả: Cập nhật API GET List Housing Projects để hỗ trợ sắp xếp thông minh dựa theo profile người dân, phần tiếp nhận hồ sơ là bên nào có mã phường trùng với mã phường của housingproject.
-Tiêu chí nghiệm thu (AC):
    -Nhận thông tin User đang đăng nhập từ Token.
    -Thêm thuật toán Order By: Ưu tiên các dự án có HousingProject.Ward == User.ResidentWard lên đầu danh sách trả về. Các dự án khác xếp sau.
Task 2.2: Logic Trừ Quota (Giữ chỗ) - Maker-Checker Flow
-Mô tả: Xử lý bài toán giam giữ số lượng căn hộ (Unit) khi Ward Manager (Checker) duyệt đơn và nhả lại nếu người dùng bom/không thanh toán.
-Tiêu chí nghiệm thu (AC):
    -VO (Maker) chỉ được PROPOSE hồ sơ sang PROPOSED. KHÔNG trigger thay đổi AvailableUnits.
    -Khi Ward Manager (Checker) gọi API duyệt đơn (Trạng thái chuyển PROPOSED → APPROVED): Tự động trừ Unit = Unit - 1.
    -Gắn thời hạn thanh toán 24h (tính từ FinalDecisionDate).
    -Gợi ý cho BE: Dùng Background Job (Hangfire/Quartz) hoặc TTL (Redis) để đếm ngược. Nếu hết hạn mà hệ thống chưa nhận được tiền từ VNPay -> Cập nhật đơn thành CANCELED hoặc EXPIRED -> Cộng lại Unit = Unit + 1.