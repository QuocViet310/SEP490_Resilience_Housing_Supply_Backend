using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RHS.Domain.Constants;

namespace RHS.API.Controllers;

/// <summary>
/// API tra cứu danh mục dữ liệu cho FE (document types, priority groups, v.v.).
/// Không yêu cầu đăng nhập.
/// </summary>
[ApiController]
[Route("api/lookup")]
public class LookupController : ControllerBase
{
    // ──────────────────────────────────────────────────────────────
    // GET /api/lookup/document-types
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách tất cả loại giấy tờ mà Applicant được phép upload.
    /// FE dùng để render dropdown / checkbox khi tạo hồ sơ.
    /// </summary>
    [HttpGet("document-types")]
    public IActionResult GetDocumentTypes()
    {
        var items = DocumentTypeConstants.AllowedApplicantDocumentTypes
            .Select(code => new
            {
                code,
                label = DocumentTypeConstants.GetLabel(code)
            })
            .ToList();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/lookup/document-types/required?priorityGroup=...
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách giấy tờ BẮT BUỘC cho nhóm đối tượng cụ thể.
    /// FE dùng để validate trước khi submit và hiển thị tick đã upload.
    /// </summary>
    [HttpGet("document-types/required")]
    public IActionResult GetRequiredDocumentTypes([FromQuery] string? priorityGroup)
    {
        var requiredCodes = DocumentTypeConstants.GetRequiredTypesForSubmit(priorityGroup);

        var items = requiredCodes
            .Select(code => new
            {
                code,
                label = DocumentTypeConstants.GetLabel(code)
            })
            .ToList();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/lookup/priority-groups
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách tất cả nhóm đối tượng thụ hưởng NOXH.
    /// FE dùng để render dropdown khi tạo hồ sơ.
    /// </summary>
    [HttpGet("priority-groups")]
    public IActionResult GetPriorityGroups()
    {
        var items = PriorityGroupConstants.AllValues
            .Select(code => new
            {
                code,
                label = PriorityGroupConstants.Labels.TryGetValue(code, out var l) ? l : code,
                requiresIncomeCertificate = PriorityGroupConstants.RequiresIncomeCheck(code),
                isPovertyGroup = PriorityGroupConstants.IsPovertyGroup(code),
                requiredDocumentType = DocumentTypeConstants.RequiredSubjectProofByGroup
                    .TryGetValue(code, out var dt) ? dt : null,
                requiredDocumentLabel = DocumentTypeConstants.RequiredSubjectProofByGroup
                    .TryGetValue(code, out var dt2) ? DocumentTypeConstants.GetLabel(dt2) : null
            })
            .ToList();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/lookup/marital-statuses
    // ──────────────────────────────────────────────────────────────

    [HttpGet("marital-statuses")]
    public IActionResult GetMaritalStatuses()
    {
        var items = MaritalStatusConstants.AllValues
            .Select(code => new
            {
                code,
                label = MaritalStatusConstants.GetLabel(code)
            })
            .ToList();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/lookup/housing-statuses
    // ──────────────────────────────────────────────────────────────

    [HttpGet("housing-statuses")]
    public IActionResult GetHousingStatuses()
    {
        var items = HousingStatusConstants.AllValues
            .Select(code => new
            {
                code,
                label = HousingStatusConstants.GetLabel(code)
            })
            .ToList();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/lookup/household-relationships
    // ──────────────────────────────────────────────────────────────

    [HttpGet("household-relationships")]
    public IActionResult GetHouseholdRelationships()
    {
        var items = HouseholdRelationshipConstants.AllValues
            .Select(code => new
            {
                code,
                label = HouseholdRelationshipConstants.GetLabel(code)
            })
            .ToList();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/lookup/dependent-reasons
    // ──────────────────────────────────────────────────────────────

    [HttpGet("dependent-reasons")]
    public IActionResult GetDependentReasons()
    {
        var items = DependentReasonConstants.AllValues
            .Select(code => new
            {
                code,
                label = DependentReasonConstants.GetLabel(code)
            })
            .ToList();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/lookup/profile-document-types
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Loại giấy tờ được phép lưu trong Kho hồ sơ cá nhân (Document Vault).
    /// </summary>
    [HttpGet("profile-document-types")]
    public IActionResult GetProfileDocumentTypes()
    {
        var items = DocumentTypeConstants.AllowedProfileDocumentTypes
            .Select(code => new
            {
                code,
                label = DocumentTypeConstants.GetLabel(code)
            })
            .ToList();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/lookup/profile-document-types/required
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Giấy tờ bắt buộc trong Kho hồ sơ theo hôn nhân + thực trạng nhà ở + có người phụ thuộc.
    /// </summary>
    [HttpGet("profile-document-types/required")]
    public IActionResult GetRequiredProfileDocumentTypes(
        [FromQuery] string? maritalStatus,
        [FromQuery] string? housingStatus,
        [FromQuery] bool hasDependentMembers = false)
    {
        var requiredCodes = DocumentTypeConstants.GetRequiredTypesForCitizenProfile(
            maritalStatus, housingStatus, hasDependentMembers);

        var items = requiredCodes
            .Select(code => new
            {
                code,
                label = DocumentTypeConstants.GetLabel(code)
            })
            .ToList();

        return Ok(items);
    }

    // ──────────────────────────────────────────────────────────────
    // GET /api/lookup/apartment-types
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách tất cả loại căn hộ trong CSDL (1 phòng ngủ, 2 phòng ngủ...).
    /// FE dùng để render dropdown chọn loại căn hộ cho CĐT khi tạo/sửa dự án.
    /// </summary>
    [HttpGet("apartment-types")]
    public async Task<IActionResult> GetApartmentTypes(
        [FromServices] RHS.Infrastructure.Data.AppDbContext dbContext)
    {
        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            dbContext.ApartmentTypes
                .AsNoTracking()
                .OrderBy(t => t.TypeCode)
                .Select(t => new
                {
                    id = t.Id,
                    typeCode = t.TypeCode,
                    typeName = t.TypeName,
                    description = t.Description
                }));

        return Ok(items);
    }
}
