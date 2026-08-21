namespace RHS.Application.DTOs.Apartment;

public class ApartmentDto
{
    public Guid Id { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public double Area { get; set; }
    public decimal Price { get; set; }
    /// <summary>AVAILABLE | ASSIGNED</summary>
    public string Status { get; set; } = string.Empty;
    public Guid? ApartmentTypeId { get; set; }
    /// <summary>ONE_BEDROOM (1 phòng ngủ) | TWO_BEDROOM (2 phòng ngủ)</summary>
    public string ApartmentType { get; set; } = string.Empty;
    public string ApartmentTypeLabel { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Model3DUrl { get; set; }
    public string? VirtualTourUrl { get; set; }
}

public class CreateApartmentDto
{
    public string UnitName { get; set; } = string.Empty;
    public double Area { get; set; }
    public decimal Price { get; set; }
    /// <summary>ID của Loại căn hộ (ApartmentType Entity). Nếu không truyền có thể dùng mã TypeCode.</summary>
    public Guid? ApartmentTypeId { get; set; }
    /// <summary>Mã loại căn hộ: ONE_BEDROOM (1 phòng ngủ) | TWO_BEDROOM (2 phòng ngủ)</summary>
    public string? ApartmentType { get; set; }
    public string? Description { get; set; }
    public string? Model3DUrl { get; set; }
    public string? VirtualTourUrl { get; set; }
}


