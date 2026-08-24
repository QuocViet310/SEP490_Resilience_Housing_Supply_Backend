namespace RHS.Domain.Constants;

/// <summary>
/// Loại giấy tờ trong hồ sơ đăng ký mua nhà ở xã hội.
/// Theo Điều 77 Luật Nhà ở 2023, Nghị định 100/2024/NĐ-CP (Đ29, Đ30).
/// 
/// Hồ sơ gồm 3 nhóm:
///   (A) Giấy xác nhận điều kiện nhà ở — bắt buộc tất cả
///   (B) Giấy tờ chứng minh đối tượng — bắt buộc 1 loại phù hợp nhóm đối tượng
///   (C) Giấy xác nhận thu nhập — bắt buộc cho một số nhóm (không bắt buộc hộ nghèo)
/// </summary>
public static class DocumentTypeConstants
{
    // ════════════════════════════════════════════════════════════════
    // (A) Giấy tờ bắt buộc cho TẤT CẢ đối tượng
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Giấy xác nhận điều kiện nhà ở (Đ29):
    ///   - Chưa có nhà (NO_HOUSE): xác nhận không có tên trong GCN QSDĐ
    ///   - Có nhà nhưng diện tích &lt; 15 m²/người (SMALL_HOUSE): xác nhận diện tích bình quân
    /// </summary>
    public const string HousingConditionProof = "HOUSING_CONDITION_PROOF";

    // ════════════════════════════════════════════════════════════════
    // (B) Giấy tờ chứng minh đối tượng (chọn 1 theo nhóm)
    // ════════════════════════════════════════════════════════════════

    /// <summary>Giấy chứng nhận hộ nghèo / hộ cận nghèo (do địa phương cấp) — Nhóm 2-4 Đ76.</summary>
    public const string PovertyHouseholdCertificate = "POVERTY_HOUSEHOLD_CERTIFICATE";

    /// <summary>Giấy xác nhận người có công với cách mạng / thân nhân liệt sĩ — Nhóm 1 Đ76.</summary>
    public const string MeritPersonCertificate = "MERIT_PERSON_CERTIFICATE";

    /// <summary>Giấy xác nhận thu nhập thấp tại đô thị (cơ quan thuế / UBND cấp) — Nhóm 5 Đ76.</summary>
    public const string LowIncomeCertificate = "LOW_INCOME_CERTIFICATE";

    /// <summary>Giấy xác nhận đang làm việc tại DN/HTX/Liên hiệp HTX/KCN — Nhóm 6 Đ76.</summary>
    public const string EmploymentCertificate = "EMPLOYMENT_CERTIFICATE";

    /// <summary>Giấy xác nhận đang phục vụ tại ngũ (lực lượng vũ trang / cơ yếu) — Nhóm 7 Đ76.</summary>
    public const string MilitaryServiceCertificate = "MILITARY_SERVICE_CERTIFICATE";

    /// <summary>Giấy xác nhận cán bộ / công chức / viên chức (cơ quan công tác cấp) — Nhóm 8 Đ76.</summary>
    public const string CivilServantCertificate = "CIVIL_SERVANT_CERTIFICATE";

    /// <summary>Quyết định / văn bản trả lại nhà ở công vụ — Nhóm 9 Đ76.</summary>
    public const string PublicHousingReturnCertificate = "PUBLIC_HOUSING_RETURN_CERTIFICATE";

    /// <summary>Quyết định thu hồi đất / giải tỏa nhà ở (chưa được bồi thường nhà, đất) — Nhóm 10 Đ76.</summary>
    public const string LandRecoveryDecision = "LAND_RECOVERY_DECISION";

    // ════════════════════════════════════════════════════════════════
    // (C) Giấy tờ bổ sung — thu nhập
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Giấy xác nhận thu nhập (do cơ quan thuế hoặc nơi làm việc cấp) — Đ30.
    /// Bắt buộc cho nhóm: thu nhập thấp, công nhân, quân nhân, cán bộ, trả nhà công vụ, bị thu hồi đất.
    /// KHÔNG bắt buộc cho nhóm hộ nghèo/cận nghèo (dùng chuẩn nghèo thay trần thu nhập).
    /// KHÔNG bắt buộc cho nhóm người có công với cách mạng.
    /// </summary>
    public const string IncomeCertificate = "INCOME_CERTIFICATE";

    // ════════════════════════════════════════════════════════════════
    // (D) Giấy tờ nhân thân & Hồ sơ cá nhân tái sử dụng (Profile Document Vault)
    // ════════════════════════════════════════════════════════════════

    /// <summary>Căn cước công dân - Mặt trước</summary>
    public const string CitizenIdFront = "CITIZEN_ID_FRONT";

    /// <summary>Căn cước công dân - Mặt sau</summary>
    public const string CitizenIdBack = "CITIZEN_ID_BACK";

