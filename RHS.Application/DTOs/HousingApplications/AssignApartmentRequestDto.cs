namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO cho API gán loại căn hộ cho hồ sơ đã trúng bốc thăm.
/// </summary>
public class AssignApartmentRequestDto
{
    /// <summary>ID loại căn hộ (ApartmentType) được gán</summary>
    public Guid ApartmentTypeId { get; set; }
}
