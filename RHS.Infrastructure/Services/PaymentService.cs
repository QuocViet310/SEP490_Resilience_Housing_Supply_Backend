using Microsoft.AspNetCore.Http;
using RHS.Application.DTOs.Payment;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;

namespace RHS.Infrastructure.Services;

/// <summary>
/// Triển khai IPaymentService – điều phối toàn bộ nghiệp vụ thanh toán:
/// 1. Tạo giao dịch Pending → lấy URL VNPay
/// 2. Xử lý callback → xác minh chữ ký → cập nhật trạng thái DB
/// 3. Cung cấp API tra cứu lịch sử
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IVnPayService _vnPayService;
    private readonly IPaymentRepository _paymentRepository;

    public PaymentService(
        IVnPayService vnPayService,
        IPaymentRepository paymentRepository)
    {
        _vnPayService       = vnPayService;
        _paymentRepository  = paymentRepository;
    }

    /// <inheritdoc/>
    public async Task<PaymentResponseDto> CreatePaymentAsync(
        Guid userId,
        CreatePaymentDto dto,
        HttpContext httpContext)
    {
        // ── 1. Tạo mã đơn hàng duy nhất (timestamp + random suffix) ─────
        var orderId = GenerateOrderId();

        // ── 2. Lưu bản ghi Payment với trạng thái Pending ────────────────
        var payment = new Payment
        {
            Id        = Guid.NewGuid(),
            UserId    = userId,
            OrderId   = orderId,
            OrderInfo = dto.OrderInfo,
            Amount    = dto.Amount,
            Status    = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        await _paymentRepository.CreateAsync(payment);

        // ── 3. Tạo URL thanh toán VNPay ───────────────────────────────────
        var vnpRequest = new VnPaymentRequest
        {
            OrderId     = orderId,
            OrderInfo   = dto.OrderInfo,
            OrderType   = dto.OrderType,
            Amount      = dto.Amount,
            CreatedDate = DateTime.Now   // VNPay dùng giờ local (không phải UTC)
        };

        var paymentUrl = _vnPayService.CreatePaymentUrl(httpContext, vnpRequest);

        return new PaymentResponseDto
        {
            Success    = true,
            Message    = "Tạo URL thanh toán thành công",
            PaymentUrl = paymentUrl,
            OrderId    = orderId,
            Amount     = dto.Amount
        };
    }

    /// <inheritdoc/>
    public async Task<bool> HandleCallbackAsync(IQueryCollection queryParams)
    {
        // ── 1. Xác minh chữ ký HMAC-SHA512 ───────────────────────────────
        var isValidSignature = _vnPayService.ValidateSignature(queryParams);
        if (!isValidSignature)
        {
            return false;
        }

        // ── 2. Lấy mã đơn hàng từ callback ───────────────────────────────
        var orderId = queryParams["vnp_TxnRef"].ToString();
        if (string.IsNullOrEmpty(orderId))
            return false;

        // ── 3. Tìm bản ghi Payment trong DB ──────────────────────────────
        var payment = await _paymentRepository.GetByOrderIdAsync(orderId);
        if (payment == null)
            return false;

        // ── 4. Đọc thông tin phản hồi VNPay ──────────────────────────────
        var responseCode       = queryParams["vnp_ResponseCode"].ToString();
        var transactionStatus  = queryParams["vnp_TransactionStatus"].ToString();
        var transactionNo      = queryParams["vnp_TransactionNo"].ToString();
        var bankCode           = queryParams["vnp_BankCode"].ToString();
        var bankTranNo         = queryParams["vnp_BankTranNo"].ToString();
        var cardType           = queryParams["vnp_CardType"].ToString();
        var payDate            = queryParams["vnp_PayDate"].ToString();

        // ── 5. Cập nhật Payment theo kết quả VNPay ────────────────────────
        // VNPay trả về "00" cho cả ResponseCode và TransactionStatus khi thành công
        payment.VnpResponseCode      = responseCode;
        payment.VnpTransactionStatus = transactionStatus;
        payment.VnpTransactionNo     = transactionNo;
        payment.VnpBankCode          = bankCode;
        payment.VnpBankTranNo        = bankTranNo;
        payment.VnpCardType          = cardType;
        payment.VnpPayDate           = payDate;

        if (responseCode == "00" && transactionStatus == "00")
        {
            payment.Status = "Success";
            payment.PaidAt = DateTime.UtcNow;
        }
        else
        {
            // Map một số mã lỗi thường gặp
            payment.Status = responseCode switch
            {
                "24" => "Cancelled",   // Người dùng hủy giao dịch
                _    => "Failed"
            };
        }

        await _paymentRepository.UpdateAsync(payment);
        return true;
    }

    /// <inheritdoc/>
    public async Task<PaymentInfoDto?> GetPaymentByOrderIdAsync(string orderId)
    {
        var payment = await _paymentRepository.GetByOrderIdAsync(orderId);
        return payment == null ? null : MapToInfoDto(payment);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentInfoDto>> GetPaymentsByUserIdAsync(Guid userId)
    {
        var payments = await _paymentRepository.GetByUserIdAsync(userId);
        return payments.Select(MapToInfoDto);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Tạo mã đơn hàng duy nhất: yyyyMMddHHmmss + 4 chữ số random.
    /// Giới hạn 50 ký tự theo cấu hình DB.
    /// Ví dụ: 202505291304230042
    /// </summary>
    private static string GenerateOrderId()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var random    = new Random().Next(1000, 9999);
        return $"{timestamp}{random}";
    }

    /// <summary>Map Payment entity sang PaymentInfoDto trả về client</summary>
    private static PaymentInfoDto MapToInfoDto(Payment payment) => new()
    {
        Id               = payment.Id,
        OrderId          = payment.OrderId,
        OrderInfo        = payment.OrderInfo,
        Amount           = payment.Amount,
        Status           = payment.Status,
        VnpResponseCode  = payment.VnpResponseCode,
        VnpTransactionNo = payment.VnpTransactionNo,
        VnpBankCode      = payment.VnpBankCode,
        VnpPayDate       = payment.VnpPayDate,
        CreatedAt        = payment.CreatedAt,
        PaidAt           = payment.PaidAt
    };
}
