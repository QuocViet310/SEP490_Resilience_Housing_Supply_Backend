using System;

namespace RHS.Domain.Entities
{
    public class PrincipleAgreement
    {
        public Guid Id { get; set; }

        public Guid ApplicationId { get; set; }

        public string PdfUrl { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public HousingApplication HousingApplication { get; set; } = null!;
    }
}
