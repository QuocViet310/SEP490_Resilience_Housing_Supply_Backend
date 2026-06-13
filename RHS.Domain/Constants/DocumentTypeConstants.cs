namespace RHS.Domain.Constants;

/// <summary>
/// Định nghĩa các hằng số loại giấy tờ được phép upload trong hồ sơ nhà ở xã hội.
/// Applicant chỉ được chọn 1 trong 2 loại chính theo nghiệp vụ.
/// </summary>
public static class DocumentTypeConstants
{
    /// <summary>
    /// Giấy tờ chứng minh điều kiện nhà ở
    /// (ví dụ: xác nhận chưa có nhà, hoặc diện tích nhà ở < 15m²)
    /// </summary>
    public const string HousingConditionProof = "HOUSING_CONDITION_PROOF";

    /// <summary>
    /// Giấy chứng nhận hộ nghèo/cận nghèo
    /// (do UBND Phường/Xã cấp)
    /// </summary>
    public const string PovertyHouseholdCertificate = "POVERTY_HOUSEHOLD_CERTIFICATE";

    /// <summary>
    /// Danh sách 2 loại giấy tờ chính mà Applicant được phép chọn upload.
    /// Applicant CHỈ ĐƯỢC upload 1 trong 2 loại này.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedApplicantDocumentTypes = new[]
    {
        HousingConditionProof,
        PovertyHouseholdCertificate
    };

    /// <summary>
    /// Kiểm tra loại giấy tờ có phải là loại Applicant được phép upload không.
    /// </summary>
    public static bool IsAllowedApplicantType(string documentType)
        => AllowedApplicantDocumentTypes.Contains(documentType);
}
