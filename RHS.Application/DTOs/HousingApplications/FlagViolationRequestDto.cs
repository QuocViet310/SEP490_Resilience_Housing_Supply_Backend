namespace RHS.Application.DTOs.HousingApplications;

/// <summary>
/// DTO chứa lý do gắn cờ vi phạm hồ sơ.
/// </summary>
public class FlagViolationRequestDto
{
    public string Reason { get; set; } = string.Empty;
}
