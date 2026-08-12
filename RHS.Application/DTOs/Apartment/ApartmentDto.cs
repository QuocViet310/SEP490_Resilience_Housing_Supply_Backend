namespace RHS.Application.DTOs.Apartment;

public class ApartmentDto
{
    public Guid Id { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public double Area { get; set; }
    public decimal Price { get; set; }
    /// <summary>AVAILABLE | ASSIGNED</summary>
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Model3DUrl { get; set; }
    public string? VirtualTourUrl { get; set; }
}

public class CreateApartmentDto
{
    public string UnitName { get; set; } = string.Empty;
    public double Area { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? Model3DUrl { get; set; }
    public string? VirtualTourUrl { get; set; }
}
