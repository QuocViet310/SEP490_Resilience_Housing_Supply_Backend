using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RHS.Application.Interfaces;
using RHS.Domain.Constants;

namespace RHS.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub: sảnh chờ, bốc live, phát sóng kết quả + trạng thái phiên.
/// Theo dõi SXD online để giám sát phiên (Đ36.2.b NĐ 100/2024).
/// </summary>
[Authorize]
public class LotteryHub : Hub<ILotteryHubClient>
{
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> ProjectLobbies = new();
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Guid>> ProjectSxdConnections = new();
    private static readonly ConcurrentDictionary<string, ConcurrentBag<Guid>> ConnectionProjects = new();

    private readonly ILotteryService _lotteryService;

    public LotteryHub(ILotteryService lotteryService)
    {
        _lotteryService = lotteryService;
    }

    /// <summary>
    /// Tham gia sảnh. Applicant bắt buộc OTP (joinCode). Staff (CĐT/SXD/Admin) không cần.
    /// </summary>
    public async Task JoinProjectLobby(Guid projectId, string? joinCode = null)
    {
        var userId = GetUserId();
        var isStaff = IsStaff();
        var isSxd = IsSxd();

        var verify = await _lotteryService.VerifyJoinCodeAsync(projectId, userId, joinCode, isStaff);
        if (!verify.Success)
            throw new HubException(verify.Message);

        var groupName = GetGroupName(projectId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var lobby = ProjectLobbies.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, byte>());
        lobby.TryAdd(Context.ConnectionId, 0);

        if (isSxd)
        {
            var sxdLobby = ProjectSxdConnections.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, Guid>());
            sxdLobby[Context.ConnectionId] = userId;
            await _lotteryService.RecordSupervisorAsync(projectId, userId);
        }

        var userProjects = ConnectionProjects.GetOrAdd(Context.ConnectionId, _ => new ConcurrentBag<Guid>());
        userProjects.Add(projectId);

        await Clients.Group(groupName).ReceiveLobbyCount(lobby.Count);
        await Clients.Group(groupName).ReceiveSxdSupervisorCount(GetSxdOnlineCount(projectId));
        if (!string.IsNullOrWhiteSpace(verify.SessionStatus))
            await Clients.Caller.ReceiveLotteryStatus(verify.SessionStatus);
    }

    public async Task LeaveProjectLobby(Guid projectId)
    {
        var groupName = GetGroupName(projectId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        if (ProjectLobbies.TryGetValue(projectId, out var lobby))
        {
            lobby.TryRemove(Context.ConnectionId, out _);
            await Clients.Group(groupName).ReceiveLobbyCount(lobby.Count);
        }

        if (ProjectSxdConnections.TryGetValue(projectId, out var sxdLobby))
        {
            sxdLobby.TryRemove(Context.ConnectionId, out _);
            await Clients.Group(groupName).ReceiveSxdSupervisorCount(GetSxdOnlineCount(projectId));
        }
    }

    public async Task DrawUnit(Guid projectId)
    {
        var userId = GetUserId();
        try
        {
            // Service đã broadcast ReceiveDrawResult
            await _lotteryService.DrawUnitRealtimeAsync(projectId, userId);
        }
        catch (Exception ex)
        {
            throw new HubException(ex.Message);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionProjects.TryRemove(Context.ConnectionId, out var projects))
        {
            foreach (var projectId in projects.Distinct())
            {
                if (ProjectLobbies.TryGetValue(projectId, out var lobby))
                {
                    lobby.TryRemove(Context.ConnectionId, out _);
                    await Clients.Group(GetGroupName(projectId)).ReceiveLobbyCount(lobby.Count);
                }

                if (ProjectSxdConnections.TryGetValue(projectId, out var sxdLobby))
                {
                    sxdLobby.TryRemove(Context.ConnectionId, out _);
                    await Clients.Group(GetGroupName(projectId))
                        .ReceiveSxdSupervisorCount(GetSxdOnlineCount(projectId));
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public static string GetGroupName(Guid projectId) => $"project_lottery_{projectId}";

    /// <summary>Số connection Sở Xây dựng đang online trong sảnh dự án.</summary>
    public static int GetSxdOnlineCount(Guid projectId) =>
        ProjectSxdConnections.TryGetValue(projectId, out var sxd)
            ? sxd.Count
            : 0;

    private Guid GetUserId()
    {
        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            throw new HubException("Không thể xác thực người dùng.");
        return userId;
    }

    private HashSet<string> GetRoles() =>
        Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase)
        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private bool IsStaff()
    {
        var roles = GetRoles();
        return roles.Contains(RoleConstants.HousingDeveloper)
               || roles.Contains(RoleConstants.DepartmentOfConstruction)
               || roles.Contains(RoleConstants.SystemAdministrator);
    }

    private bool IsSxd()
    {
        var roles = GetRoles();
        return roles.Contains(RoleConstants.DepartmentOfConstruction)
               || roles.Contains(RoleConstants.SystemAdministrator);
    }
}