    /// <summary>Giấy chứng nhận kết hôn</summary>
    public const string MarriageCertificate = "MARRIAGE_CERTIFICATE";

    /// <summary>Giấy xác nhận tình trạng độc thân</summary>
    public const string SingleStatusCertificate = "SINGLE_STATUS_CERTIFICATE";

    /// <summary>Quyết định / Bản án ly hôn của Tòa án</summary>
    public const string DivorceCertificate = "DIVORCE_CERTIFICATE";

    /// <summary>Giấy tờ chứng minh người phụ thuộc (Giấy khai sinh con, xác nhận sinh viên, giám định khuyết tật...)</summary>
    public const string DependentProof = "DEPENDENT_PROOF";

    /// <summary>Giấy xác nhận thông tin cư trú (CT07 / CT08 / Sổ hộ khẩu)</summary>
    public const string ResidenceConfirmation = "RESIDENCE_CONFIRMATION";

    // ════════════════════════════════════════════════════════════════
    // Collections & Mappings
    // ════════════════════════════════════════════════════════════════

    /// <summary>Tất cả giấy tờ chứng minh đối tượng (B).</summary>
    public static readonly IReadOnlyList<string> SubjectProofTypes = new[]
    {
        PovertyHouseholdCertificate,
        MeritPersonCertificate,
        LowIncomeCertificate,
        EmploymentCertificate,
        MilitaryServiceCertificate,
        CivilServantCertificate,
        PublicHousingReturnCertificate,
        LandRecoveryDecision
    };

    /// <summary>Tất cả loại giấy tờ mà Applicant được phép upload vào đơn đăng ký.</summary>
    public static readonly IReadOnlyList<string> AllowedApplicantDocumentTypes = new[]
    {
        // (A) Bắt buộc tất cả
        HousingConditionProof,
        // (B) Chứng minh đối tượng
        PovertyHouseholdCertificate,
        MeritPersonCertificate,
        LowIncomeCertificate,
        EmploymentCertificate,
        MilitaryServiceCertificate,
        CivilServantCertificate,
        PublicHousingReturnCertificate,
        LandRecoveryDecision,
        // (C) Thu nhập
        IncomeCertificate,
        // (D) Hồ sơ nhân thân đi kèm
        MarriageCertificate,
        SingleStatusCertificate,
        DivorceCertificate,
        DependentProof,
        ResidenceConfirmation
    };

    /// <summary>Tất cả loại giấy tờ được phép lưu trong Kho tài liệu cá nhân tái sử dụng (Profile Document Vault).</summary>
    public static readonly IReadOnlyList<string> AllowedProfileDocumentTypes = new[]
    {
        CitizenIdFront,
        CitizenIdBack,
        HousingConditionProof,
        PovertyHouseholdCertificate,
        MeritPersonCertificate,
        LowIncomeCertificate,
        EmploymentCertificate,
        MilitaryServiceCertificate,
        CivilServantCertificate,
        PublicHousingReturnCertificate,
        LandRecoveryDecision,
        IncomeCertificate,
        MarriageCertificate,
        SingleStatusCertificate,
        DivorceCertificate,
        DependentProof,
        ResidenceConfirmation
    };

    /// <summary>
    /// Mapping: nhóm đối tượng → giấy tờ chứng minh đối tượng bắt buộc (B).
    /// Mỗi nhóm chỉ yêu cầu 1 loại giấy tờ chứng minh.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RequiredSubjectProofByGroup =
        new Dictionary<string, string>
        {
            [PriorityGroupConstants.MeritPerson]          = MeritPersonCertificate,
            [PriorityGroupConstants.RuralPoor]             = PovertyHouseholdCertificate,
            [PriorityGroupConstants.RuralNearPoor]         = PovertyHouseholdCertificate,
            [PriorityGroupConstants.UrbanPoor]             = PovertyHouseholdCertificate,
            [PriorityGroupConstants.UrbanNearPoor]         = PovertyHouseholdCertificate,
            [PriorityGroupConstants.LowIncomeUrban]        = LowIncomeCertificate,
            [PriorityGroupConstants.Worker]                = EmploymentCertificate,
            [PriorityGroupConstants.MilitaryPersonnel]     = MilitaryServiceCertificate,
            [PriorityGroupConstants.CivilServant]          = CivilServantCertificate,
            [PriorityGroupConstants.PublicHousingReturn]    = PublicHousingReturnCertificate,
            [PriorityGroupConstants.LandRecoveryAffected]  = LandRecoveryDecision
        };

