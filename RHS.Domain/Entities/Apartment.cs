namespace RHS.Domain.Entities;

/// <summary>
/// Một căn hộ cụ thể trong dự án NOXH (tên / diện tích / giá / trạng thái bàn giao).
/// </summary>
public class Apartment
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>Tên căn: "A-101", "Studio tầng 5", …</summary>
    public string UnitName { get; set; } = string.Empty;

    /// <summary>Diện tích (m²)</summary>
    public double Area { get; set; }

    /// <summary>Giá bán đã thẩm định (VND)</summary>
    public decimal Price { get; set; }

    /// <summary>AVAILABLE | ASSIGNED — xem ApartmentStatusConstants</summary>
    public string Status { get; set; } = "AVAILABLE";

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public HousingProject HousingProject { get; set; } = null!;

    public ICollection<HousingApplication> HousingApplications { get; set; }
        = new List<HousingApplication>();
}
