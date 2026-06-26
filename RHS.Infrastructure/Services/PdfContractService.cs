using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Helpers;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Sinh PDF hợp đồng nguyên tắc mua nhà ở xã hội theo template chuẩn.
/// Dùng QuestPDF để render, upload lên Cloudinary qua IFileStorageService.
/// </summary>
public class PdfContractService : IPdfContractService
{
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<PdfContractService> _logger;

    public PdfContractService(
        IFileStorageService fileStorage,
        ILogger<PdfContractService> logger)
    {
        _fileStorage = fileStorage;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateAndUploadContractAsync(
        HousingApplication application,
        HousingProject project,
        string slotCode,
        decimal paymentAmount,
        string? vnpTransactionNo,
        string wardManagerName)
    {
        _logger.LogInformation(
            "Generating PDF contract for Application {AppId}, SlotCode={SlotCode}.",
            application.ApplicationId, slotCode);

        // ── 1. Sinh PDF bytes bằng QuestPDF ────────────────────────────────
        var pdfBytes = GeneratePdfBytes(
            application, project, slotCode,
            paymentAmount, vnpTransactionNo, wardManagerName);

        // ── 2. Upload lên Cloudinary ────────────────────────────────────────
        var fileName = $"HopDong_{slotCode}.pdf";
        var pdfUrl = await _fileStorage.UploadPdfFromBytesAsync(
            pdfBytes, fileName, "principle-agreements");

        _logger.LogInformation(
            "PDF contract uploaded: {Url} for SlotCode={SlotCode}.", pdfUrl, slotCode);

        return pdfUrl;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Private: Render PDF content
    // ═══════════════════════════════════════════════════════════════════════

    private static byte[] GeneratePdfBytes(
        HousingApplication application,
        HousingProject project,
        string slotCode,
        decimal paymentAmount,
        string? vnpTransactionNo,
        string wardManagerName)
    {
        var now = DateTime.Now;
        var amountInWords = VietnameseNumberToWords.Convert(paymentAmount);
        var amountFormatted = paymentAmount.ToString("#,##0");
        var fullAddress = $"{project.Street}, {project.Ward}, {project.District}, {project.Province}";

        // Thông tin bốc thăm
        var lotteryDateStr = project.LotteryDate?.ToString("dd/MM/yyyy") ?? "(Sẽ thông báo sau)";
        var lotteryTimeStr = project.LotteryDate?.ToString("HH:mm") ?? "08:00";
        var lotteryLocation = project.LotteryLocation
            ?? $"Hội trường UBND {project.Ward}, {project.Province}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(60);
                page.MarginVertical(40);
                page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // ── Header quốc hiệu ──────────────────────────────────
                    col.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM")
                        .Bold().FontSize(13);
                    col.Item().AlignCenter().Text("Độc lập – Tự do – Hạnh phúc")
                        .Bold().FontSize(12).Underline();

                    col.Item().Height(20);

                    // ── Tiêu đề ────────────────────────────────────────────
                    col.Item().AlignCenter().Text("HỢP ĐỒNG NGUYÊN TẮC")
                        .Bold().FontSize(16);

                    col.Item().Height(15);

                    // ── Căn cứ pháp lý ─────────────────────────────────────
                    col.Item().Text(text =>
                    {
                        text.Span("- Căn cứ Bộ luật Dân sự số 91/2015/QH13 được Quốc hội nước Cộng hoà xã hội chủ nghĩa Việt Nam thông qua ngày 24 tháng 11 năm 2015;")
                            .Italic().FontSize(11);
                    });
                    col.Item().Text(text =>
                    {
                        text.Span("- Căn cứ vào nhu cầu và khả năng của cả 2 bên.")
                            .Italic().FontSize(11);
                    });

                    col.Item().Height(10);

                    col.Item().Text($"Hôm nay, ngày {now:dd} tháng {now:MM} năm {now:yyyy}, chúng tôi gồm:");

                    col.Item().Height(12);

                    // ── BÊN A: BAN QUẢN LÝ DỰ ÁN ──────────────────────────
                    col.Item().Text("BÊN ĐẠI DIỆN CUNG CẤP: BAN QUẢN LÝ DỰ ÁN").Bold();
                    col.Item().PaddingLeft(20).Column(sub =>
                    {
                        sub.Item().Text(t =>
                        {
                            t.Span("Dự án: ").SemiBold();
                            t.Span(project.ProjectName);
                        });
                        sub.Item().Text(t =>
                        {
                            t.Span("Địa chỉ dự án: ").SemiBold();
                            t.Span(fullAddress);
                        });
                        sub.Item().Text(t =>
                        {
                            t.Span("Đại diện bởi Ông/Bà: ").SemiBold();
                            t.Span(wardManagerName);
                        });
                        sub.Item().Text(t =>
                        {
                            t.Span("Chức vụ: ").SemiBold();
                            t.Span("Cán bộ quản lý địa bàn");
                        });
                    });

                    col.Item().Height(10);

                    // ── BÊN B: NGƯỜI ĐĂNG KÝ ───────────────────────────────
                    col.Item().Text("BÊN ĐĂNG KÝ NHẬN NHÀ: NGƯỜI ĐĂNG KÝ").Bold();
                    col.Item().PaddingLeft(20).Column(sub =>
                    {
                        sub.Item().Text(t =>
                        {
                            t.Span("Họ và tên: ").SemiBold();
                            t.Span(application.FullName);
                        });
                        sub.Item().Text(t =>
                        {
                            t.Span("Số CCCD: ").SemiBold();
                            t.Span(application.CitizenId);
                        });
                        sub.Item().Text(t =>
                        {
                            t.Span("Nơi thường trú: ").SemiBold();
                            t.Span(application.PermanentAddress);
                        });
                    });

                    col.Item().Height(10);

                    col.Item().Text("Sau khi bàn bạc, hai bên thống nhất ký kết Hợp đồng nguyên tắc này với các điều khoản sau:")
                        .Italic();

                    col.Item().Height(12);

                    // ── ĐIỀU 1 ──────────────────────────────────────────────
                    col.Item().Text("ĐIỀU 1: ĐỐI TƯỢNG HỢP ĐỒNG").Bold();
                    col.Item().PaddingLeft(10).Text(t =>
                    {
                        t.Span($"Bên A xác nhận Bên B đủ điều kiện và chính thức sở hữu một (01) suất mua/thuê căn hộ thuộc Dự án Nhà ở xã hội ");
                        t.Span(project.ProjectName).Bold();
                        t.Span(".");
                    });
                    col.Item().PaddingLeft(10).Text(t =>
                    {
                        t.Span("Mã định danh suất mua: ").SemiBold();
                        t.Span(slotCode).Bold().FontColor(Colors.Blue.Darken2);
                    });

                    col.Item().Height(10);

                    // ── ĐIỀU 2 ──────────────────────────────────────────────
                    col.Item().Text("ĐIỀU 2: THANH TOÁN TÀI CHÍNH").Bold();
                    col.Item().PaddingLeft(10).Column(sub =>
                    {
                        sub.Item().Text(t =>
                        {
                            t.Span("Để bảo đảm suất mua nêu trên, Bên B đã hoàn tất thanh toán số tiền là: ");
                            t.Span($"{amountFormatted} VNĐ").Bold();
                            t.Span(".");
                        });
                        sub.Item().Text(t =>
                        {
                            t.Span("(Bằng chữ: ");
                            t.Span($"{amountInWords} đồng chẵn").Italic();
                            t.Span(").");
                        });
                        sub.Item().Text(t =>
                        {
                            t.Span("Phương thức thanh toán: ").SemiBold();
                            t.Span($"Chuyển khoản thành công qua Cổng thanh toán điện tử: {vnpTransactionNo ?? "N/A"}");
                            t.Span(".");
                        });
                    });

                    col.Item().Height(10);

                    // ── ĐIỀU 3 ──────────────────────────────────────────────
                    col.Item().Text("ĐIỀU 3: BỐC THĂM VỊ TRÍ CĂN HỘ").Bold();
                    col.Item().PaddingLeft(10).Column(sub =>
                    {
                        sub.Item().Text("Do dự án có nhiều vị trí/tầng/hướng khác nhau, Bên B sẽ tham gia buổi bốc thăm để nhận mã số căn hộ cụ thể (Số phòng, Tầng, Tòa).");
                        sub.Item().Height(5);
                        sub.Item().Text("Thông tin buổi bốc thăm:").SemiBold();
                        sub.Item().PaddingLeft(10).Text(t =>
                        {
                            t.Span("Thời gian tổ chức: ").SemiBold();
                            t.Span($"{lotteryTimeStr}, ngày {lotteryDateStr}");
                        });
                        sub.Item().PaddingLeft(10).Text(t =>
                        {
                            t.Span("Địa điểm tổ chức: ").SemiBold();
                            t.Span(lotteryLocation);
                        });
                        sub.Item().Height(5);
                        sub.Item().Text("Sau khi có kết quả bốc thăm vị trí căn hộ, hai bên sẽ tiến hành ký Hợp đồng Mua bán/Bàn giao nhà chính thức dựa trên mã căn hộ mà Bên B bốc trúng.");
                    });

                    col.Item().Height(10);

                    // ── ĐIỀU 4 ──────────────────────────────────────────────
                    col.Item().Text("ĐIỀU 4: ĐIỀU KHOẢN CHUNG").Bold();
                    col.Item().PaddingLeft(10).Column(sub =>
                    {
                        sub.Item().Text("Hợp đồng này có hiệu lực kể từ thời điểm hệ thống ghi nhận thanh toán thành công.");
                        sub.Item().Text("Hợp đồng được tạo lập dưới dạng chứng từ điện tử trên Hệ thống Quản lý, có giá trị pháp lý và hiệu lực đối soát tương đương bản giấy.");
                    });

                    col.Item().Height(30);

                    // ── Chữ ký ──────────────────────────────────────────────
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Column(left =>
                        {
                            left.Item().Text("ĐẠI DIỆN BÊN CUNG CẤP").Bold().FontSize(11);
                            left.Item().Height(50); // Khoảng trống cho chữ ký
                            left.Item().Text(wardManagerName).Bold();
                        });

                        row.RelativeItem().AlignCenter().Column(right =>
                        {
                            right.Item().Text("ĐẠI DIỆN BÊN ĐĂNG KÝ NHẬN NHÀ").Bold().FontSize(11);
                            right.Item().Height(50);
                            right.Item().Text(application.FullName).Bold();
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
}
