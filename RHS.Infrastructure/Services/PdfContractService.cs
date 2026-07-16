using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Helpers;
using System.Reflection;

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

        _logger.LogInformation(
            "PDF generated: {ByteCount} bytes for SlotCode={SlotCode}.",
            pdfBytes.Length, slotCode);

        if (pdfBytes.Length == 0)
        {
            throw new InvalidOperationException(
                $"PDF generation returned empty bytes for SlotCode={slotCode}.");
        }

        // ── 2. Upload lên Cloudinary ────────────────────────────────────────
        var fileName = $"HopDong_{slotCode}.pdf";
        var pdfUrl = await _fileStorage.UploadPdfFromBytesAsync(
            pdfBytes, fileName, "principle-agreements");

        _logger.LogInformation(
            "PDF contract uploaded: {Url} for SlotCode={SlotCode}.", pdfUrl, slotCode);

        return pdfUrl;
    }

    /// <inheritdoc/>
    public byte[] GeneratePdfBytesOnly(
        HousingApplication application,
        HousingProject project,
        string slotCode,
        decimal paymentAmount,
        string? vnpTransactionNo,
        string wardManagerName)
    {
        return GeneratePdfBytes(application, project, slotCode,
            paymentAmount, vnpTransactionNo, wardManagerName);
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

        var lotteryDateStr = project.LotteryDate?.ToString("dd/MM/yyyy") ?? "(Sẽ thông báo sau)";
        var lotteryTimeStr = project.LotteryDate?.ToString("HH:mm") ?? "08:00";
        var lotteryLocation = project.LotteryLocation
            ?? $"Hội trường UBND {project.Ward}, {project.Province}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(55);
                page.MarginVertical(40);
                page.DefaultTextStyle(x => x.FontSize(12).FontFamily(Fonts.Lato));

                page.Content().Column(col =>
                {
                    // ── Header quốc hiệu ──────────────────────────────────
                    col.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold().FontSize(13);
                    col.Item().AlignCenter().Text("Độc lập – Tự do – Hạnh phúc").Bold().FontSize(12).Underline();

                    col.Item().Height(18);

                    // ── Tiêu đề ────────────────────────────────────────────
                    col.Item().AlignCenter().Text("HỢP ĐỒNG NGUYÊN TẮC").Bold().FontSize(15);

                    col.Item().Height(12);

                    // ── Căn cứ pháp lý ─────────────────────────────────────
                    col.Item().Text("- Căn cứ Bộ luật Dân sự số 91/2015/QH13 ngày 24/11/2015;").Italic().FontSize(11);
                    col.Item().Text("- Căn cứ vào nhu cầu và khả năng của cả 2 bên.").Italic().FontSize(11);

                    col.Item().Height(10);

                    col.Item().Text($"Hôm nay, ngày {now:dd} tháng {now:MM} năm {now:yyyy}, chúng tôi gồm:");

                    col.Item().Height(12);

                    // ── BÊN A ──────────────────────────────────────────────
                    col.Item().Text("BÊN ĐẠI DIỆN CUNG CẤP (BÊN A): BAN QUẢN LÝ DỰ ÁN").Bold();
                    col.Item().PaddingLeft(20).Column(sub =>
                    {
                        sub.Item().Text(t => { t.Span("Dự án: ").SemiBold(); t.Span(project.ProjectName); });
                        sub.Item().Text(t => { t.Span("Địa chỉ dự án: ").SemiBold(); t.Span(fullAddress); });
                        sub.Item().Text(t => { t.Span("Đại diện bởi Ông/Bà: ").SemiBold(); t.Span(wardManagerName); });
                        sub.Item().Text(t => { t.Span("Chức vụ: ").SemiBold(); t.Span("Cán bộ quản lý địa bàn"); });
                    });

                    col.Item().Height(10);

                    // ── BÊN B ──────────────────────────────────────────────
                    col.Item().Text("BÊN ĐĂNG KÝ NHẬN NHÀ (BÊN B): NGƯỜI ĐĂNG KÝ").Bold();
                    col.Item().PaddingLeft(20).Column(sub =>
                    {
                        sub.Item().Text(t => { t.Span("Họ và tên: ").SemiBold(); t.Span(application.FullName); });
                        sub.Item().Text(t => { t.Span("Số CCCD: ").SemiBold(); t.Span(application.CitizenId); });
                        sub.Item().Text(t => { t.Span("Nơi thường trú: ").SemiBold(); t.Span(application.PermanentAddress); });
                    });

                    col.Item().Height(10);

                    col.Item().Text("Hai bên thống nhất ký kết Hợp đồng nguyên tắc với các điều khoản sau:").Italic();

                    col.Item().Height(12);

                    // ── ĐIỀU 1 ──────────────────────────────────────────────
                    col.Item().Text("ĐIỀU 1: ĐỐI TƯỢNG HỢP ĐỒNG").Bold();
                    col.Item().PaddingLeft(15).Text(t =>
                    {
                        t.Span("Bên A xác nhận Bên B đủ điều kiện tham gia phân suất mua căn hộ thuộc Dự án Nhà ở xã hội ");
                        t.Span(project.ProjectName).Bold();
                        t.Span(" theo hình thức bốc thăm (không đồng nghĩa với việc đã được phân căn).");
                    });
                    col.Item().PaddingLeft(15).Text(t =>
                    {
                        t.Span("Mã tham dự bốc thăm: ").SemiBold();
                        t.Span(slotCode).Bold().FontColor(Colors.Blue.Darken2);
                    });

                    col.Item().Height(10);

                    // ── ĐIỀU 2 ──────────────────────────────────────────────
                    col.Item().Text("ĐIỀU 2: THANH TOÁN TÀI CHÍNH").Bold();
                    col.Item().PaddingLeft(15).Column(sub =>
                    {
                        sub.Item().Text(t =>
                        {
                            t.Span("1. Bên B đã hoàn tất thanh toán tiền đặt cọc để đủ điều kiện tham gia bốc thăm, số tiền: ");
                            t.Span($"{amountFormatted} VNĐ").Bold();
                            t.Span(".");
                        });
                        sub.Item().PaddingLeft(10).Text(t =>
                        {
                            t.Span("(Bằng chữ: ");
                            t.Span($"{amountInWords} đồng chẵn").Italic();
                            t.Span(").");
                        });
                        sub.Item().Text(t =>
                        {
                            t.Span("2. Phương thức thanh toán: ").SemiBold();
                            t.Span($"Chuyển khoản qua Cổng thanh toán điện tử VNPay.");
                        });
                        sub.Item().PaddingLeft(10).Text(t =>
                        {
                            t.Span("Mã giao dịch: ").SemiBold();
                            t.Span(vnpTransactionNo ?? "N/A");
                        });
                        sub.Item().Text("3. Khoản đặt cọc không phải thanh toán giá mua nhà đầy đủ và không bảo đảm Bên B chắc chắn được phân căn.");
                    });

                    col.Item().Height(10);

                    // ── ĐIỀU 3 ──────────────────────────────────────────────
                    col.Item().Text("ĐIỀU 3: BỐC THĂM PHÂN SUẤT").Bold();
                    col.Item().PaddingLeft(15).Column(sub =>
                    {
                        sub.Item().Text("1. Bên B được đưa vào danh sách tham gia bốc thăm phân suất theo quy định. Kết quả có thể là trúng hoặc không trúng.");
                        sub.Item().Text("2. Chỉ khi trúng bốc thăm, Bên B mới được phân suất và hai bên tiến hành ký Hợp đồng mua bán chính thức.");
                        sub.Item().Text("3. Nếu không trúng, Hợp đồng nguyên tắc này chấm dứt hiệu lực phân suất; việc xử lý tiền đặt cọc thực hiện theo quy định áp dụng.");
                        sub.Item().PaddingLeft(10).Text(t =>
                        {
                            t.Span("Thời gian tổ chức (dự kiến): ").SemiBold();
                            t.Span($"{lotteryTimeStr}, ngày {lotteryDateStr}");
                        });
                        sub.Item().PaddingLeft(10).Text(t =>
                        {
                            t.Span("Địa điểm tổ chức (dự kiến): ").SemiBold();
                            t.Span(lotteryLocation);
                        });
                    });

                    col.Item().Height(10);

                    // ── ĐIỀU 4 ──────────────────────────────────────────────
                    col.Item().Text("ĐIỀU 4: ĐIỀU KHOẢN CHUNG").Bold();
                    col.Item().PaddingLeft(15).Column(sub =>
                    {
                        sub.Item().Text("1. Hợp đồng có hiệu lực kể từ thời điểm hệ thống ghi nhận thanh toán đặt cọc thành công.");
                        sub.Item().Text("2. Hợp đồng được tạo lập dưới dạng chứng từ điện tử trên Hệ thống Quản lý, có giá trị pháp lý tương đương bản giấy.");
                        sub.Item().Text("3. Hợp đồng nguyên tắc này không thay thế Hợp đồng mua bán nhà ở xã hội.");
                    });

                    col.Item().Height(35);

                    // ── Chữ ký ──────────────────────────────────────────────
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Column(left =>
                        {
                            left.Item().AlignCenter().Text("ĐẠI DIỆN BÊN CUNG CẤP").Bold().FontSize(11);
                            left.Item().Height(70);
                            left.Item().AlignCenter().Text(wardManagerName).Bold();
                        });
                        row.RelativeItem().AlignCenter().Column(right =>
                        {
                            right.Item().AlignCenter().Text("ĐẠI DIỆN BÊN ĐĂNG KÝ NHẬN NHÀ").Bold().FontSize(11);
                            right.Item().Height(70);
                            right.Item().AlignCenter().Text(application.FullName).Bold();
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
}
