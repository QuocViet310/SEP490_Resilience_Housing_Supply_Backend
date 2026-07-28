namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// Request DTO: gán một căn hộ cụ thể cho hồ sơ đã trúng / chốt suất.
/// </summary>
public class AssignApartmentRequestDto
{
    /// <summary>ID căn hộ (Apartment) còn AVAILABLE trong dự án</summary>
    public Guid ApartmentId { get; set; }
}
