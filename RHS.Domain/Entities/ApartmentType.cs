namespace RHS.Domain.Entities;

/// <summary>
/// Loại căn hộ trong dự án NOXH, với giá đã được SXD thẩm định.
/// Ví dụ: Studio 38m² 730tr, 1PN 47m² 920tr, 2PN 66m² 1.3 tỷ
/// </summary>
public class ApartmentType
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>Tên loại: "Studio", "1PN", "2PN", "3PN"</summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>Diện tích (m²)</summary>
    public double Area { get; set; }

    /// <summary>Giá bán đã được SXD thẩm định duyệt (VND)</summary>
    public decimal Price { get; set; }

    /// <summary>Số lượng căn loại này trong dự án</summary>
    public int Quantity { get; set; }

    /// <summary>Số lượng còn lại chưa phân bổ</summary>
    public int RemainingQuantity { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public HousingProject HousingProject { get; set; } = null!;
}
