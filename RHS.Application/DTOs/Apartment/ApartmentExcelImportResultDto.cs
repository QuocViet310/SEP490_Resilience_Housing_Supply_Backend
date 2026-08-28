namespace RHS.Application.DTOs.Apartment;

/// <summary>
/// DTO Kết quả sau khi nhập danh sách căn hộ từ file Excel.
/// </summary>
public class ApartmentExcelImportResultDto
{
    /// <summary>Tổng số dòng dữ liệu đọc được từ file Excel</summary>
    public int TotalRows { get; set; }

    /// <summary>Số lượng căn hộ nhập thành công vào hệ thống</summary>
    public int SuccessCount { get; set; }

    /// <summary>Số lượng dòng dữ liệu bị lỗi</summary>
    public int FailedCount { get; set; }

    /// <summary>Thông báo tóm tắt kết quả</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Danh sách chi tiết các dòng bị lỗi (nếu có)</summary>
    public List<ApartmentExcelRowErrorDto> Errors { get; set; } = new();

    /// <summary>Danh sách căn hộ đã tạo thành công</summary>
    public List<ApartmentDto> Data { get; set; } = new();
}

/// <summary>
/// Chi tiết lỗi tại một dòng trong file Excel.
/// </summary>
public class ApartmentExcelRowErrorDto
{
    /// <summary>Vị trí dòng trong file Excel (1-indexed, tính cả header)</summary>
    public int Row { get; set; }

    /// <summary>Mã / Tên căn hộ của dòng đó</summary>
    public string? UnitName { get; set; }

    /// <summary>Nội dung lỗi chi tiết</summary>
    public string ErrorMessage { get; set; } = string.Empty;
}
