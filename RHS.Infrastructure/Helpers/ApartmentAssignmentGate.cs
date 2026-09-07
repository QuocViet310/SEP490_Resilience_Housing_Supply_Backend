using RHS.Domain.Constants;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Helpers;

/// <summary>
/// Một nơi duy nhất quyết định "căn này có được gán cho hồ sơ này hay không".
/// Bốc thăm chỉ chọn ra AI được mua; căn cụ thể do CĐT gán sau (Đ38.2 Nghị định 100/2024),
/// nên nếu bước gán không có ràng buộc thì kết quả bốc thăm công bằng vẫn bị làm lệch ở bước sau.
///
/// Hai ràng buộc cốt lõi:
///   1. Đúng loại căn người dân đã đăng ký nguyện vọng — vì suất bốc thăm được đếm theo từng
///      loại căn (<see cref="ProjectUnitSeatHelper.GetAvailableUnitsByTypeAsync"/>). Gán lệch loại
///      làm pool loại này dư suất ảo, pool loại kia hụt căn, và người đăng ký đúng loại bị đẩy
///      xuống danh sách dự bị dù đáng ra còn suất.
///   2. Quỹ căn ưu tiên chỉ dành cho đối tượng chính sách — nếu không cưỡng chế thì cột
///      <c>UnitGroup</c> chỉ là nhãn trang trí trong báo cáo.
/// </summary>
public static class ApartmentAssignmentGate
{
    /// <summary>
    /// Kiểm tra nhanh, dùng khi cần LỌC danh sách (đôn danh sách dự bị, dropdown chọn căn)
    /// thay vì chặn một thao tác cụ thể.
    /// </summary>
    public static bool IsAssignable(HousingApplication app, Apartment apartment) =>
        MatchesDesiredType(app, apartment) && MatchesUnitGroup(app, apartment);

    /// <summary>
    /// Đúng loại căn đã đăng ký nguyện vọng.
    /// Hồ sơ cũ chưa khai nguyện vọng, hoặc căn chưa gắn loại, thì không có căn cứ đối chiếu — cho qua
    /// để không vỡ dữ liệu đang chạy, thay vì chặn oan.
    /// </summary>
    public static bool MatchesDesiredType(HousingApplication app, Apartment apartment)
    {
        if (!app.DesiredApartmentTypeId.HasValue || !apartment.ApartmentTypeId.HasValue)
            return true;

        return app.DesiredApartmentTypeId.Value == apartment.ApartmentTypeId.Value;
    }

    /// <summary>
    /// Quỹ căn ưu tiên chỉ gán cho hồ sơ thuộc nhóm ưu tiên.
    /// Ràng buộc CHỈ MỘT CHIỀU: người ưu tiên vẫn được nhận căn tiêu chuẩn, vì quỹ ưu tiên là mức
    /// sàn dành riêng chứ không phải mức trần — chặn hai chiều sẽ khiến người ưu tiên trắng tay
    /// khi quỹ ưu tiên hết căn, tức là tự thêm điều kiện ngoài luật.
    /// </summary>
    public static bool MatchesUnitGroup(HousingApplication app, Apartment apartment)
    {
        var group = apartment.UnitGroup?.Trim().ToUpperInvariant();
        if (group != UnitGroupConstants.Priority)
            return true;

        return !string.IsNullOrWhiteSpace(app.PriorityGroup);
    }

    /// <summary>
    /// Chặn thao tác gán căn không hợp lệ, kèm lý do đủ cụ thể để cán bộ biết phải chọn căn nào.
    /// </summary>
    /// <param name="app">Hồ sơ được cấp căn.</param>
    /// <param name="apartment">Căn được chọn.</param>
    /// <param name="appLabel">Tên hồ sơ để hiển thị khi gán theo lô, giúp biết hồ sơ nào lỗi.</param>
    public static void EnsureAssignable(
        HousingApplication app,
        Apartment apartment,
        string? appLabel = null)
    {
        var who = string.IsNullOrWhiteSpace(appLabel) ? "Hồ sơ" : $"Hồ sơ {appLabel}";

        if (apartment.ProjectId != app.ProjectId)
            throw new InvalidOperationException(
                $"Căn '{apartment.UnitName}' không thuộc dự án của {who.ToLowerInvariant()}.");

        if (app.ApartmentId.HasValue)
            throw new InvalidOperationException($"{who} đã được gán căn trước đó.");

        if (!string.Equals(apartment.Status, ApartmentStatusConstants.Available, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Căn '{apartment.UnitName}' không còn trống (Status={apartment.Status}).");

        if (!MatchesDesiredType(app, apartment))
        {
            var desired = app.DesiredApartmentType?.TypeName ?? "loại đã đăng ký";
            var actual = apartment.ApartmentType?.TypeName ?? "loại khác";
            throw new InvalidOperationException(
                $"{who} đăng ký nguyện vọng '{desired}' nhưng căn '{apartment.UnitName}' thuộc '{actual}'. " +
                "Suất trúng được tính theo từng loại căn, nên gán lệch loại sẽ làm sai số suất còn lại " +
                "của cả hai loại và ảnh hưởng người khác. Vui lòng chọn căn đúng loại nguyện vọng.");
        }

        if (!MatchesUnitGroup(app, apartment))
            throw new InvalidOperationException(
                $"Căn '{apartment.UnitName}' thuộc quỹ căn ưu tiên, chỉ dành cho hồ sơ thuộc nhóm đối tượng " +
                $"được ưu tiên. {who} không thuộc nhóm ưu tiên nên phải chọn căn thuộc quỹ tiêu chuẩn.");
    }
}