    /// <summary>
    /// Nhóm đối tượng cần thêm giấy xác nhận thu nhập (C).
    /// Hộ nghèo/cận nghèo và người có công KHÔNG cần.
    /// </summary>
    public static readonly IReadOnlyList<string> GroupsRequiringIncomeCertificate = new[]
    {
        PriorityGroupConstants.LowIncomeUrban,
        PriorityGroupConstants.Worker,
        PriorityGroupConstants.MilitaryPersonnel,
        PriorityGroupConstants.CivilServant,
        PriorityGroupConstants.PublicHousingReturn,
        PriorityGroupConstants.LandRecoveryAffected
    };

    /// <summary>Label tiếng Việt cho FE hiển thị.</summary>
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [HousingConditionProof]          = "Giấy xác nhận điều kiện nhà ở",
        [PovertyHouseholdCertificate]    = "Giấy chứng nhận hộ nghèo/cận nghèo",
        [MeritPersonCertificate]         = "Giấy xác nhận người có công với cách mạng",
        [LowIncomeCertificate]           = "Giấy xác nhận thu nhập thấp tại đô thị",
        [EmploymentCertificate]          = "Giấy xác nhận đang làm việc tại DN/HTX/KCN",
        [MilitaryServiceCertificate]     = "Giấy xác nhận phục vụ lực lượng vũ trang/cơ yếu",
        [CivilServantCertificate]        = "Giấy xác nhận cán bộ/công chức/viên chức",
        [PublicHousingReturnCertificate] = "Văn bản trả lại nhà ở công vụ",
        [LandRecoveryDecision]           = "Quyết định thu hồi đất/giải tỏa nhà ở",
        [IncomeCertificate]              = "Giấy xác nhận thu nhập",
        [CitizenIdFront]                 = "CCCD - Mặt trước",
        [CitizenIdBack]                  = "CCCD - Mặt sau",
        [MarriageCertificate]            = "Giấy chứng nhận kết hôn",
        [SingleStatusCertificate]        = "Giấy xác nhận tình trạng độc thân",
        [DivorceCertificate]             = "Quyết định ly hôn của Tòa án",
        [DependentProof]                 = "Giấy tờ chứng minh người phụ thuộc",
        [ResidenceConfirmation]          = "Giấy xác nhận thông tin cư trú"
    };

    /// <summary>Kiểm tra loại giấy tờ có được phép upload hay không.</summary>
    public static bool IsAllowedApplicantType(string documentType)
        => AllowedApplicantDocumentTypes.Contains(documentType);

    /// <summary>Kiểm tra loại giấy tờ có được phép upload vào Kho hồ sơ cá nhân hay không.</summary>
    public static bool IsAllowedProfileDocumentType(string documentType)
        => AllowedProfileDocumentTypes.Contains(documentType);

    /// <summary>
    /// Trả về danh sách giấy tờ bắt buộc khi submit,
    /// dựa trên nhóm đối tượng đã khai trong hồ sơ.
    /// </summary>
    public static List<string> GetRequiredTypesForSubmit(string? priorityGroup)
    {
        var required = new List<string> { HousingConditionProof };

        // (B) Giấy tờ chứng minh đối tượng
        if (!string.IsNullOrWhiteSpace(priorityGroup)
            && RequiredSubjectProofByGroup.TryGetValue(priorityGroup, out var subjectProof))
        {
            required.Add(subjectProof);
        }

        // (C) Giấy xác nhận thu nhập (nếu nhóm đối tượng yêu cầu)
        if (!string.IsNullOrWhiteSpace(priorityGroup)
            && GroupsRequiringIncomeCertificate.Contains(priorityGroup))
        {
            required.Add(IncomeCertificate);
        }

        return required;
    }

    /// <summary>
    /// Giấy tờ bắt buộc trong Kho hồ sơ cá nhân theo hôn nhân + thực trạng nhà ở.
    /// Không thay thế giấy tờ theo nhóm đối tượng khi nộp đơn dự án.
    /// </summary>
    public static List<string> GetRequiredTypesForCitizenProfile(
        string? maritalStatus,
        string? housingStatus,
        bool hasDependentMembers = false)
    {
        var required = new List<string>();

        var marital = maritalStatus?.Trim().ToUpperInvariant();
        if (marital == MaritalStatusConstants.Married)
            required.Add(MarriageCertificate);
        else if (marital == MaritalStatusConstants.Single)
            required.Add(SingleStatusCertificate);
        else if (marital == MaritalStatusConstants.Divorced)
            required.Add(DivorceCertificate);

        if (!string.IsNullOrWhiteSpace(housingStatus)
            && HousingStatusConstants.IsValid(housingStatus))
        {
            required.Add(HousingConditionProof);
        }

        if (hasDependentMembers)
            required.Add(DependentProof);

        return required.Distinct().ToList();
    }

    /// <summary>
    /// Lấy label tiếng Việt cho loại giấy tờ.
    /// Trả về chính documentType nếu không tìm thấy label.
    /// </summary>
    public static string GetLabel(string documentType)
        => Labels.TryGetValue(documentType, out var label) ? label : documentType;
}
