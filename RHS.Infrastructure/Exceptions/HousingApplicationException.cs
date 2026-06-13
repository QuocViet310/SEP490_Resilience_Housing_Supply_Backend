namespace RHS.Infrastructure.Exceptions;

/// <summary>
/// Base exception cho toàn bộ luồng xét duyệt hồ sơ nhà ở xã hội.
/// Mọi exception liên quan đến Housing Application đều kế thừa từ class này,
/// giúp controller bắt theo nhóm bằng một catch duy nhất.
/// </summary>
public abstract class HousingApplicationException : Exception
{
    /// <summary>Mã lỗi nội bộ để phân loại và log (ví dụ: "APP_INVALID_STATUS")</summary>
    public string ErrorCode { get; }

    protected HousingApplicationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    protected HousingApplicationException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
