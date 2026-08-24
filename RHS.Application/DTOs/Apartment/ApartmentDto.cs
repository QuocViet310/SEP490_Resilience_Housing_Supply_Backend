using System.ComponentModel.DataAnnotations;

namespace RHS.Application.DTOs.Apartment;

/// <summary>
/// DTO thông tin chi tiết căn hộ.
/// </summary>
public class ApartmentDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public int FloorNumber { get; set; }
    public string? BuildingBlock { get; set; }
    public int NumberOfBedrooms { get; set; }
    public int NumberOfBathrooms { get; set; }
    public double Area { get; set; }
    public double? GrossArea { get; set; }
    public string? MainDoorDirection { get; set; }
    public string? MainDoorDirectionLabel { get; set; }
    public string? BalconyDirection { get; set; }
    public string? BalconyDirectionLabel { get; set; }
    public string? ViewDescription { get; set; }
    public int? MaxOccupants { get; set; }
    public decimal? MinSuitableIncome { get; set; }
    public decimal? MaxSuitableIncome { get; set; }
    public string UnitGroup { get; set; } = "STANDARD";
    public string UnitGroupLabel { get; set; } = "Căn Hộ Tiêu Chuẩn";
    public string SaleType { get; set; } = "FULL_OWNERSHIP";
    public string SaleTypeLabel { get; set; } = "Sở hữu 100%";
    public decimal? CoOwnershipRatio { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "AVAILABLE";
    public string? Description { get; set; }
    public string? Model3DUrl { get; set; }
    public string? VirtualTourUrl { get; set; }
    public Guid? ApartmentTypeId { get; set; }
    public string ApartmentType { get; set; } = string.Empty;
    public string ApartmentTypeLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO tạo 1 căn hộ mới.
/// </summary>
public class CreateApartmentDto
{
    [Required(ErrorMessage = "Mã/Tên căn hộ không được để trống.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Mã căn hộ từ 1 đến 50 ký tự.")]
    public string UnitName { get; set; } = string.Empty;

    [Range(1, 200, ErrorMessage = "Tầng phải từ 1 đến 200.")]
    public int FloorNumber { get; set; } = 1;

    [StringLength(50, ErrorMessage = "Tên tòa/block không quá 50 ký tự.")]
    public string? BuildingBlock { get; set; }

    [Range(1, 10, ErrorMessage = "Số phòng ngủ từ 1 đến 10.")]
    public int NumberOfBedrooms { get; set; } = 1;

    [Range(1, 10, ErrorMessage = "Số phòng vệ sinh từ 1 đến 10.")]
    public int NumberOfBathrooms { get; set; } = 1;

    [Range(15.0, 300.0, ErrorMessage = "Diện tích thông thủy phải từ 15m² đến 300m².")]
    public double Area { get; set; }

    [Range(15.0, 350.0, ErrorMessage = "Diện tích tim tường phải từ 15m² đến 350m².")]
    public double? GrossArea { get; set; }

    public string? MainDoorDirection { get; set; }
    public string? BalconyDirection { get; set; }
    public string? ViewDescription { get; set; }

    [Range(1, 20, ErrorMessage = "Sức chứa khuyến nghị từ 1 đến 20 người.")]
    public int? MaxOccupants { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Thu nhập tối thiểu không hợp lệ.")]
    public decimal? MinSuitableIncome { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Thu nhập tối đa không hợp lệ.")]
    public decimal? MaxSuitableIncome { get; set; }

    /// <summary>PRIORITY (Ưu tiên) | STANDARD (Tiêu chuẩn)</summary>
    public string UnitGroup { get; set; } = "STANDARD";

    /// <summary>FULL_OWNERSHIP (Sở hữu 100%) | CO_OWNERSHIP (Đồng sở hữu)</summary>
    public string SaleType { get; set; } = "FULL_OWNERSHIP";

    [Range(1.0, 99.0, ErrorMessage = "Tỷ lệ đồng sở hữu phải từ 1% đến 99%.")]
    public decimal? CoOwnershipRatio { get; set; }

    [Range(1000000, 100000000000, ErrorMessage = "Giá bán phải lớn hơn 1.000.000 VND.")]
    public decimal Price { get; set; }

    public Guid? ApartmentTypeId { get; set; }
    public string? ApartmentType { get; set; }
    public string? Description { get; set; }
    public string? Model3DUrl { get; set; }
    public string? VirtualTourUrl { get; set; }
}

/// <summary>
/// DTO cập nhật thông tin căn hộ.
/// </summary>
public class UpdateApartmentDto
{
    [Required(ErrorMessage = "Mã/Tên căn hộ không được để trống.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Mã căn hộ từ 1 đến 50 ký tự.")]
    public string UnitName { get; set; } = string.Empty;

    [Range(1, 200, ErrorMessage = "Tầng phải từ 1 đến 200.")]
    public int FloorNumber { get; set; } = 1;

    [StringLength(50, ErrorMessage = "Tên tòa/block không quá 50 ký tự.")]
    public string? BuildingBlock { get; set; }

    [Range(1, 10, ErrorMessage = "Số phòng ngủ từ 1 đến 10.")]
    public int NumberOfBedrooms { get; set; } = 1;

    [Range(1, 10, ErrorMessage = "Số phòng vệ sinh từ 1 đến 10.")]
    public int NumberOfBathrooms { get; set; } = 1;

    [Range(15.0, 300.0, ErrorMessage = "Diện tích thông thủy phải từ 15m² đến 300m².")]
    public double Area { get; set; }

    [Range(15.0, 350.0, ErrorMessage = "Diện tích tim tường phải từ 15m² đến 350m².")]
    public double? GrossArea { get; set; }

    public string? MainDoorDirection { get; set; }
    public string? BalconyDirection { get; set; }
    public string? ViewDescription { get; set; }

    [Range(1, 20, ErrorMessage = "Sức chứa khuyến nghị từ 1 đến 20 người.")]
    public int? MaxOccupants { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Thu nhập tối thiểu không hợp lệ.")]
    public decimal? MinSuitableIncome { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Thu nhập tối đa không hợp lệ.")]
    public decimal? MaxSuitableIncome { get; set; }

    public string UnitGroup { get; set; } = "STANDARD";
    public string SaleType { get; set; } = "FULL_OWNERSHIP";

    [Range(1.0, 99.0, ErrorMessage = "Tỷ lệ đồng sở hữu phải từ 1% đến 99%.")]
    public decimal? CoOwnershipRatio { get; set; }

    [Range(1000000, 100000000000, ErrorMessage = "Giá bán phải lớn hơn 1.000.000 VND.")]
    public decimal Price { get; set; }

    public Guid? ApartmentTypeId { get; set; }
    public string? ApartmentType { get; set; }
    public string? Description { get; set; }
    public string? Model3DUrl { get; set; }
    public string? VirtualTourUrl { get; set; }
}

/// <summary>
/// DTO tạo hàng loạt căn hộ (Batch create theo tầng/block).
/// </summary>
public class BatchCreateApartmentsRequestDto
{
    [Required(ErrorMessage = "Danh sách căn hộ không được để trống.")]
    [MinLength(1, ErrorMessage = "Phải có ít nhất 1 căn hộ.")]
    public List<CreateApartmentDto> Apartments { get; set; } = new();
}

/// <summary>
/// DTO tìm kiếm và lọc căn hộ.
/// </summary>
public class ApartmentFilterRequestDto
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
    public int? FloorNumber { get; set; }
    public string? BuildingBlock { get; set; }
    public Guid? ApartmentTypeId { get; set; }
    public string? ApartmentTypeCode { get; set; }
    public string? UnitGroup { get; set; }
    public string? SaleType { get; set; }
    public string? Status { get; set; }
    public string? Direction { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public double? MinArea { get; set; }
    public double? MaxArea { get; set; }
    public int? NumberOfBedrooms { get; set; }
}

/// <summary>
/// Sơ đồ mặt bằng dự án gom nhóm theo Block và Tầng.
/// </summary>
public class FloorPlanResponseDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalApartments { get; set; }
    public int AvailableApartments { get; set; }
    public int AssignedApartments { get; set; }
    public List<FloorPlanBlockDto> Blocks { get; set; } = new();
}

public class FloorPlanBlockDto
{
    public string BlockName { get; set; } = string.Empty;
    public int TotalApartmentsInBlock { get; set; }
    public List<FloorPlanFloorDto> Floors { get; set; } = new();
}

public class FloorPlanFloorDto
{
    public int FloorNumber { get; set; }
    public int TotalApartmentsOnFloor { get; set; }
    public List<ApartmentDto> Apartments { get; set; } = new();
}

/// <summary>
/// Thống kê cơ cấu căn hộ của dự án.
/// </summary>
public class ApartmentStatisticsResponseDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalUnits { get; set; }
    public int AvailableUnits { get; set; }
    public int AssignedUnits { get; set; }
    public int PriorityUnits { get; set; }
    public int StandardUnits { get; set; }
    public int FullOwnershipUnits { get; set; }
    public int CoOwnershipUnits { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public double MinArea { get; set; }
    public double MaxArea { get; set; }
    public List<ApartmentTypeStatDto> ByType { get; set; } = new();
    public List<FloorStatDto> ByFloor { get; set; } = new();
}

public class ApartmentTypeStatDto
{
    public Guid? TypeId { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int AvailableCount { get; set; }
    public int AssignedCount { get; set; }
}

public class FloorStatDto
{
    public string? Block { get; set; }
    public int FloorNumber { get; set; }
    public int TotalCount { get; set; }
    public int AvailableCount { get; set; }
    public int AssignedCount { get; set; }
}
