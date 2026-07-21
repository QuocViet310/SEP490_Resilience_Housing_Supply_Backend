using Microsoft.AspNetCore.Http;
using RHS.Application.DTOs.Installment;
using RHS.Application.DTOs.Payment;

namespace RHS.Application.Interfaces;

/// <summary>
/// Service quản lý lịch đóng tiền theo đợt (Event-Driven + Template Pattern).
/// - PaymentMilestone = template cấu hình bởi CĐT
/// - PaymentInstallment = khoản thu thực tế, sinh tự động khi event fires
/// </summary>
public interface IInstallmentService
{
    /// <summary>
    /// Kích hoạt sự kiện → sinh PaymentInstallment từ milestones phù hợp.
    /// Idempotent: nếu installment đã tồn tại cho milestone, sẽ bỏ qua.
    /// </summary>
    /// <param name="applicationId">Hồ sơ đăng ký</param>
    /// <param name="triggerEvent">Sự kiện: ON_APPROVED, ON_LOTTERY_WON, ON_CONTRACT_SIGNED</param>
    /// <param name="eventDate">Ngày sự kiện xảy ra (dùng tính DueDate = eventDate + DueDays)</param>
    Task FireTriggerEventAsync(Guid applicationId, string triggerEvent, DateTime eventDate);

    /// <summary>
    /// Lấy tổng hợp lịch đóng tiền theo hồ sơ (tất cả đợt, kèm summary).
    /// </summary>
    Task<InstallmentSummaryDto?> GetSummaryAsync(Guid applicationId);

    /// <summary>
    /// Tạo URL VNPay thanh toán cho đợt cụ thể (PaymentInstallment).
    /// Validate: chỉ cho thanh toán đợt PENDING/OVERDUE, chủ hồ sơ, đúng thứ tự.
    /// </summary>
    Task<PaymentResponseDto> CreateInstallmentPaymentAsync(
        Guid userId, Guid installmentId, HttpContext httpContext);

    /// <summary>
    /// Xử lý sau khi VNPay callback thành công cho 1 installment.
    /// Cập nhật installment → PAID, kiểm tra nếu tất cả đợt PAID → Application = FULLY_PAID.
    /// </summary>
    Task ProcessInstallmentPaidAsync(Guid installmentId, Guid paymentId);

    /// <summary>
    /// Background job: scan các installments quá hạn → OVERDUE + notification.
    /// Gọi từ OverduePaymentWorker mỗi đêm.
    /// </summary>
    Task ProcessOverdueInstallmentsAsync(CancellationToken ct = default);
}
