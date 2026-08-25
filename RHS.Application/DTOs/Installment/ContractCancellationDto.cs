namespace RHS.Application.DTOs.Installment;

/// <summary>
/// DTO gửi yêu cầu hủy hợp đồng / hủy căn hộ.
/// Support 2 luồng: Tự nguyện rút hồ sơ OR Cưỡng chế thu hồi căn (nếu quá 2 đợt trễ hạn).
/// </summary>
public class CancelContractRequestDto
{
    public string Reason { get; set; } = string.Empty;

    /// <summary>Truyền true nếu CĐT cưỡng chế thu hồi căn do người mua chậm đóng từ 2 đợt trở lên</summary>
    public bool IsForcedRevocation { get; set; } = false;

    public string? BankAccountNumber { get; set; }

    public string? BankName { get; set; }

    public string? AccountHolderName { get; set; }
}

/// <summary>
/// DTO gửi yêu cầu từ chối đơn xin ngừng thanh toán từ CĐT.
/// </summary>
public class RejectCancellationRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// DTO thông tin đơn xin ngừng thanh toán đang chờ CĐT duyệt.
/// </summary>
public class CancellationRequestItemDto
{
    public Guid ApplicationId { get; set; }

    public string ApplicantName { get; set; } = string.Empty;

    public string CitizenId { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? ApartmentUnitName { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? BankAccountNumber { get; set; }

    public string? BankName { get; set; }

    public string? AccountHolderName { get; set; }

    public decimal Phase1DepositForfeited { get; set; }

    public decimal Phase2PlusPaidAmount { get; set; }

    public decimal UnpaidPenaltyAmount { get; set; }

    public decimal NetRefundAmount { get; set; }

    public DateTime RequestedAt { get; set; }
}

/// <summary>
/// DTO xem trước bảng tính chi tiết hoàn tiền & phạt cọc khi hủy hợp đồng.
/// </summary>
public class ContractCancellationPreviewDto
{
    public Guid ApplicationId { get; set; }

    public string ApplicantName { get; set; } = string.Empty;

    public string? ApartmentUnitName { get; set; }

    public decimal? ApartmentPrice { get; set; }

    /// <summary>Trạng thái hồ sơ hiện tại</summary>
    public string CurrentApplicationStatus { get; set; } = string.Empty;

    /// <summary>Có đủ điều kiện thực hiện hủy hợp đồng hay không</summary>
    public bool CanCancel { get; set; }

    public string? Message { get; set; }

    /// <summary>Số đợt thu đang quá hạn</summary>
    public int OverduePhasesCount { get; set; }

    /// <summary>Đủ điều kiện cưỡng chế thu hồi căn do CĐT đơn phương chấm dứt (OverduePhasesCount >= 2)</summary>
    public bool IsEligibleForForcedRevocation { get; set; }

    /// <summary>Số tiền Đợt 1 (Đặt cọc)</summary>
    public decimal Phase1Amount { get; set; }

    /// <summary>Số tiền Đợt 1 đã đóng</summary>
    public decimal Phase1PaidAmount { get; set; }

    /// <summary>Số tiền cọc bị tịch thu (Phạt cọc = Phase1PaidAmount)</summary>
    public decimal DepositForfeited { get; set; }

    /// <summary>Tổng số tiền các Đợt 2+ đã đóng</summary>
    public decimal Phase2PlusPaidAmount { get; set; }

    /// <summary>Tổng tiền lãi phạt trễ hạn 0.05%/ngày chưa thanh toán</summary>
    public decimal TotalUnpaidPenalty { get; set; }

    /// <summary>Số tiền thực hoàn trả cho người dân = Max(0, Phase2PlusPaidAmount - TotalUnpaidPenalty)</summary>
    public decimal RefundAmount { get; set; }

    /// <summary>Tên ứng viên tiếp theo trong Danh sách chờ (Waitlist) sẽ được đôn lên nhận căn hộ thu hồi (nếu có)</summary>
    public string? PromotedWaitlistApplicantName { get; set; }

    /// <summary>Chi tiết các đợt thu</summary>
    public List<InstallmentDto> Installments { get; set; } = new();
}

/// <summary>
/// DTO trả về kết quả sau khi thực hiện hủy hợp đồng & phạt cọc thành công.
/// </summary>
public class ContractCancellationResultDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public Guid ApplicationId { get; set; }

    /// <summary>Là cưỡng chế thu hồi do quá 2 đợt hay tự nguyện rút hồ sơ</summary>
    public bool IsForcedRevocation { get; set; }

    /// <summary>Số tiền cọc bị tịch thu (Phạt cọc)</summary>
    public decimal DepositForfeited { get; set; }

    /// <summary>Số tiền thực hoàn trả</summary>
    public decimal RefundAmount { get; set; }

    /// <summary>Số tiền lãi phạt trễ hạn đã trừ</summary>
    public decimal TotalPenaltyDeducted { get; set; }

    /// <summary>ID ứng viên được đôn từ Danh sách chờ (Waitlist) lên nhận căn hộ thu hồi (nếu có)</summary>
    public Guid? PromotedWaitlistApplicantId { get; set; }

    /// <summary>Họ tên ứng viên được đôn từ Danh sách chờ (Waitlist)</summary>
    public string? PromotedWaitlistApplicantName { get; set; }

    public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO báo cáo tiến độ thu tiền & nợ phạt theo dự án dành cho Chủ đầu tư / SXD.
/// </summary>
public class ProjectPaymentProgressDto
{
    public Guid ProjectId { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public int TotalApplications { get; set; }

    /// <summary>Tổng tiền dự kiến thu theo tất cả hợp đồng (VND)</summary>
    public decimal TotalExpectedAmount { get; set; }

    /// <summary>Tổng tiền thực tế đã thu (VND)</summary>
    public decimal TotalCollectedAmount { get; set; }

    /// <summary>Tổng nợ gốc quá hạn (VND)</summary>
    public decimal TotalOverdueAmount { get; set; }

    /// <summary>Tổng tiền lãi phạt 0.05%/ngày tích lũy (VND)</summary>
    public decimal TotalAccruedPenalties { get; set; }

    /// <summary>Tỷ lệ thu hồi vốn (%)</summary>
    public double CollectionRatePercentage { get; set; }

    public List<ApplicationProgressItemDto> Items { get; set; } = new();
}

/// <summary>
/// Chi tiết tiến độ thu tiền của 1 hồ sơ trong dự án.
/// </summary>
public class ApplicationProgressItemDto
{
    public Guid ApplicationId { get; set; }

    public string ApplicantName { get; set; } = string.Empty;

    public string CitizenId { get; set; } = string.Empty;

    public string? SlotCode { get; set; }

    public string? ApartmentUnitName { get; set; }

    public decimal TotalContractAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingAmount { get; set; }

    public decimal AccruedPenalty { get; set; }

    public int PaidPhasesCount { get; set; }

    public int OverduePhasesCount { get; set; }

    public bool IsEligibleForForcedRevocation => OverduePhasesCount >= 2;

    public string ApplicationStatus { get; set; } = string.Empty;
}
