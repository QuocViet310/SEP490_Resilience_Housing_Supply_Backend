using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RHS.Application.DTOs.Reports;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;
using ExcelColor = System.Drawing.Color;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Service triển khai kết xuất báo cáo Excel (EPPlus) và PDF (QuestPDF) cho Sở Xây dựng & Chủ đầu tư.
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ReportExportService> _logger;

    public ReportExportService(AppDbContext dbContext, ILogger<ReportExportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    #region 1. Application List Report (Excel & PDF)

    public async Task<byte[]> ExportApplicationsExcelAsync(ExportApplicationFilterDto filter)
    {
        _logger.LogInformation("Exporting Applications Excel report with filter ProjectId={ProjectId}, Status={Status}", filter.ProjectId, filter.Status);
        var rows = await GetApplicationExportDataAsync(filter);

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Danh Sách Hồ Sơ NOXH");

        // 1. Title Block
        ws.Cells["A1:O1"].Merge = true;
        ws.Cells["A1"].Value = "SỞ XÂY DỰNG - BÁO CÁO DANH SÁCH HỒ SƠ ĐĂNG KÝ MUA/THUÊ NHÀ Ở XÃ HỘI";
        ws.Cells["A1"].Style.Font.Size = 14;
        ws.Cells["A1"].Style.Font.Bold = true;
        ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        ws.Cells["A2:O2"].Merge = true;
        ws.Cells["A2"].Value = $"Thời gian xuất báo cáo: {DateTime.Now:dd/MM/yyyy HH:mm} | Tổng số hồ sơ: {rows.Count}";
        ws.Cells["A2"].Style.Font.Italic = true;
        ws.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        // 2. Headers
        string[] headers = {
            "STT", "Mã Hồ Sơ", "Họ và Tên", "Số CCCD", "Ngày Sinh",
            "Số Điện Thoại", "Email", "Địa Chỉ", "Dự Án Đăng Ký",
            "Đối Tượng Thụ Hưởng", "Điều Kiện Nhà Ở", "Trạng Thái Hồ Sơ",
            "Ngày Nộp", "Ngày Duyệt/Thẩm Định", "Lý Do Từ Chối / Ghi Chú"
        };

        int headerRow = 4;
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cells[headerRow, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(ExcelColor.FromArgb(31, 78, 120)); // Navy blue
            cell.Style.Font.Color.SetColor(ExcelColor.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        // 3. Data Rows
        int currentRow = 5;
        foreach (var r in rows)
        {
            ws.Cells[currentRow, 1].Value = r.Index;
            ws.Cells[currentRow, 2].Value = r.ApplicationCode;
            ws.Cells[currentRow, 3].Value = r.FullName;
            ws.Cells[currentRow, 4].Value = r.CitizenId;
            ws.Cells[currentRow, 4].Style.Numberformat.Format = "@"; // Format as text to keep leading zero
            ws.Cells[currentRow, 5].Value = r.DateOfBirth;
            ws.Cells[currentRow, 6].Value = r.PhoneNumber;
            ws.Cells[currentRow, 7].Value = r.Email;
            ws.Cells[currentRow, 8].Value = r.Address;
            ws.Cells[currentRow, 9].Value = r.ProjectName;
            ws.Cells[currentRow, 10].Value = r.BeneficiaryGroup;
            ws.Cells[currentRow, 11].Value = r.HousingStatus;
            ws.Cells[currentRow, 12].Value = r.ApplicationStatus;
            ws.Cells[currentRow, 13].Value = r.SubmittedAt.ToString("dd/MM/yyyy HH:mm");
            ws.Cells[currentRow, 14].Value = r.ReviewedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-";
            ws.Cells[currentRow, 15].Value = r.RejectReason;

            for (int col = 1; col <= 15; col++)
            {
                ws.Cells[currentRow, col].Style.Border.BorderAround(ExcelBorderStyle.Thin, ExcelColor.LightGray);
            }
            currentRow++;
        }

        ws.Cells.AutoFitColumns();
        return await package.GetAsByteArrayAsync();
    }

    public async Task<byte[]> ExportApplicationsPdfAsync(ExportApplicationFilterDto filter)
    {
        _logger.LogInformation("Exporting Applications PDF report with filter ProjectId={ProjectId}", filter.ProjectId);
        var rows = await GetApplicationExportDataAsync(filter);
        var now = DateTime.Now;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(30);
                page.MarginVertical(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Lato));

                // Page Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("SỞ XÂY DỰNG TỈNH/THÀNH PHỐ").Bold().FontSize(10);
                            c.Item().Text("BỘ PHẬN THẨM ĐỊNH HỒ SƠ NOXH").FontSize(9);
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignRight().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold().FontSize(10);
                            c.Item().AlignRight().Text("Độc lập - Tự do - Hạnh phúc").Bold().FontSize(9).Underline();
                            c.Item().AlignRight().Text($"Ngày xuất: {now:dd/MM/yyyy HH:mm}").Italic().FontSize(8);
                        });
                    });

                    col.Item().PaddingTop(10).AlignCenter().Text("DANH SÁCH HỒ SƠ ĐĂNG KÝ MUA/THUÊ NHÀ Ở XÃ HỘI").Bold().FontSize(14);
                    col.Item().Height(10);
                });

                // Page Content - Table
                page.Content().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(25);  // STT
                        cols.ConstantColumn(75);  // Mã HS
                        cols.ConstantColumn(100); // Họ tên
                        cols.ConstantColumn(75);  // CCCD
                        cols.ConstantColumn(65);  // Số ĐT
                        cols.RelativeColumn(1);   // Dự án
                        cols.RelativeColumn(1);   // Đối tượng
                        cols.ConstantColumn(80);  // Trạng thái
                        cols.ConstantColumn(65);  // Ngày nộp
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background("#1F4E78").Padding(4).AlignCenter().Text("STT").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1F4E78").Padding(4).AlignCenter().Text("Mã Hồ Sơ").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1F4E78").Padding(4).Text("Họ và Tên").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1F4E78").Padding(4).AlignCenter().Text("Số CCCD").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1F4E78").Padding(4).AlignCenter().Text("Số ĐT").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1F4E78").Padding(4).Text("Dự Án").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1F4E78").Padding(4).Text("Đối Tượng").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1F4E78").Padding(4).AlignCenter().Text("Trạng Thái").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1F4E78").Padding(4).AlignCenter().Text("Ngày Nộp").Bold().FontColor("#FFFFFF");
                    });

                    foreach (var r in rows)
                    {
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.Index.ToString());
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.ApplicationCode);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).Text(r.FullName);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.CitizenId);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.PhoneNumber);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).Text(r.ProjectName);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).Text(r.BeneficiaryGroup);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.ApplicationStatus);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.SubmittedAt.ToString("dd/MM/yyyy"));
                    }
                });

                // Page Footer
                page.Footer().Row(r =>
                {
                    r.RelativeItem().Text(x =>
                    {
                        x.Span("Tổng số bản ghi: ");
                        x.Span(rows.Count.ToString()).Bold();
                    });

                    r.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Trang ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    #endregion

    #region 2. Post-Check Duplicate Report (Excel)

    public async Task<byte[]> ExportPostCheckExcelAsync(Guid? projectId = null)
    {
        _logger.LogInformation("Exporting Post-Check Excel report for ProjectId={ProjectId}", projectId);

        var query = _dbContext.HousingApplications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.HousingProject)
            .Where(a => a.ApplicationStatus == ApplicationStatusConstants.Approved || a.ApplicationStatus == ApplicationStatusConstants.DepositPaid);

        if (projectId.HasValue)
        {
            query = query.Where(a => a.ProjectId == projectId.Value);
        }

        var list = await query
            .OrderBy(a => a.Applicant.CitizenId)
            .ThenByDescending(a => a.SubmittedAt)
            .ToListAsync();

        var rows = list.Select((a, idx) => new PostCheckExportRowDto
        {
            Index = idx + 1,
            CitizenId = a.CitizenId ?? a.Applicant?.CitizenId ?? string.Empty,
            FullName = a.FullName ?? a.Applicant?.FullName ?? string.Empty,
            DateOfBirth = a.Applicant?.DateOfBirth?.ToString("dd/MM/yyyy") ?? string.Empty,
            ApplicationCode = a.ApplicationId.ToString()[..8].ToUpper(),
            ProjectName = a.HousingProject?.ProjectName ?? string.Empty,
            BeneficiaryGroup = a.PriorityGroup ?? string.Empty,
            ApplicationStatus = a.ApplicationStatus,
            ApprovedAt = a.UpdatedAt ?? a.SubmittedAt,
            IsDepositPaid = a.ApplicationStatus == ApplicationStatusConstants.DepositPaid
        }).ToList();

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Đối Soát Hậu Kiểm CCCD");

        ws.Cells["A1:J1"].Merge = true;
        ws.Cells["A1"].Value = "SỞ XÂY DỰNG - BÁO CÁO HẬU KIỂM VÀ ĐỐI SOÁT TRÙNG LẶP CCCD HƯỞNG CHÍNH SÁCH NOXH";
        ws.Cells["A1"].Style.Font.Size = 14;
        ws.Cells["A1"].Style.Font.Bold = true;
        ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        ws.Cells["A2:J2"].Merge = true;
        ws.Cells["A2"].Value = $"Căn cứ Điều 38.1.đ & 38.1.e Nghị định 100/2024/NĐ-CP | Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cells["A2"].Style.Font.Italic = true;
        ws.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        string[] headers = {
            "STT", "Số CCCD", "Họ và Tên", "Ngày Sinh", "Mã Hồ Sơ",
            "Dự Án Phê Duyệt", "Đối Tượng", "Trạng Thái", "Ngày Phê Duyệt", "Đã Đặt Cọc"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cells[4, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(ExcelColor.FromArgb(192, 0, 0)); // Dark red for audit
            cell.Style.Font.Color.SetColor(ExcelColor.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        int currentRow = 5;
        foreach (var r in rows)
        {
            ws.Cells[currentRow, 1].Value = r.Index;
            ws.Cells[currentRow, 2].Value = r.CitizenId;
            ws.Cells[currentRow, 2].Style.Numberformat.Format = "@";
            ws.Cells[currentRow, 3].Value = r.FullName;
            ws.Cells[currentRow, 4].Value = r.DateOfBirth;
            ws.Cells[currentRow, 5].Value = r.ApplicationCode;
            ws.Cells[currentRow, 6].Value = r.ProjectName;
            ws.Cells[currentRow, 7].Value = r.BeneficiaryGroup;
            ws.Cells[currentRow, 8].Value = r.ApplicationStatus;
            ws.Cells[currentRow, 9].Value = r.ApprovedAt.ToString("dd/MM/yyyy HH:mm");
            ws.Cells[currentRow, 10].Value = r.IsDepositPaid ? "Đã đặt cọc" : "Chưa đặt cọc";

            for (int col = 1; col <= 10; col++)
            {
                ws.Cells[currentRow, col].Style.Border.BorderAround(ExcelBorderStyle.Thin, ExcelColor.LightGray);
            }
            currentRow++;
        }

        ws.Cells.AutoFitColumns();
        return await package.GetAsByteArrayAsync();
    }

    #endregion

    #region 3. Lottery Results Report (Excel & PDF)

    public async Task<byte[]> ExportLotteryResultsExcelAsync(Guid projectId)
    {
        _logger.LogInformation("Exporting Lottery Results Excel for ProjectId={ProjectId}", projectId);
        var (project, rows) = await GetLotteryExportDataAsync(projectId);

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Kết Quả Bốc Thăm NOXH");

        ws.Cells["A1:I1"].Merge = true;
        ws.Cells["A1"].Value = $"BÁO CÁO KẾT QUẢ BỐC THĂM / PHÂN SUẤT NHÀ Ở XÃ HỘI (ĐIỀU 44)";
        ws.Cells["A1"].Style.Font.Size = 14;
        ws.Cells["A1"].Style.Font.Bold = true;
        ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        ws.Cells["A2:I2"].Merge = true;
        ws.Cells["A2"].Value = $"Dự án: {project?.ProjectName} | Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cells["A2"].Style.Font.Italic = true;
        ws.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        string[] headers = {
            "STT", "Mã Hồ Sơ", "Họ và Tên", "Số CCCD", "Số Điện Thoại",
            "Đối Tượng Thụ Hưởng", "Kết Quả Bốc Thăm", "Mã Suất / Căn", "Ngày Bốc Thăm"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cells[4, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(ExcelColor.FromArgb(46, 117, 89)); // Dark green
            cell.Style.Font.Color.SetColor(ExcelColor.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        int currentRow = 5;
        foreach (var r in rows)
        {
            ws.Cells[currentRow, 1].Value = r.Index;
            ws.Cells[currentRow, 2].Value = r.ApplicationCode;
            ws.Cells[currentRow, 3].Value = r.FullName;
            ws.Cells[currentRow, 4].Value = r.CitizenId;
            ws.Cells[currentRow, 4].Style.Numberformat.Format = "@";
            ws.Cells[currentRow, 5].Value = r.PhoneNumber;
            ws.Cells[currentRow, 6].Value = r.BeneficiaryGroup;
            ws.Cells[currentRow, 7].Value = r.LotteryResult;
            ws.Cells[currentRow, 8].Value = string.IsNullOrEmpty(r.SlotCode) ? "-" : r.SlotCode;
            ws.Cells[currentRow, 9].Value = r.DrawnAt?.ToString("dd/MM/yyyy HH:mm") ?? "-";

            for (int col = 1; col <= 9; col++)
            {
                ws.Cells[currentRow, col].Style.Border.BorderAround(ExcelBorderStyle.Thin, ExcelColor.LightGray);
            }
            currentRow++;
        }

        ws.Cells.AutoFitColumns();
        return await package.GetAsByteArrayAsync();
    }

    public async Task<byte[]> ExportLotteryResultsPdfAsync(Guid projectId)
    {
        _logger.LogInformation("Exporting Lottery Results PDF for ProjectId={ProjectId}", projectId);
        var (project, rows) = await GetLotteryExportDataAsync(projectId);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(35);
                page.MarginVertical(35);
                page.DefaultTextStyle(x => x.FontSize(9.5f).FontFamily(Fonts.Lato));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("SỞ XÂY DỰNG TỈNH/THÀNH PHỐ").Bold().FontSize(9.5f);
                            c.Item().Text(project?.ProjectName ?? "DỰ ÁN NOXH").Bold().FontSize(9);
                        });

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignRight().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold().FontSize(9.5f);
                            c.Item().AlignRight().Text("Độc lập - Tự do - Hạnh phúc").Bold().FontSize(9).Underline();
                        });
                    });

                    col.Item().PaddingTop(12).AlignCenter().Text("DANH SÁCH KẾT QUẢ PHÂN SUẤT / BỐC THĂM NHÀ Ở XÃ HỘI").Bold().FontSize(13);
                    col.Item().AlignCenter().Text("(Công bố công khai theo Điều 44 Nghị định 100/2024/NĐ-CP)").Italic().FontSize(8.5f);
                    col.Item().Height(10);
                });

                page.Content().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(25);  // STT
                        cols.ConstantColumn(75);  // Mã HS
                        cols.RelativeColumn(1);   // Họ tên
                        cols.ConstantColumn(80);  // CCCD
                        cols.ConstantColumn(75);  // Số ĐT
                        cols.ConstantColumn(90);  // Kết quả
                        cols.ConstantColumn(65);  // Mã suất
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background("#2E7559").Padding(4).AlignCenter().Text("STT").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2E7559").Padding(4).AlignCenter().Text("Mã HS").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2E7559").Padding(4).Text("Họ và Tên").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2E7559").Padding(4).AlignCenter().Text("Số CCCD").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2E7559").Padding(4).AlignCenter().Text("Số ĐT").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2E7559").Padding(4).AlignCenter().Text("Kết Quả").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#2E7559").Padding(4).AlignCenter().Text("Mã Căn").Bold().FontColor("#FFFFFF");
                    });

                    foreach (var r in rows)
                    {
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.Index.ToString());
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.ApplicationCode);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).Text(r.FullName);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.CitizenId);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.PhoneNumber);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(r.LotteryResult);
                        table.Cell().BorderBottom(1).BorderColor("#E0E0E0").Padding(3).AlignCenter().Text(string.IsNullOrEmpty(r.SlotCode) ? "-" : r.SlotCode);
                    }
                });

                page.Footer().Column(c =>
                {
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"Tổng số người tham gia: {rows.Count}");
                        r.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Trang ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportLotteryMinutesPdfAsync(Guid projectId)
    {
        _logger.LogInformation("Exporting Lottery Minutes PDF for ProjectId={ProjectId}", projectId);

        var project = await _dbContext.HousingProjects
            .AsNoTracking()
            .Include(p => p.Developer)
            .Include(p => p.LotterySupervisor)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        var draw = await _dbContext.LotteryDraws
            .AsNoTracking()
            .Include(d => d.DrawnByUser)
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.DrawnAt)
            .FirstOrDefaultAsync();

        var (_, rows) = await GetLotteryExportDataAsync(projectId);
        var winners = rows.Where(r =>
            r.LotteryResult is "WON" or "PRIORITY_WON").ToList();
        var losers = rows.Where(r => r.LotteryResult == "LOST").ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Lato));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold();
                    col.Item().AlignCenter().Text("Độc lập - Tự do - Hạnh phúc").Bold().Underline();
                    col.Item().PaddingTop(12).AlignCenter()
                        .Text("BIÊN BẢN PHIÊN BỐC THĂM NHÀ Ở XÃ HỘI").Bold().FontSize(14);
                    col.Item().AlignCenter().Text(project?.ProjectName ?? "").Italic();
                });

                page.Content().PaddingTop(16).Column(col =>
                {
                    col.Item().Text($"Ngày giờ tổ chức: {project?.LotteryDate:dd/MM/yyyy HH:mm} (UTC)");
                    col.Item().Text($"Hình thức: {project?.LotteryType ?? "-"}");
                    col.Item().Text($"Địa điểm / kênh: {project?.LotteryLocation ?? "-"}");
                    col.Item().Text($"Trạng thái phiên: {project?.LotterySessionStatus ?? "-"}");
                    col.Item().Text($"Chủ đầu tư: {project?.Developer?.FullName ?? "-"}");
                    col.Item().Text(
                        $"Đại diện Sở giám sát: {project?.LotterySupervisor?.FullName ?? "(chưa ghi nhận)"}" +
                        (project?.LotterySupervisedAt != null
                            ? $" — có mặt từ {project.LotterySupervisedAt:dd/MM/yyyy HH:mm:ss} UTC"
                            : ""));
                    if (draw != null)
                    {
                        col.Item().PaddingTop(6).Text($"Mã phiên (DrawId): {draw.DrawId}");
                        col.Item().Text($"Thời điểm chốt: {draw.DrawnAt:dd/MM/yyyy HH:mm:ss} UTC");
                        col.Item().Text($"Người điều hành: {draw.DrawnByUser?.FullName ?? draw.DrawnBy.ToString()}");
                        col.Item().Text($"Số người tham gia: {draw.TotalParticipants} | Trúng: {draw.PriorityAllocated + draw.RandomAllocated}");
                    }

                    col.Item().PaddingTop(12).Text("I. DANH SÁCH TRÚNG TUYỂN").Bold();
                    foreach (var (w, i) in winners.Select((w, i) => (w, i + 1)))
                        col.Item().Text($"{i}. {w.FullName} — CCCD {w.CitizenId} — {w.LotteryResult} — {w.SlotCode}");

                    if (winners.Count == 0)
                        col.Item().Text("(Không có)").Italic();

                    col.Item().PaddingTop(12).Text("II. DANH SÁCH KHÔNG TRÚNG").Bold();
                    foreach (var (l, i) in losers.Select((l, i) => (l, i + 1)))
                        col.Item().Text($"{i}. {l.FullName} — CCCD {l.CitizenId}");

                    if (losers.Count == 0)
                        col.Item().Text("(Không có)").Italic();

                    col.Item().PaddingTop(20).Text(
                        "Biên bản được lập tự động từ hệ thống RHS sau khi kết thúc/công bố phiên bốc thăm, phục vụ giám sát minh bạch.")
                        .Italic().FontSize(9);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Trang ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    #endregion

    #region 4. Projects Summary Report (Excel)

    public async Task<byte[]> ExportProjectsSummaryExcelAsync()
    {
        _logger.LogInformation("Exporting Projects Summary Excel report");

        var projects = await _dbContext.HousingProjects
            .AsNoTracking()
            .Include(p => p.Developer)
            .Include(p => p.HousingProjectStatus)
            .Include(p => p.HousingApplications)
            .ToListAsync();

        var rows = projects.Select((p, idx) => new ProjectSummaryExportRowDto
        {
            Index = idx + 1,
            ProjectName = p.ProjectName,
            DeveloperName = p.Developer?.FullName ?? "-",
            DecisionNumber = p.DecisionNumber ?? "-",
            Address = $"{p.Street}, {p.Ward}, {p.District}, {p.Province}",
            TotalUnits = p.AvailableUnits,
            AvailableUnits = p.AvailableUnits,
            TotalApplications = p.HousingApplications?.Count ?? 0,
            ApprovedApplications = p.HousingApplications?.Count(a => a.ApplicationStatus == ApplicationStatusConstants.Approved) ?? 0,
            DepositPaidApplications = p.HousingApplications?.Count(a => a.ApplicationStatus == ApplicationStatusConstants.DepositPaid) ?? 0,
            ProjectStatus = p.HousingProjectStatus?.StatusName ?? "UNKNOWN",
            ApplicationOpenDate = p.ApplicationOpenDate?.ToString("dd/MM/yyyy") ?? "-",
            ApplicationCloseDate = p.ApplicationCloseDate?.ToString("dd/MM/yyyy") ?? "-"
        }).ToList();

        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Tổng Hop Dự Án NOXH");

        ws.Cells["A1:M1"].Merge = true;
        ws.Cells["A1"].Value = "SỞ XÂY DỰNG - BÁO CÁO TỔNG HỢP TIẾN ĐỘ VÀ QUẢN LÝ DỰ ÁN NHÀ Ở XÃ HỘI";
        ws.Cells["A1"].Style.Font.Size = 14;
        ws.Cells["A1"].Style.Font.Bold = true;
        ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        ws.Cells["A2:M2"].Merge = true;
        ws.Cells["A2"].Value = $"Thời gian kết xuất: {DateTime.Now:dd/MM/yyyy HH:mm} | Tổng số dự án: {rows.Count}";
        ws.Cells["A2"].Style.Font.Italic = true;
        ws.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        string[] headers = {
            "STT", "Tên Dự Án", "Chủ Đầu Tư", "Số QĐ Phê Duyệt", "Địa Chỉ",
            "Tổng Số Căn", "Căn Còn Lại", "Tổng Hồ Sơ Nộp", "Hồ Sơ Đã Duyệt",
            "Hồ Sơ Đã Đặt Cọc", "Trạng Thái Dự Án", "Ngày Mở Nộp", "Ngày Đóng Nộp"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cells[4, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(ExcelColor.FromArgb(31, 78, 120));
            cell.Style.Font.Color.SetColor(ExcelColor.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        int currentRow = 5;
        foreach (var r in rows)
        {
            ws.Cells[currentRow, 1].Value = r.Index;
            ws.Cells[currentRow, 2].Value = r.ProjectName;
            ws.Cells[currentRow, 3].Value = r.DeveloperName;
            ws.Cells[currentRow, 4].Value = r.DecisionNumber;
            ws.Cells[currentRow, 5].Value = r.Address;
            ws.Cells[currentRow, 6].Value = r.TotalUnits;
            ws.Cells[currentRow, 7].Value = r.AvailableUnits;
            ws.Cells[currentRow, 8].Value = r.TotalApplications;
            ws.Cells[currentRow, 9].Value = r.ApprovedApplications;
            ws.Cells[currentRow, 10].Value = r.DepositPaidApplications;
            ws.Cells[currentRow, 11].Value = r.ProjectStatus;
            ws.Cells[currentRow, 12].Value = r.ApplicationOpenDate;
            ws.Cells[currentRow, 13].Value = r.ApplicationCloseDate;

            for (int col = 1; col <= 13; col++)
            {
                ws.Cells[currentRow, col].Style.Border.BorderAround(ExcelBorderStyle.Thin, ExcelColor.LightGray);
            }
            currentRow++;
        }

        ws.Cells.AutoFitColumns();
        return await package.GetAsByteArrayAsync();
    }

    #endregion

    #region Private Helper Methods

    private async Task<List<HousingApplicationExportRowDto>> GetApplicationExportDataAsync(ExportApplicationFilterDto filter)
    {
        var query = _dbContext.HousingApplications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.HousingProject)
            .Include(a => a.StatusHistories)
            .AsQueryable();

        if (filter.ProjectId.HasValue)
        {
            query = query.Where(a => a.ProjectId == filter.ProjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(a => a.ApplicationStatus == filter.Status);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(a => a.SubmittedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(a => a.SubmittedAt <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(a => (a.FullName != null && a.FullName.ToLower().Contains(term))
                                  || (a.CitizenId != null && a.CitizenId.Contains(term))
                                  || (a.Applicant != null && (a.Applicant.FullName.ToLower().Contains(term) || a.Applicant.CitizenId.Contains(term))));
        }

        var list = await query
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync();

        return list.Select((a, idx) =>
        {
            var lastReject = a.StatusHistories?
                .Where(h => h.NewStatus == ApplicationStatusConstants.Rejected)
                .OrderByDescending(h => h.ChangedAt)
                .FirstOrDefault();

            return new HousingApplicationExportRowDto
            {
                Index = idx + 1,
                ApplicationCode = a.ApplicationId.ToString()[..8].ToUpper(),
                FullName = a.FullName ?? a.Applicant?.FullName ?? string.Empty,
                CitizenId = a.CitizenId ?? a.Applicant?.CitizenId ?? string.Empty,
                DateOfBirth = a.Applicant?.DateOfBirth?.ToString("dd/MM/yyyy") ?? string.Empty,
                PhoneNumber = a.Applicant?.PhoneNumber ?? string.Empty,
                Email = a.Applicant?.Email ?? string.Empty,
                Address = a.PermanentAddress ?? a.Applicant?.Address ?? string.Empty,
                ProjectName = a.HousingProject?.ProjectName ?? string.Empty,
                BeneficiaryGroup = a.PriorityGroup ?? string.Empty,
                HousingStatus = a.HousingStatus ?? string.Empty,
                ApplicationStatus = a.ApplicationStatus,
                SubmittedAt = a.SubmittedAt,
                ReviewedAt = a.UpdatedAt,
                RejectReason = lastReject?.Note ?? string.Empty
            };
        }).ToList();
    }

    private async Task<(HousingProject? Project, List<LotteryResultExportRowDto> Rows)> GetLotteryExportDataAsync(Guid projectId)
    {
        var project = await _dbContext.HousingProjects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId);

        var apps = await _dbContext.HousingApplications
            .AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.PrincipleAgreement)
            .Where(a => a.ProjectId == projectId && a.LotteryResult != null)
            .OrderByDescending(a => a.UpdatedAt ?? a.SubmittedAt)
            .ToListAsync();

        var rows = apps.Select((a, idx) => new LotteryResultExportRowDto
        {
            Index = idx + 1,
            ApplicationCode = a.ApplicationId.ToString()[..8].ToUpper(),
            FullName = a.FullName ?? a.Applicant?.FullName ?? string.Empty,
            CitizenId = a.CitizenId ?? a.Applicant?.CitizenId ?? string.Empty,
            PhoneNumber = a.Applicant?.PhoneNumber ?? string.Empty,
            BeneficiaryGroup = a.PriorityGroup ?? string.Empty,
            LotteryResult = a.LotteryResult ?? string.Empty,
            SlotCode = a.SlotCode ?? string.Empty,
            DrawnAt = a.UpdatedAt,
            HasPrincipleAgreement = a.PrincipleAgreement != null
        }).ToList();

        return (project, rows);
    }

    #endregion
}
