using RHS.Application.DTOs.Apartment;
using RHS.Application.DTOs.HousingProjects;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service quản lý Căn hộ, Tầng, Sơ đồ mặt bằng và Thống kê quỹ căn NOXH.
/// </summary>
public interface IApartmentService
{
    /// <summary>
    /// Lấy danh sách căn hộ theo dự án với bộ lọc đa tiêu chí và phân trang.
    /// </summary>
    Task<PagedResultDto<ApartmentDto>> GetApartmentsAsync(
        Guid projectId,
        ApartmentFilterRequestDto filter,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy thông tin chi tiết một căn hộ theo ID.
    /// </summary>
    Task<ApartmentDto> GetApartmentByIdAsync(
        Guid projectId,
        Guid apartmentId,
        CancellationToken ct = default);

    /// <summary>
    /// Tạo mới một căn hộ trong dự án (CĐT / Admin).
    /// </summary>
    Task<ApartmentDto> CreateApartmentAsync(
        Guid projectId,
        Guid userId,
        CreateApartmentDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Tạo hàng loạt căn hộ theo sơ đồ tầng/block (Batch Creation).
    /// </summary>
    Task<List<ApartmentDto>> BatchCreateApartmentsAsync(
        Guid projectId,
        Guid userId,
        BatchCreateApartmentsRequestDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Cập nhật thông tin căn hộ (chỉ khi căn hộ chưa bị ASSIGNED).
    /// </summary>
    Task<ApartmentDto> UpdateApartmentAsync(
        Guid projectId,
        Guid apartmentId,
        Guid userId,
        UpdateApartmentDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Xóa căn hộ khỏi dự án (chỉ khi căn hộ ở trạng thái AVAILABLE).
    /// </summary>
    Task<bool> DeleteApartmentAsync(
        Guid projectId,
        Guid apartmentId,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy sơ đồ mặt bằng dự án gom nhóm theo Block và Tầng (Floor Plan).
    /// </summary>
    Task<FloorPlanResponseDto> GetFloorPlanAsync(
        Guid projectId,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy thống kê phân bổ quỹ căn (Ưu tiên, Tiêu chuẩn, Trống, Đã cấp, Theo loại phòng, Theo tầng).
    /// </summary>
    Task<ApartmentStatisticsResponseDto> GetApartmentStatisticsAsync(
        Guid projectId,
        CancellationToken ct = default);

    /// <summary>
    /// Xuất file mẫu Excel (.xlsx) để CĐT tải về điền danh sách căn hộ.
    /// </summary>
    Task<byte[]> GenerateApartmentExcelTemplateAsync(
        Guid projectId,
        CancellationToken ct = default);

    /// <summary>
    /// Nhập danh sách căn hộ tự động từ file Excel (.xlsx).
    /// </summary>
    Task<ApartmentExcelImportResultDto> ImportApartmentsFromExcelAsync(
        Guid projectId,
        Guid userId,
        Microsoft.AspNetCore.Http.IFormFile file,
        CancellationToken ct = default);
}
