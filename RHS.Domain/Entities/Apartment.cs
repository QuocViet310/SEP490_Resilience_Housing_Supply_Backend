namespace RHS.Domain.Entities;

/// <summary>
/// Một căn hộ cụ thể trong dự án NOXH (mã căn, tầng, tòa, diện tích, giá, phân nhóm, trạng thái bàn giao...).
/// </summary>
public class Apartment
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    /// <summary>Tên / Mã căn: "A-101", "B-0502", "Studio tầng 5", …</summary>
    public string UnitName { get; set; } = string.Empty;

    /// <summary>Số tầng (Floor / FloorNumber): 1, 2, 3...</summary>
    public int FloorNumber { get; set; } = 1;

    /// <summary>Tòa nhà / Phân khu (Block / Building): "Tòa A", "Block B", "Tháp 1"...</summary>
    public string? BuildingBlock { get; set; }

    /// <summary>Số phòng ngủ: 1, 2, 3...</summary>
    public int NumberOfBedrooms { get; set; } = 1;

    /// <summary>Số phòng vệ sinh: 1, 2...</summary>
    public int NumberOfBathrooms { get; set; } = 1;

    /// <summary>Diện tích thông thủy (m²) - Net Area</summary>
    public double Area { get; set; }

    /// <summary>Diện tích tim tường / sàn xây dựng (m²) - Gross Area</summary>
    public double? GrossArea { get; set; }

    /// <summary>Hướng cửa chính: EAST, WEST, SOUTH, NORTH, SOUTH_EAST, NORTH_EAST, SOUTH_WEST, NORTH_WEST</summary>
    public string? MainDoorDirection { get; set; }

    /// <summary>Hướng ban công / cửa sổ chính</summary>
    public string? BalconyDirection { get; set; }

    /// <summary>Mô tả tầm nhìn (View): "Công viên nội khu", "Hồ cảnh quan", "Mặt đường chính"...</summary>
    public string? ViewDescription { get; set; }

    /// <summary>Sức chứa số người khuyến nghị (người)</summary>
    public int? MaxOccupants { get; set; }

    /// <summary>Mức thu nhập tối thiểu khuyến nghị phù hợp mua căn hộ (VND/tháng)</summary>
    public decimal? MinSuitableIncome { get; set; }

    /// <summary>Mức thu nhập tối đa khuyến nghị phù hợp mua căn hộ (VND/tháng)</summary>
    public decimal? MaxSuitableIncome { get; set; }

    /// <summary>
    /// Phân nhóm quỹ căn: PRIORITY (Căn Ưu Tiên) | STANDARD (Căn Tiêu Chuẩn)
    /// </summary>
    public string UnitGroup { get; set; } = "STANDARD";

    /// <summary>
    /// Hình thức mở bán: FULL_OWNERSHIP (Sở hữu 100%) | CO_OWNERSHIP (Đồng sở hữu)
    /// </summary>
    public string SaleType { get; set; } = "FULL_OWNERSHIP";

    /// <summary>Tỷ lệ đồng sở hữu (%) nếu SaleType = CO_OWNERSHIP</summary>
    public decimal? CoOwnershipRatio { get; set; }

    /// <summary>Giá bán đã thẩm định (VND)</summary>
    public decimal Price { get; set; }

    /// <summary>AVAILABLE | ASSIGNED — xem ApartmentStatusConstants</summary>
    public string Status { get; set; } = "AVAILABLE";

    public string? Description { get; set; }

    /// <summary>URL file mô hình 3D (.glb)</summary>
    public string? Model3DUrl { get; set; }

    /// <summary>URL tour 360 / iframe nhúng (Matterport, Sketchfab, Kuula...)</summary>
    public string? VirtualTourUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Khóa ngoại liên kết tới loại căn hộ (ApartmentType)</summary>
    public Guid? ApartmentTypeId { get; set; }

    public ApartmentType? ApartmentType { get; set; }

    public HousingProject HousingProject { get; set; } = null!;

    public ICollection<HousingApplication> HousingApplications { get; set; }
        = new List<HousingApplication>();
}
