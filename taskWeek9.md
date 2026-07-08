PHẦN 1: CẬP NHẬT CORE & MAP MÔ HÌNH DỮ LIỆU
Yêu cầu cốt lõi: Cập nhật lại toàn bộ Role và Enum Trạng thái trong hệ thống để đồng bộ với nghiệp vụ pháp lý thực tế.
-Mapping Role:
    +Đổi VerificationOfficer (VO) thành HousingDeveloper (Chủ đầu tư - CĐT).
    +Đổi WardManager (WM) thành DepartmentOfConstruction (Sở Xây dựng - SXD).
-10 Trạng thái Hồ sơ (ApplicationStatus Enum):
    +DRAFT: Nháp (Applicant đang soạn).
    +SUBMITTED: Đã nộp, chờ CĐT tiếp nhận.
    +REVIEWING: CĐT đang thẩm định hồ sơ.
    +NEED_MORE_DOCUMENTS: CĐT yêu cầu Applicant bổ sung giấy tờ.
    +PENDING_SXD_REVIEW: CĐT đã chốt danh sách, gửi lên Sở Xây dựng (Hồ sơ bị khóa với CĐT).
    +APPROVED: Sở Xây dựng đã phê duyệt (Mở khóa thanh toán đặt cọc).
    +DEPOSIT_PAID: Đã đặt cọc thành công (Hoàn tất).
    +REJECTED: Bị từ chối (Bởi CĐT hoặc SXD - Bắt buộc có lý do).
    +CANCELED: Người dân tự hủy.
    +EXPIRED: Quá 24h không thanh toán đặt cọc.
PHẦN 2: QUẢN LÝ DỰ ÁN (HOUSING PROJECTS)
TASK #1: Cập nhật Entity và Ràng buộc Dữ liệu Dự án
-Mô tả: Chuyển đổi cách lưu trữ pháp lý dự án từ file cứng sang dữ liệu có cấu trúc.
-Phạm vi xử lý:
    +Thêm các cột vào Entity HousingProject: DecisionNumber (nvarchar), ApprovalDate (datetime), IsConfirmed (bit), ApplicationOpenDate (datetime), ApplicationCloseDate (datetime).
    +Seed thêm 2 trạng thái dự án mới vào HousingProjectStatuses: PENDING và REJECTED.
    +Thêm Validation DTO tại API POST/PUT /api/HousingProjects:
    +IsConfirmed phải là true (Ném lỗi 400 Bad Request nếu false).
    +DecisionNumber không được blank.
    +ApplicationOpenDate phải nhỏ hơn ApplicationCloseDate.
TASK #2: API Phê duyệt/Từ chối Dự án (Dành cho Sở Xây dựng)
-Mô tả: Sở Xây dựng cấp phép cho dự án được hiển thị trên hệ thống.
-Phạm vi xử lý:
    +Endpoint: PATCH /api/HousingProjects/{id}/status
    +Quyền truy cập: Bắt buộc Role DepartmentOfConstruction.
    +Logic:
        *APPROVE: Chuyển status từ PENDING → UPCOMING.
        *REJECT: Chuyển status từ PENDING → REJECTED. Bắt buộc nhận param RejectReason lưu vào DB.
TASK #3: Scheduled Job tự động hóa Dự án & Quy tắc 20 ngày
-Mô tả: Dịch vụ chạy ngầm (Cron Job) vận hành thời gian dự án và áp dụng luật "Im lặng đồng ý" (Tacit Approval).
-Phạm vi xử lý:
    +Cron expression: Chạy 1 lần vào 00:00:00 hàng ngày.
    +Nhiệm vụ 1 (Vòng đời dự án):
-Nếu Status == UPCOMING & ApplicationOpenDate <= Now → Update thành OPEN.
-Nếu Status == OPEN & ApplicationCloseDate < Now → Update thành CLOSED.
    +Nhiệm vụ 2 (Luật 20 ngày của Sở Xây dựng):
-Tìm các hồ sơ (HousingApplication) có trạng thái PENDING_SXD_REVIEW.
-Nếu Now - Ngày chuyển trạng thái PENDING_SXD_REVIEW > 20 ngày → Tự động chuyển hồ sơ sang APPROVED. Lưu lịch sử (Audit log): "Tự động phê duyệt theo quy định 20 ngày".
TASK #4: API Lọc Danh sách Dự án (Filtering & Querying)
-Mô tả: Điều chỉnh Endpoint truy xuất dự án để bảo mật thông tin các dự án chưa duyệt.
-Phạm vi xử lý:
    +Endpoint: GET /api/HousingProjects
    +Global Query Filter: Luôn mặc định áp dụng IsDeleted = false.
    +Phân quyền hiển thị: Trả về các dự án UPCOMING, OPEN, CLOSED, FULL cho khách public/người mua. Các dự án PENDING/REJECTED chỉ hiển thị khi UserId truy vấn chính là chủ dự án đó, hoặc Role là DepartmentOfConstruction.
PHẦN 3: VÒNG ĐỜI HỒ SƠ ĐĂNG KÝ (MAKER - CHECKER)
TASK #5: Sinh Biên nhận PDF
-Mô tả: Tạo hồ sơ y như cũ bỏ thu nhập bổ sung các trường còn thiếu dựa trên giấy đăng ký nhà ở xã hội sau khi summit thì tạo giấy biên nhận
-Phạm vi xử lý:
    +Sinh Biên nhận: Sau khi qua vòng Validate, hồ sơ chuyển sang SUBMITTED. Hệ thống tự động sinh PDF "Giấy biên nhận nộp hồ sơ" (mang thông tin của HousingDeveloper), upload lên Blob Storage và cập nhật ReceiptUrl vào DB. (Lưu ý: Không tự động chạy AI ở bước này để tiết kiệm chi phí).