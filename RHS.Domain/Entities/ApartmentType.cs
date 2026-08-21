namespace RHS.Domain.Entities;

/// <summary>
/// Thực thể loại căn hộ trong dự án nhà ở xã hội (ví dụ: 1 phòng ngủ, 2 phòng ngủ...).
/// </summary>
public class ApartmentType
{
    public Guid Id { get; set; }

    /// <summary>Mã loại căn hộ: "ONE_BEDROOM", "TWO_BEDROOM", ...</summary>
    public string TypeCode { get; set; } = string.Empty;

    /// <summary>Tên hiển thị: "Căn hộ 1 phòng ngủ", "Căn hộ 2 phòng ngủ", ...</summary>
    public string TypeName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Apartment> Apartments { get; set; } = new List<Apartment>();
}
