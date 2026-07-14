namespace RHS.Domain.Constants;

/// <summary>
/// Loại giấy tờ bắt buộc khi nộp hồ sơ mua NOXH (đối tượng hộ nghèo/cận nghèo đô thị).
/// Người dân phải nộp đủ cả 2 loại.
/// </summary>
public static class DocumentTypeConstants
{
    /// <summary>
    /// Giấy xác nhận nhà ở (Đ29): chưa có nhà (NO_HOUSE) hoặc có nhà nhưng &lt; 15 m²/người (SMALL_HOUSE).
    /// </summary>
    public const string HousingConditionProof = "HOUSING_CONDITION_PROOF";

    /// <summary>
    /// Giấy chứng nhận hộ nghèo / hộ cận nghèo (do địa phương cấp).
    /// </summary>
    public const string PovertyHouseholdCertificate = "POVERTY_HOUSEHOLD_CERTIFICATE";

    /// <summary>Hai loại giấy tờ Applicant được phép upload — và bắt buộc đủ cả hai khi nộp.</summary>
    public static readonly IReadOnlyList<string> AllowedApplicantDocumentTypes = new[]
    {
        HousingConditionProof,
        PovertyHouseholdCertificate
    };

    /// <summary>Hai loại bắt buộc khi SUBMITTED.</summary>
    public static readonly IReadOnlyList<string> RequiredForSubmit = AllowedApplicantDocumentTypes;

    public static bool IsAllowedApplicantType(string documentType)
        => AllowedApplicantDocumentTypes.Contains(documentType);
}
