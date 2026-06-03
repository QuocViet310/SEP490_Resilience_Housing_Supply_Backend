namespace RHS.Infrastructure.Exceptions;

/// <summary>
/// Base exception cho toàn bộ luồng eKYC.
/// Mọi exception liên quan đến eKYC đều kế thừa từ class này,
/// giúp caller có thể bắt theo nhóm bằng một <c>catch (EKycException)</c> duy nhất.
/// </summary>
public abstract class EKycException : Exception
{
    /// <summary>
    /// Mã lỗi nội bộ để phân loại và log, ví dụ: "EKYC_TIMEOUT", "EKYC_INVALID_FILE".
    /// </summary>
    public string ErrorCode { get; }

    protected EKycException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    protected EKycException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
