using RHS.Application.DTOs.Payment;
using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

/// <summary>
/// Repository thao tác CRUD với bảng Payments.
/// </summary>
public interface IPaymentRepository
{
    /// <summary>Lưu một bản ghi Payment mới vào DB</summary>
    Task CreateAsync(Payment payment);

    /// <summary>Cập nhật bản ghi Payment (dùng khi callback về)</summary>
    Task UpdateAsync(Payment payment);

    /// <summary>Tìm Payment theo mã đơn hàng nội bộ (OrderId)</summary>
    Task<Payment?> GetByOrderIdAsync(string orderId);

    /// <summary>Tìm Payment theo ID</summary>
    Task<Payment?> GetByIdAsync(Guid id);

    /// <summary>Lấy toàn bộ lịch sử thanh toán của một user</summary>
    Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId);

    /// <summary>Lấy danh sách thanh toán phân trang & lọc dành cho Admin</summary>
    Task<(IEnumerable<Payment> Items, int TotalCount)> GetAdminPaymentsPagedAsync(AdminTransactionQueryDto queryDto);

    /// <summary>Lấy danh sách thanh toán phân trang & lọc của một user</summary>
    Task<(IEnumerable<Payment> Items, int TotalCount)> GetUserPaymentsPagedAsync(Guid userId, UserTransactionQueryDto queryDto);
}
