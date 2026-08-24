namespace RHS.Application.DTOs.Eligibility;

/// <summary>
/// Kết quả thẩm định tự động điều kiện mua nhà ở xã hội (Rule Engine).
/// </summary>
public class EligibilityResultDto
{
    public Guid AssessmentId { get; set; }
    public Guid? ApplicationId { get; set; }

    /// <summary>Đạt toàn bộ điều kiện hưởng chính sách NOXH hay không</summary>
    public bool Eligible { get; set; }

    /// <summary>Điểm ưu tiên ước tính (0 - 100)</summary>
    public decimal EstimatedScore { get; set; }

    /// <summary>Đạt điều kiện nhóm đối tượng thụ hưởng (Đ76)</summary>
    public bool PriorityGroupCheckPassed { get; set; }

    /// <summary>Đạt điều kiện thu nhập (&lt; 15 triệu/người/tháng - Đ30)</summary>
    public bool IncomeCheckPassed { get; set; }

    /// <summary>Đạt điều kiện thực trạng nhà ở &amp; diện tích (&lt; 10m²/người - Đ29)</summary>
    public bool HousingAreaCheckPassed { get; set; }

    /// <summary>Tổng thu nhập hàng tháng được tính để xét duyệt (VND)</summary>
    public decimal? TotalHouseholdIncome { get; set; }

    /// <summary>Mức trần thu nhập tối đa được phép đối với hồ sơ này (VND)</summary>
    public decimal? MaxAllowedIncome { get; set; }

    /// <summary>Diện tích nhà ở bình quân đầu người tính toán (m²/người)</summary>
    public decimal? CalculatedAverageArea { get; set; }

    /// <summary>Mức diện tích bình quân tối đa cho phép đối với nhà chật (m²/người, mặc định 10m²)</summary>
    public decimal? MaxAllowedAreaPerPerson { get; set; }

    /// <summary>Thông điệp tóm tắt kết luận thẩm định</summary>
    public string SummaryMessage { get; set; } = string.Empty;

    /// <summary>Danh sách chi tiết các lý do và căn cứ quy định</summary>
    public List<string> Reasons { get; set; } = new();

    public DateTime AssessmentDate { get; set; }
}
