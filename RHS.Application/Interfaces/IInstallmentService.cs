using Microsoft.AspNetCore.Http;
using RHS.Application.DTOs.Installment;
using RHS.Application.DTOs.Payment;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service quản lý lịch đóng tiền theo đợt (Event-Driven + Template Pattern), tính lãi phạt trễ hạn (0.05%/ngày)
/// và xử lý nghiệp vụ đơn xin ngừng thanh toán (Maker-Checker approval), phạt cọc & hủy căn hộ.
/// </summary>
public interface IInstallmentService
{
    /// <summary>
    /// Kích hoạt sự kiện → sinh PaymentInstallment từ milestones phù hợp.
    /// Idempotent: nếu installment đã tồn tại cho milestone, sẽ bỏ qua.
    /// </summary>
    Task FireTriggerEventAsync(Guid applicationId, string triggerEvent, DateTime eventDate);

    /// <summary>
    /// Lấy tổng hợp lịch đóng tiền theo hồ sơ (tất cả đợt, kèm summary và lãi phạt trễ hạn tích lũy).
    /// </summary>
    Task<InstallmentSummaryDto?> GetSummaryAsync(Guid applicationId);

    /// <summary>
    /// Tạo URL VNPay thanh toán cho đợt cụ thể (PaymentInstallment).
    /// Nếu đợt quá hạn, số tiền thanh toán bao gồm gốc + tiền lãi phạt trễ hạn 0.05%/ngày.
    /// </summary>
    Task<PaymentResponseDto> CreateInstallmentPaymentAsync(
        Guid userId, Guid installmentId, HttpContext httpContext);

    /// <summary>
    /// Xử lý sau khi VNPay callback thành công cho 1 installment.
    /// </summary>
    Task ProcessInstallmentPaidAsync(Guid installmentId, Guid paymentId);

    /// <summary>
    /// Background job: scan các installments quá hạn → OVERDUE + notification.
    /// </summary>
    Task ProcessOverdueInstallmentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Chủ đầu tư chọn Thời điểm phát hành (triggerEvent) để mở/unlock đợt tiến độ cho toàn dự án.
    /// </summary>
    Task<int> UnlockPhaseByEventAsync(Guid projectId, string triggerEvent);

    /// <summary>
    /// Xem trước bảng kê chi tiết phạt cọc Đợt 1, tiền đợt 2+ đã đóng, khấu trừ phạt và tiền thực hoàn khi hủy căn.
    /// </summary>
    Task<ContractCancellationPreviewDto> PreviewContractCancellationAsync(Guid applicationId);

    /// <summary>
    /// Nộp đơn xin ngừng thanh toán / rút hồ sơ tự nguyện (Dành cho Người dân).
    /// Chuyển ApplicationStatus sang CANCELLATION_REQUESTED và gửi thông báo cho CĐT duyệt.
    /// </summary>
    Task<ContractCancellationResultDto> SubmitCancellationRequestAsync(
        Guid userId, Guid applicationId, CancelContractRequestDto dto);

    /// <summary>
    /// Chủ đầu tư phê duyệt đơn xin ngừng thanh toán:
    /// - Thực hiện phạt cọc Đợt 1, hoàn tiền Đợt 2+ sau trừ phạt.
    /// - Chuyển hồ sơ sang CANCELED, giải phóng căn hộ và tự động đôn Waitlist nếu có.
    /// </summary>
    Task<ContractCancellationResultDto> ApproveCancellationRequestAsync(
        Guid userId, Guid applicationId);

    /// <summary>
    /// Chủ đầu tư từ chối đơn xin ngừng thanh toán:
    /// - Khôi phục trạng thái hồ sơ về trạng thái hoạt động trước đó.
    /// - Gửi thông báo lý do từ chối cho người dân.
    /// </summary>
    Task<ContractCancellationResultDto> RejectCancellationRequestAsync(
        Guid userId, Guid applicationId, RejectCancellationRequestDto dto);

    /// <summary>
    /// Lấy danh sách tất cả các đơn xin ngừng thanh toán đang chờ CĐT duyệt theo dự án.
    /// </summary>
    Task<List<CancellationRequestItemDto>> GetPendingCancellationRequestsAsync(Guid projectId);

    /// <summary>
    /// Cưỡng chế hủy hợp đồng & thu hồi căn do CĐT đơn phương chấm dứt (quá 2 đợt trễ hạn không thanh toán).
    /// </summary>
    Task<ContractCancellationResultDto> CancelContractAndProcessRefundAsync(
        Guid userId, Guid applicationId, CancelContractRequestDto dto);

    /// <summary>
    /// Báo cáo tổng quan tiến độ thu tiền & nợ phạt trễ hạn theo dự án cho Chủ đầu tư / SXD.
    /// </summary>
    Task<ProjectPaymentProgressDto> GetProjectPaymentProgressAsync(Guid projectId);
}
