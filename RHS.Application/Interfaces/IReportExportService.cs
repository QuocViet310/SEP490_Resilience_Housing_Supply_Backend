using System;
using System.Threading.Tasks;
using RHS.Application.DTOs.Reports;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service chịu trách nhiệm xuất báo cáo dạng Excel (.xlsx) và PDF (.pdf) cho Sở Xây dựng và Chủ đầu tư.
/// </summary>
public interface IReportExportService
{
    /// <summary>
    /// Xuất file Excel danh sách hồ sơ nhà ở xã hội theo bộ lọc.
    /// </summary>
    Task<byte[]> ExportApplicationsExcelAsync(ExportApplicationFilterDto filter);

    /// <summary>
    /// Xuất file PDF danh sách hồ sơ nhà ở xã hội theo bộ lọc.
    /// </summary>
    Task<byte[]> ExportApplicationsPdfAsync(ExportApplicationFilterDto filter);

    /// <summary>
    /// Xuất file Excel hậu kiểm trùng lặp CCCD cho Sở Xây dựng.
    /// </summary>
    Task<byte[]> ExportPostCheckExcelAsync(Guid? projectId = null);

    /// <summary>
    /// Xuất file Excel danh sách kết quả bốc thăm / phân suất căn hộ (Điều 44).
    /// </summary>
    Task<byte[]> ExportLotteryResultsExcelAsync(Guid projectId);

    /// <summary>
    /// Xuất file PDF danh sách kết quả bốc thăm / phân suất căn hộ (Điều 44).
    /// </summary>
    Task<byte[]> ExportLotteryResultsPdfAsync(Guid projectId);

    /// <summary>
    /// Xuất file Excel báo cáo tổng hợp tiến độ các dự án NOXH.
    /// </summary>
    Task<byte[]> ExportProjectsSummaryExcelAsync();
}
