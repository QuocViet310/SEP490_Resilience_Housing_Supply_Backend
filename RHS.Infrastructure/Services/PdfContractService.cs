using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Helpers;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Sinh PDF Hợp đồng mua bán nhà ở xã hội theo Mẫu số 01 Phụ lục VI
/// Thông tư 05/2024/TT-BXD (thay thế Hợp đồng nguyên tắc).
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
        _logger = logger;
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
            "Generating sale-contract PDF for Application {AppId}, SlotCode={SlotCode}.",
            application.ApplicationId, slotCode);

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

        var fileName = $"HopDongMuaBanNOXH_{slotCode}.pdf";
        var pdfUrl = await _fileStorage.UploadPdfFromBytesAsync(
            pdfBytes, fileName, "sale-contracts");

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

    private static byte[] GeneratePdfBytes(
        HousingApplication application,
        HousingProject project,
        string slotCode,
        decimal paymentAmount,
        string? vnpTransactionNo,
        string wardManagerName)
    {
        var now = DateTime.Now;
        var contractNo = $"NOXH-{now:yyyy}-{slotCode}";
        var projectAddress = $"{project.Street}, {project.Ward}, {project.District}, {project.Province}";

        var apartment = application.Apartment;
        var hasApartment = apartment != null;
        var houseType = "Căn hộ chung cư nhà ở xã hội";
        var areaText = hasApartment
            ? apartment!.Area.ToString("0.##")
            : (project.MinArea > 0
                ? $"{project.MinArea:0.##}–{project.MaxArea:0.##}"
                : "Theo hồ sơ thiết kế dự án");
        var unitLabel = hasApartment
            ? $"{apartment!.UnitName} ({apartment.Area:0.##} m²)"
            : $"Theo phương án giá dự án (mã suất: {slotCode})";

        // Giá bán: ưu tiên giá căn đã cấp; không thì khoảng giá dự án / số tiền đợt tạm
        var salePrice = hasApartment
            ? apartment!.Price
            : (project.MinPrice > 0 ? project.MinPrice : paymentAmount);
        var salePriceWords = VietnameseNumberToWords.Convert(salePrice);
        var salePriceFmt = salePrice.ToString("#,##0");

        var maintenanceFee = Math.Round(salePrice * 0.02m, 0);
        var maintenanceWords = VietnameseNumberToWords.Convert(maintenanceFee);
        var totalValue = salePrice + maintenanceFee;
        var totalWords = VietnameseNumberToWords.Convert(totalValue);

        var depositFmt = paymentAmount.ToString("#,##0");
        var depositWords = VietnameseNumberToWords.Convert(paymentAmount);

        var buyerName = application.FullName ?? "";
        var buyerCccd = application.CitizenId ?? "";
        var buyerAddress = !string.IsNullOrWhiteSpace(application.PermanentAddress)
            ? application.PermanentAddress
            : application.CurrentResidence;
        var buyerPhone = application.Applicant?.PhoneNumber ?? "—";
        var submittedAt = application.SubmittedAt == default
            ? now
            : application.SubmittedAt.ToLocalTime();

        var sellerName = project.Developer?.FullName
            ?? "Chủ đầu tư dự án nhà ở xã hội";
        var sellerOrg = project.Developer != null
            ? $"Đại diện CĐT: {project.Developer.FullName}"
            : "Ban quản lý / Chủ đầu tư dự án";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(45);
                page.MarginVertical(36);
                page.DefaultTextStyle(x => x.FontSize(10.5f).FontFamily(Fonts.Lato).LineHeight(1.35f));

                page.Header().Column(h =>
                {
                    h.Item().AlignCenter().Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM").Bold().FontSize(12);
                    h.Item().AlignCenter().Text("Độc lập – Tự do – Hạnh phúc").Bold().FontSize(11).Underline();
                    h.Item().PaddingTop(6).AlignCenter()
                        .Text($"{project.Ward}, ngày {now:dd} tháng {now:MM} năm {now:yyyy}")
                        .Italic().FontSize(10);
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Trang ").FontSize(9).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken1);
                    t.Span(" / ").FontSize(9).FontColor(Colors.Grey.Darken1);
                    t.TotalPages().FontSize(9).FontColor(Colors.Grey.Darken1);
                    t.Span("  ·  Mẫu số 01 Phụ lục VI – Thông tư 05/2024/TT-BXD").FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Spacing(4);

                    col.Item().AlignCenter().Text("HỢP ĐỒNG MUA BÁN NHÀ Ở XÃ HỘI").Bold().FontSize(14);
                    col.Item().AlignCenter().Text($"Số: {contractNo}/HĐ").SemiBold().FontSize(11);

                    col.Item().PaddingTop(8).Text("Căn cứ Bộ Luật Dân sự ngày 24 tháng 11 năm 2015;").Italic().FontSize(9.5f);
                    col.Item().Text("Căn cứ Luật Nhà ở ngày 27 tháng 11 năm 2023;").Italic().FontSize(9.5f);
                    col.Item().Text("Căn cứ Nghị định số 100/2024/NĐ-CP ngày 26 tháng 7 năm 2024 của Chính phủ;").Italic().FontSize(9.5f);
                    col.Item().Text("Căn cứ Thông tư số 05/2024/TT-BXD ngày 31 tháng 7 năm 2024 của Bộ Xây dựng;").Italic().FontSize(9.5f);
                    col.Item().Text(t =>
                    {
                        t.Span("Căn cứ đơn đề nghị mua nhà ở xã hội của Ông/Bà ").Italic().FontSize(9.5f);
                        t.Span(buyerName).Bold().Italic().FontSize(9.5f);
                        t.Span($" ngày {submittedAt:dd}/{submittedAt:MM}/{submittedAt:yyyy};").Italic().FontSize(9.5f);
                    });
                    col.Item().Text($"Căn cứ hồ sơ đăng ký số suất: {slotCode} — Dự án «{project.ProjectName}».").Italic().FontSize(9.5f);

                    col.Item().PaddingTop(8).Text("Hai bên chúng tôi gồm:").SemiBold();

                    // ── Bên bán ──
                    col.Item().PaddingTop(6).Text("BÊN BÁN NHÀ Ở XÃ HỘI (sau đây gọi tắt là Bên bán):").Bold();
                    Bullet(col, $"Tên đơn vị/doanh nghiệp: {sellerOrg}");
                    Bullet(col, $"Người đại diện: {(string.IsNullOrWhiteSpace(wardManagerName) ? sellerName : wardManagerName)}, Chức vụ: Đại diện chủ đầu tư / Ban quản lý dự án");
                    Bullet(col, $"Dự án: {project.ProjectName}");
                    Bullet(col, $"Địa chỉ dự án: {projectAddress}");
                    if (!string.IsNullOrWhiteSpace(project.DecisionNumber))
                        Bullet(col, $"Quyết định/phê duyệt dự án: {project.DecisionNumber}");
                    Bullet(col, "Điện thoại / Fax / Số tài khoản / MST: theo hồ sơ pháp lý chủ đầu tư tại hệ thống.");

                    // ── Bên mua ──
                    col.Item().PaddingTop(6).Text("BÊN MUA NHÀ Ở XÃ HỘI (sau đây gọi tắt là Bên mua):").Bold();
                    Bullet(col, $"Ông/Bà: {buyerName}");
                    Bullet(col, $"Căn cước công dân số: {buyerCccd}");
                    Bullet(col, $"Đăng ký thường trú (hoặc tạm trú) tại: {buyerAddress}");
                    Bullet(col, $"Địa chỉ liên hệ: {application.CurrentResidence}");
                    Bullet(col, $"Điện thoại: {buyerPhone}");
                    Bullet(col, "Số tài khoản / MST: theo thông tin Bên mua cung cấp khi thanh toán.");

                    col.Item().PaddingTop(8).Text(
                        "Hai bên thống nhất ký kết hợp đồng mua bán nhà ở xã hội với các nội dung sau đây:")
                        .Italic();

                    // ── Điều 1 ──
                    Section(col, "Điều 1. Các thông tin về nhà ở mua bán");
                    Bullet(col, $"1. Loại nhà ở: {houseType}");
                    Bullet(col, $"2. Địa chỉ nhà ở / dự án: {projectAddress}");
                    Bullet(col, $"3. Diện tích sử dụng: {areaText} m² (căn hộ chung cư tính theo diện tích thông thủy)");
                    Bullet(col, $"4. Căn hộ / mã suất: {unitLabel}");
                    Bullet(col, "5. Phần sở hữu chung, sử dụng chung; thời hạn sử dụng nhà chung cư; diện tích sở hữu riêng; mục đích sử dụng phần chung: theo hồ sơ thiết kế đã được phê duyệt của dự án.");
                    Bullet(col, "6. Các trang thiết bị chủ yếu gắn liền với nhà ở: theo biên bản bàn giao và hồ sơ kỹ thuật dự án.");
                    Bullet(col, $"7. Đặc điểm về đất xây dựng / vị trí: {project.Ward}, {project.Province}.");
                    Bullet(col, $"8. Năm hoàn thành / tiến độ: theo tiến độ công bố của dự án «{project.ProjectName}».");
                    if (!hasApartment)
                    {
                        col.Item().PaddingLeft(12).Text(
                            "Ghi chú: Chi tiết căn, diện tích và đơn giá chính thức được chốt khi Chủ đầu tư cấp căn trên hệ thống trước khi ký; phụ lục điều chỉnh (nếu có) đính kèm hợp đồng này.")
                            .Italic().FontSize(9).FontColor(Colors.Grey.Darken2);
                    }

                    // ── Điều 2 ──
                    Section(col, "Điều 2. Giá bán, phương thức và thời hạn thanh toán");
                    Bullet(col, $"1. Giá bán nhà ở là {salePriceFmt} đồng. (Bằng chữ: {salePriceWords} đồng). Giá bán này đã bao gồm thuế giá trị gia tăng (GTGT) theo quy định.");
                    Bullet(col, $"2. Kinh phí bảo trì 2% giá bán căn hộ là {maintenanceFee:#,##0} đồng. (Bằng chữ: {maintenanceWords} đồng).");
                    Bullet(col, $"3. Tổng giá trị hợp đồng: {totalValue:#,##0} đồng. (Bằng chữ: {totalWords} đồng).");
                    Bullet(col, "4. Phương thức thanh toán: bằng tiền Việt Nam, chuyển khoản qua cổng thanh toán điện tử VNPay / tài khoản ngân hàng theo thỏa thuận.");
                    if (!string.IsNullOrWhiteSpace(vnpTransactionNo))
                        Bullet(col, $"   Mã giao dịch gần nhất: {vnpTransactionNo}");
                    Bullet(col, "5. Thời hạn thực hiện thanh toán (trả chậm / trả dần):");
                    Bullet(col, $"   - Đợt 1 (sau ký HĐ): {depositFmt} đồng (Bằng chữ: {depositWords} đồng), trong thời hạn theo chính sách dự án.");
                    Bullet(col, "   - Các đợt tiếp theo: theo lịch thanh toán trên hệ thống (Đợt 2, Đợt 3…), không thu quá 95% giá trị hợp đồng trước khi cấp Giấy chứng nhận.");
                    Bullet(col, "6. Mức phí và nguyên tắc điều chỉnh phí quản lý vận hành nhà chung cư trong thời gian chưa thành lập Ban Quản trị: theo quy chế quản lý vận hành dự án.");

                    // ── Điều 3 ──
                    Section(col, "Điều 3. Thời hạn giao nhận nhà ở");
                    Bullet(col, "1. Bên bán bàn giao nhà ở kèm trang thiết bị và giấy tờ pháp lý nêu tại Điều 1 trong thời hạn 30 ngày, kể từ ngày Bên mua thanh toán đủ số tiền mua nhà theo quy định (trừ thỏa thuận khác). Việc bàn giao lập biên bản có chữ ký hai bên.");
                    Bullet(col, "2. Các thỏa thuận khác: theo tiến độ bàn giao công bố của dự án trên hệ thống.");

                    // ── Điều 4 ──
                    Section(col, "Điều 4. Bảo hành nhà ở");
                    Bullet(col, "1. Bên bán bảo hành nhà ở theo đúng quy định của Luật Nhà ở.");
                    Bullet(col, "2. Bên mua thông báo bằng văn bản khi có hư hỏng thuộc diện bảo hành. Trong thời hạn 15 ngày kể từ ngày nhận thông báo, Bên bán thực hiện bảo hành; chậm gây thiệt hại thì bồi thường.");
                    Bullet(col, "3. Không bảo hành khi hư hỏng do thiên tai, địch họa hoặc lỗi người sử dụng.");
                    Bullet(col, "4. Sau thời hạn bảo hành theo Luật Nhà ở, sửa chữa thuộc trách nhiệm Bên mua.");

                    // ── Điều 5–6 ──
                    Section(col, "Điều 5. Quyền và nghĩa vụ của Bên bán");
                    Bullet(col, "1. Quyền: yêu cầu Bên mua trả đủ tiền, nhận bàn giao đúng hạn, nộp nghĩa vụ tài chính theo pháp luật.");
                    Bullet(col, "2. Nghĩa vụ: bàn giao nhà và hồ sơ đúng thỏa thuận; bảo hành; bảo quản trước bàn giao; nộp nghĩa vụ tài chính liên quan; làm thủ tục đề nghị cấp Giấy chứng nhận (trừ thỏa thuận Bên mua tự làm); bồi thường thiệt hại do lỗi của mình.");

                    Section(col, "Điều 6. Quyền và nghĩa vụ của Bên mua");
                    Bullet(col, "1. Quyền: yêu cầu bàn giao đúng hạn, chất lượng; yêu cầu phối hợp cấp Giấy chứng nhận; yêu cầu bảo hành và bồi thường nếu Bên bán vi phạm.");
                    Bullet(col, "2. Nghĩa vụ: thanh toán đủ, đúng hạn; nhận bàn giao; nộp thuế, phí, lệ phí theo quy định.");

                    // ── Điều 7–8 ──
                    Section(col, "Điều 7. Trách nhiệm do vi phạm hợp đồng");
                    Bullet(col, "Hai bên thỏa thuận: chậm thanh toán hoặc chậm nhận/bàn giao nhà bị phạt và/hoặc tính lãi theo mức không trái pháp luật; phương thức thực hiện ghi nhận trên hệ thống / phụ lục HĐ.");

                    Section(col, "Điều 8. Chuyển giao quyền và nghĩa vụ");
                    Bullet(col, "1. Bên mua không được bán lại nhà trong thời hạn tối thiểu 05 năm kể từ ngày thanh toán đủ tiền mua nhà, trừ trường hợp bán lại cho chủ đầu tư hoặc đối tượng được mua NOXH với giá tối đa bằng giá trong hợp đồng này.");
                    Bullet(col, "2. Sau 05 năm và đã được cấp Giấy chứng nhận, được bán theo cơ chế thị trường theo quy định pháp luật về nhà ở và thuế.");

                    // ── Điều 9–12 ──
                    Section(col, "Điều 9. Cam kết và giải quyết tranh chấp");
                    Bullet(col, "Hai bên cam kết thực hiện đúng Hợp đồng. Tranh chấp ưu tiên thương lượng; không thương lượng được thì yêu cầu Tòa án nhân dân giải quyết theo pháp luật.");

                    Section(col, "Điều 10. Chấm dứt hợp đồng");
                    Bullet(col, "1. Hai bên đồng ý chấm dứt bằng văn bản.");
                    Bullet(col, "2. Bên mua chậm thanh toán quá 60 ngày theo Điều 2.");
                    Bullet(col, "3. Bên bán chậm bàn giao quá 90 ngày theo Điều 3.");
                    Bullet(col, "4. Các trường hợp khác theo pháp luật.");

                    Section(col, "Điều 11. Các thỏa thuận khác");
                    Bullet(col, $"1. Hợp đồng điện tử được lưu trên Hệ thống Quản lý cung ứng nhà ở xã hội; mã suất tham chiếu: {slotCode}.");
                    Bullet(col, "2. Lịch thanh toán theo đợt trên hệ thống là phụ lục không tách rời của Hợp đồng này.");
                    Bullet(col, "3. Nội dung không trái quy định pháp luật về dân sự và nhà ở.");

                    Section(col, "Điều 12. Hiệu lực của hợp đồng");
                    Bullet(col, $"1. Hợp đồng có hiệu lực kể từ ngày Bên mua ký xác nhận trên hệ thống ({now:dd/MM/yyyy}) hoặc ngày ghi nhận thanh toán Đợt 1 thành công (nếu muộn hơn).");
                    Bullet(col, "2. Hợp đồng được lập thành chứng từ điện tử có giá trị pháp lý tương đương bản giấy; mỗi bên được tải/lưu bản PDF; bản lưu tại hệ thống phục vụ cơ quan thuế / cấp Giấy chứng nhận khi có yêu cầu.");

                    col.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Column(left =>
                        {
                            left.Item().AlignCenter().Text("BÊN MUA").Bold().FontSize(11);
                            left.Item().AlignCenter().Text("(Ký và ghi rõ họ tên)").Italic().FontSize(9);
                            left.Item().Height(56);
                            left.Item().AlignCenter().Text(buyerName).Bold().FontSize(10);
                        });
                        row.RelativeItem().AlignCenter().Column(right =>
                        {
                            right.Item().AlignCenter().Text("BÊN BÁN").Bold().FontSize(11);
                            right.Item().AlignCenter().Text("(Ký tên, đóng dấu)").Italic().FontSize(9);
                            right.Item().Height(56);
                            right.Item().AlignCenter().Text(
                                string.IsNullOrWhiteSpace(wardManagerName) ? sellerName : wardManagerName)
                                .Bold().FontSize(10);
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void Section(ColumnDescriptor col, string title)
    {
        col.Item().PaddingTop(8).Text(title).Bold().FontSize(11);
    }

    private static void Bullet(ColumnDescriptor col, string text)
    {
        col.Item().PaddingLeft(8).Text(text).FontSize(10);
    }
}
