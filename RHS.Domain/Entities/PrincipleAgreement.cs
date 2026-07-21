using System;

namespace RHS.Domain.Entities
{
    public class PrincipleAgreement
    {
        public Guid Id { get; set; }

        public Guid ApplicationId { get; set; }

        public string PdfUrl { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Đánh dấu hợp đồng đã được ký (đồng ý điều khoản)</summary>
        public bool IsSigned { get; set; } = false;

        /// <summary>Thời điểm ký</summary>
        public DateTime? SignedAt { get; set; }

        /// <summary>IP address lúc ký (consent log)</summary>
        public string? SignedIpAddress { get; set; }

        // Navigation property
        public HousingApplication HousingApplication { get; set; } = null!;
    }
}
