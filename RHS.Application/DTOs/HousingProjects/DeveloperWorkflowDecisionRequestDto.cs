namespace RHS.Application.DTOs.HousingProjects;

/// <summary>
/// Yêu cầu quyết định luồng xử lý từ Chủ Dự Án (CĐT) cho danh sách hồ sơ đăng ký.
/// </summary>
public class DeveloperWorkflowDecisionRequestDto
{
    /// <summary>
    /// Quyết định của CĐT:
    /// - "CLOSE_AND_SIGN": Chốt danh sách đủ điều kiện và chuyển sang ký hợp đồng ngay (Kịch bản N_qualified <= N_available).
    /// - "KEEP_OPEN": Lưu danh sách đủ điều kiện và tiếp tục tiếp nhận thêm hồ sơ.
    /// - "PROCESS_PRIORITY_AND_LOTTERY": Xử lý duyệt người ưu tiên và bốc thăm phần dư (Kịch bản N_qualified > N_available).
    /// </summary>
    public string DecisionType { get; set; } = string.Empty;

    /// <summary>
    /// Danh sách ApplicationId diện ưu tiên được CĐT chọn duyệt trực tiếp 
    /// (Sử dụng khi N_priority > N_available và CĐT chọn thủ công, nếu rỗng hệ thống tự lọc theo PriorityScore).
    /// </summary>
    public List<Guid>? SelectedPriorityApplicationIds { get; set; }

    /// <summary>
    /// Gán căn cụ thể khi chốt / duyệt ưu tiên (bắt buộc với CLOSE_AND_SIGN và PROCESS_PRIORITY_AND_LOTTERY).
    /// Mỗi hồ sơ được chuyển CONTRACT_PENDING phải có đúng 1 căn AVAILABLE trong dự án.
    /// </summary>
    public List<ApartmentAssignmentItemDto>? ApartmentAssignments { get; set; }

    /// <summary>
    /// Tùy chọn đóng dự án (chuyển status CLOSED) sau khi chốt đợt hay không.
    /// </summary>
    public bool CloseProject { get; set; } = false;
}

public class ApartmentAssignmentItemDto
{
    public Guid ApplicationId { get; set; }
    public Guid ApartmentId { get; set; }
}
