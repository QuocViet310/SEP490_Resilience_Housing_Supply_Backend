using Microsoft.AspNetCore.Mvc;
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
}
