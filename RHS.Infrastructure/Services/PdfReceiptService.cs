using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Sinh PDF biên nhận nộp hồ sơ nhà ở xã hội theo mẫu chuẩn.
/// Dùng QuestPDF để render, upload lên Cloudinary qua IFileStorageService.
/// </summary>
public class PdfReceiptService : IPdfReceiptService
{
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<PdfReceiptService> _logger;

    public PdfReceiptService(
        IFileStorageService fileStorage,
        ILogger<PdfReceiptService> logger)
    {
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateAndUploadReceiptAsync(
        HousingApplication application,
        HousingProject project)
    {
        _logger.LogInformation(
            "Generating PDF Receipt for Application {AppId}, Project={ProjectName}.",
            application.ApplicationId, project.ProjectName);

        var pdfBytes = GeneratePdfBytes(application, project);

        _logger.LogInformation(
            "PDF Receipt generated: {ByteCount} bytes for ApplicationId={AppId}.",
            pdfBytes.Length, application.ApplicationId);

        if (pdfBytes.Length == 0)
        {
            throw new InvalidOperationException(
                $"PDF Receipt generation returned empty bytes for ApplicationId={application.ApplicationId}.");
        }

        var fileName = $"BienNhan_{application.ApplicationId}.pdf";
        var pdfUrl = await _fileStorage.UploadPdfFromBytesAsync(
            pdfBytes, fileName, "housing-receipts");

        _logger.LogInformation(
            "PDF Receipt uploaded: {Url} for ApplicationId={AppId}.", pdfUrl, application.ApplicationId);

        return pdfUrl;
    }

    private static byte[] GeneratePdfBytes(HousingApplication application, HousingProject project)
    {
        var now = DateTime.Now;
        var fullAddress = $"{project.Street}, {project.Ward}, {project.District}, {project.Province}";
        var developerName = project.Developer?.FullName ?? "Chủ đầu tư dự án";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(50);
                page.MarginVertical(40);
                // Dùng font mặc định Lato
                page.DefaultTextStyle(x => x.FontSize(10.5f).FontFamily(Fonts.Lato));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(subCol =>
                        {
                            subCol.Item().AlignCenter().Text($"SỞ XÂY DỰNG TỈNH/TP {project.Province.ToUpper()}").FontSize(9.5f).Bold();
                            subCol.Item().AlignCenter().Text(developerName.ToUpper()).FontSize(9.5f).Bold();
                        });

                        row.RelativeItem().Column(subCol =>
                        {
                            subCol.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").FontSize(9.5f).Bold();
                            subCol.Item().AlignCenter().Text("Độc lập - Tự do - Hạnh phúc").FontSize(9.5f).Bold().Underline();
                            subCol.Item().AlignCenter().Text($"{project.Province}, ngày {now.Day:D2} tháng {now.Month:D2} năm {now.Year}").FontSize(8.5f).Italic();
                        });
                    });
                    col.Item().Height(15);
                });

                page.Content().Column(col =>
                {
                    // ── Tiêu đề ────────────────────────────────────────────
                    col.Item().AlignCenter().Text("BIÊN BẢN").Bold().FontSize(13);
                    col.Item().AlignCenter().Text("Giao nhận hồ sơ, tài liệu").Bold().FontSize(12);
                    col.Item().Height(15);

                    // ── Căn cứ ─────────────────────────────────────────────
                    col.Item().Text("Căn cứ Nghị định số 30/2020/NĐ-CP ngày 05 tháng 3 năm 2020 của Chính phủ về công tác văn thư;").Italic().FontSize(9.5f);
                    col.Item().Text($"Căn cứ Kế hoạch xét duyệt và tiếp nhận hồ sơ mua Nhà ở xã hội cho dự án {project.ProjectName};").Italic().FontSize(9.5f);
                    col.Item().Height(10);

                    col.Item().Text("Chúng tôi gồm:").Bold();
                    col.Item().Height(5);

                    // ── BÊN GIAO ───────────────────────────────────────────
                    col.Item().Text("BÊN GIAO: (Cá nhân nộp hồ sơ, tài liệu)").Bold();
                    col.Item().PaddingLeft(15).Column(sub =>
                    {
                        sub.Item().Text(t => { t.Span("Ông (bà): ").SemiBold(); t.Span(application.FullName); });
                        sub.Item().Text(t => { t.Span("Số CCCD/CMND: ").SemiBold(); t.Span(application.CitizenId); });
                        sub.Item().Text(t => { t.Span("Địa chỉ thường trú: ").SemiBold(); t.Span(application.PermanentAddress); });
                        sub.Item().Text(t => { t.Span("Nơi ở hiện tại: ").SemiBold(); t.Span(application.CurrentResidence); });
                    });
                    col.Item().Height(10);

                    // ── BÊN NHẬN ───────────────────────────────────────────
                    col.Item().Text("BÊN NHẬN: (Lưu trữ cơ quan)").Bold();
                    col.Item().PaddingLeft(15).Column(sub =>
                    {
                        sub.Item().Text(t => { t.Span("Cơ quan/Tổ chức: ").SemiBold(); t.Span(developerName); });
                        sub.Item().Text(t => { t.Span("Đại diện bởi: ").SemiBold(); t.Span("Hệ thống Resilience Housing Supply (RHS)"); });
                    });
                    col.Item().Height(10);

                    col.Item().Text("Thống nhất lập biên bản giao nhận tài liệu với những nội dung như sau:").Bold();
                    col.Item().Height(5);

                    // ── NỘI DUNG GIAO NHẬN ────────────────────────────────
                    col.Item().PaddingLeft(15).Column(sub =>
                    {
                        sub.Item().Text(t => { t.Span("1. Tên khối tài liệu giao nộp: ").SemiBold(); t.Span($"Hồ sơ đăng ký mua nhà ở xã hội tại dự án {project.ProjectName}"); });
                        sub.Item().Text(t => { t.Span("2. Thời gian của hồ sơ, tài liệu: ").SemiBold(); t.Span(now.ToString("dd/MM/yyyy HH:mm:ss")); });
                        sub.Item().Text("3. Số lượng tài liệu:").SemiBold();
                        sub.Item().PaddingLeft(15).Column(inner =>
                        {
                            inner.Item().Text("a) Đối với hồ sơ, tài liệu giấy:");
                            inner.Item().PaddingLeft(15).Text("- Tổng số hộp (cặp): 0");
                            inner.Item().PaddingLeft(15).Text("- Tổng số hồ sơ (đơn vị bảo quản): 0. Quy ra mét giá: 0 mét.");
                            inner.Item().Text("b) Đối với hồ sơ, tài liệu điện tử:");
                            inner.Item().PaddingLeft(15).Text($"- Tổng số hồ sơ: 01 hồ sơ (Mã số: {application.ApplicationId.ToString().ToUpper()})");
                            inner.Item().PaddingLeft(15).Text($"- Tổng số tệp tin trong hồ sơ: {application.Documents?.Count ?? 0} tệp tin");
                        });
                        sub.Item().Text(t => { t.Span("4. Tình trạng tài liệu giao nộp: ").SemiBold(); t.Span("Đầy đủ, nguyên vẹn trên hệ thống lưu trữ điện tử."); });
                        sub.Item().Text("5. Mục lục hồ sơ, tài liệu nộp lưu kèm theo:").SemiBold();
                    });
                    col.Item().Height(10);

                    // ── BẢNG TÀI LIỆU CHI TIẾT ─────────────────────────────
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(40);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(5).AlignCenter().Text("STT").Bold().FontSize(9);
                            header.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(5).AlignCenter().Text("Tên tệp tin").Bold().FontSize(9);
                            header.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(5).AlignCenter().Text("Loại tài liệu").Bold().FontSize(9);
                            header.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(5).AlignCenter().Text("Trạng thái AI").Bold().FontSize(9);
                        });

                        if (application.Documents != null && application.Documents.Any())
                        {
                            var idx = 1;
                            foreach (var doc in application.Documents)
                            {
                                table.Cell().Border(1).Padding(5).AlignCenter().Text(idx.ToString()).FontSize(9);
                                table.Cell().Border(1).Padding(5).AlignLeft().Text(doc.FileName).FontSize(9);
                                table.Cell().Border(1).Padding(5).AlignLeft().Text(doc.DocumentType).FontSize(9);

                                var statusText = doc.VerificationStatus switch
                                {
                                    "VERIFIED" => "Đã xác minh (Khớp)",
                                    "REJECTED" => "Từ chối (Không khớp)",
                                    _ => "Chưa xác minh"
                                };
                                table.Cell().Border(1).Padding(5).AlignCenter().Text(statusText).FontSize(9);
                                idx++;
                            }
                        }
                        else
                        {
                            table.Cell().ColumnSpan(4).Border(1).Padding(5).AlignCenter().Text("Chưa có tài liệu đính kèm").Italic().FontSize(9);
                        }
                    });
                    col.Item().Height(10);

                    col.Item().Text("Biên bản này được lập thành hai bản; bên giao giữ một bản, bên nhận giữ một bản./.").Italic();
                    col.Item().Height(25);

                    // ── PHẦN CHỮ KÝ ────────────────────────────────────────
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Cell().AlignCenter().Column(sigCol =>
                        {
                            sigCol.Item().Text("ĐẠI DIỆN BÊN GIAO").Bold().FontSize(10);
                            sigCol.Item().Text("(Ký và ghi rõ họ và tên)").Italic().FontSize(8);
                            sigCol.Item().Height(50);
                            sigCol.Item().Text(application.FullName).Bold().FontSize(10);
                        });

                        table.Cell().AlignCenter().Column(sigCol =>
                        {
                            sigCol.Item().Text("ĐẠI DIỆN BÊN NHẬN").Bold().FontSize(10);
                            sigCol.Item().Text("(Ký và ghi rõ họ và tên)").Italic().FontSize(8);
                            sigCol.Item().Height(50);
                            sigCol.Item().Text(developerName).Bold().FontSize(10);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Trang ").FontSize(9);
                    t.CurrentPageNumber().FontSize(9);
                    t.Span(" trên ").FontSize(9);
                    t.TotalPages().FontSize(9);
                });
            });
        }).GeneratePdf();

        return document;
    }
}
