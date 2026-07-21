namespace RHS.Application.DTOs.ApartmentType;

public class ApartmentTypeDto
{
    public Guid Id { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public double Area { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int RemainingQuantity { get; set; }
    public string? Description { get; set; }
}

public class CreateApartmentTypeDto
{
    public string TypeName { get; set; } = string.Empty;
    public double Area { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string? Description { get; set; }
}
