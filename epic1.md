Epic 1: Cập nhật Database & Entity (Schema Migration)
Task 1.1: Refactor Entity HousingProject và User
-Mô tả: Xóa các cột địa chỉ cũ và bổ sung các cột mới phục vụ chuẩn hóa dữ liệu hành chính và bốc thăm.
-Chi tiết công việc (Sub-tasks):
    -HousingProject: Xóa cột Address. Thêm các cột: Street (String), Ward (String), District (String), Province (String), LotteryDate (DateTime), LotteryLocation (String), DepositAmount (Decimal/Double - Tiền cọc).
-User: Thêm cột ResidentWard (String) và ManagedWard (String).
-Tạo Migration và Update Database.
Task 1.2: Cập nhật Entity HousingApplication & Tạo bảng PrincipleAgreement
-Mô tả: Bổ sung trường quản lý suất mua và tạo bảng lưu trữ hợp đồng.
-Chi tiết công việc (Sub-tasks):
    -HousingApplication: Thêm cột SlotCode (String - Mã suất bốc thăm).
    -Tạo mới bảng PrincipleAgreement: Lưu thông tin hợp đồng (Id, ApplicationId, PdfUrl/FilePath, CreatedAt).
-Tạo Migration và Update Database.