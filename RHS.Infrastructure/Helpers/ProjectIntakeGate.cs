using Microsoft.EntityFrameworkCore;
using RHS.Domain.Constants;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Helpers;

/// <summary>
/// Một nơi duy nhất quyết định "dự án còn nhận hồ sơ hay không" (Đ38.1 Nghị định 100/2024).
/// Mọi cửa vào hồ sơ — tạo nháp và nộp hồ sơ — đều phải đi qua đây, nếu không thì
/// nháp tạo trước hạn vẫn nộp được sau khi đã đóng đợt hoặc đã bốc thăm.
/// </summary>
public static class ProjectIntakeGate
{
    /// <summary>Trạng thái vòng đời dự án cho phép tiếp nhận hồ sơ.</summary>
    public const string OpenStatusCode = "OPEN";

    /// <summary>
    /// Trạng thái vòng đời chặn tiếp nhận hồ sơ. Không dùng danh sách cho phép vì
    /// UPCOMING có thể đã tới ApplicationOpenDate mà worker vòng đời chưa kịp chuyển sang OPEN —
    /// khi đó khung thời gian đã công bố mới là căn cứ, không phải cột trạng thái.
    /// </summary>
    private static readonly string[] IntakeBlockedStatusCodes =
    {
        "PENDING", "REJECTED", "CLOSED", "FULL"
    };

    /// <summary>
    /// Nạp dự án kèm trạng thái vòng đời để kiểm tra cổng nhận hồ sơ.
    /// </summary>
    public static Task<HousingProject?> LoadProjectForIntakeAsync(
        AppDbContext db,
        Guid projectId,
        CancellationToken ct = default) =>
        db.HousingProjects
            .AsNoTracking()
            .Include(p => p.HousingProjectStatus)
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted, ct);

    /// <summary>
    /// Chặn nếu dự án không còn nhận hồ sơ. Ném <see cref="InvalidOperationException"/> kèm lý do
    /// cụ thể để hiển thị cho người dân.
    /// </summary>
    /// <param name="project">Dự án đã Include HousingProjectStatus.</param>
    /// <param name="now">Thời điểm xét (UTC).</param>
    /// <param name="action">Hành động đang thực hiện, dùng trong thông báo lỗi.</param>
    public static void EnsureIntakeOpen(HousingProject? project, DateTime now, string action)
    {
        if (project == null)
            throw new KeyNotFoundException("Không tìm thấy dự án.");

        var statusCode = project.HousingProjectStatus?.StatusCode?.Trim().ToUpperInvariant();

        // 1. Đã chốt lịch bốc thăm thì tuyệt đối không nhận thêm hồ sơ:
        //    danh sách tham gia bốc thăm phải cố định trước phiên, căn trả lại dùng danh sách dự bị.
        if (project.LotteryDate.HasValue || project.IsLotteryApproved == true)
        {
            throw new InvalidOperationException(
                $"Dự án đã chốt danh sách và lên lịch bốc thăm (dự kiến {project.LotteryDate:dd/MM/yyyy HH:mm}) " +
                $"nên không thể {action}. Căn hộ bị trả lại sẽ chuyển cho người đứng đầu Danh sách dự bị, " +
                "không mở thêm đợt nhận hồ sơ.");
        }

        if (project.LotterySessionStatus is not null)
        {
            throw new InvalidOperationException(
                $"Phiên bốc thăm của dự án đã được khởi tạo nên không thể {action}.");
        }

        // 2. Trạng thái vòng đời đã đóng cửa tiếp nhận.
        if (statusCode is not null && IntakeBlockedStatusCodes.Contains(statusCode))
        {
            var label = project.HousingProjectStatus?.StatusName ?? statusCode;
            throw new InvalidOperationException(
                $"Dự án đang ở trạng thái '{label}' nên không thể {action}. " +
                "Chỉ nhận hồ sơ trong thời gian dự án mở tiếp nhận.");
        }

        // 3. Khung thời gian tiếp nhận đã công bố.
        if (project.ApplicationOpenDate.HasValue && now < project.ApplicationOpenDate.Value)
        {
            throw new InvalidOperationException(
                $"Dự án chưa đến thời gian mở tiếp nhận hồ sơ " +
                $"(bắt đầu {project.ApplicationOpenDate.Value:dd/MM/yyyy HH:mm}) nên không thể {action}.");
        }

        if (project.ApplicationCloseDate.HasValue && now > project.ApplicationCloseDate.Value)
        {
            throw new InvalidOperationException(
                $"Dự án đã hết thời hạn tiếp nhận hồ sơ " +
                $"(hạn chót {project.ApplicationCloseDate.Value:dd/MM/yyyy HH:mm}) nên không thể {action}.");
        }
    }

    /// <summary>Nạp dự án và kiểm tra cổng nhận hồ sơ trong một lượt.</summary>
    public static async Task<HousingProject> RequireIntakeOpenAsync(
        AppDbContext db,
        Guid projectId,
        string action,
        DateTime? now = null,
        CancellationToken ct = default)
    {
        var project = await LoadProjectForIntakeAsync(db, projectId, ct);
        EnsureIntakeOpen(project, now ?? DateTime.UtcNow, action);
        return project!;
    }
}
