using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RHS.Application.DTOs.Reports;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;

namespace RHS.API.Controllers;

/// <summary>
/// API Kết xuất Báo cáo Excel/PDF phục vụ Sở Xây dựng & Chủ đầu tư
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{RoleConstants.DepartmentOfConstruction},{RoleConstants.HousingDeveloper},{RoleConstants.SystemAdministrator},{RoleConstants.HousingAuthorityOfficer}")]
public class ReportsController : ControllerBase
{
    private readonly IReportExportService _reportExportService;

    public ReportsController(IReportExportService reportExportService)
    {
        _reportExportService = reportExportService;
    }

    /// <summary>
    /// Xuất file Excel danh sách hồ sơ nhà ở xã hội (có bộ lọc).
    /// </summary>
    [HttpPost("applications/excel")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportApplicationsExcel([FromBody] ExportApplicationFilterDto filter)
    {
        var bytes = await _reportExportService.ExportApplicationsExcelAsync(filter);
        var fileName = $"BaoCao_HoSo_NOXH_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Xuất file PDF danh sách hồ sơ nhà ở xã hội (có bộ lọc).
    /// </summary>
    [HttpPost("applications/pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportApplicationsPdf([FromBody] ExportApplicationFilterDto filter)
    {
        var bytes = await _reportExportService.ExportApplicationsPdfAsync(filter);
        var fileName = $"BaoCao_HoSo_NOXH_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    /// <summary>
    /// Xuất file Excel đối soát hậu kiểm trùng lặp CCCD cho Sở Xây dựng.
    /// </summary>
    [HttpGet("post-check/excel")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPostCheckExcel([FromQuery] Guid? projectId)
    {
        var bytes = await _reportExportService.ExportPostCheckExcelAsync(projectId);
        var fileName = $"BaoCao_HauKiem_CCCD_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Xuất file Excel danh sách kết quả bốc thăm / phân suất căn hộ theo dự án (Điều 44).
    /// </summary>
    [HttpGet("lottery-results/{projectId:guid}/excel")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportLotteryResultsExcel(Guid projectId)
    {
        var bytes = await _reportExportService.ExportLotteryResultsExcelAsync(projectId);
        var fileName = $"KetQua_BocTham_{projectId.ToString()[..8]}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>
    /// Xuất file PDF danh sách kết quả bốc thăm / phân suất căn hộ theo dự án (Điều 44).
    /// </summary>
    [HttpGet("lottery-results/{projectId:guid}/pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportLotteryResultsPdf(Guid projectId)
    {
        var bytes = await _reportExportService.ExportLotteryResultsPdfAsync(projectId);
        var fileName = $"KetQua_BocTham_{projectId.ToString()[..8]}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    /// <summary>
    /// Xuất file Excel báo cáo tổng hợp tiến độ và quản lý các dự án NOXH.
    /// </summary>
    [HttpGet("projects-summary/excel")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportProjectsSummaryExcel()
    {
        var bytes = await _reportExportService.ExportProjectsSummaryExcelAsync();
        var fileName = $"BaoCao_TongHop_DuAn_NOXH_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
