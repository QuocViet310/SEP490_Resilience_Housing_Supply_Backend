namespace RHS.Domain.Entities;

public class Appointment
{
    public Guid AppointmentId { get; set; }

    public Guid ApplicationId { get; set; }

    public DateTime AppointmentDate { get; set; }

    public string Location { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    // Navigation properties
    public HousingApplication HousingApplication { get; set; } = null!;
}
