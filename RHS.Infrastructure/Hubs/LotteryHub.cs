using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RHS.Application.Interfaces;

namespace RHS.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub quản lý sảnh chờ bốc thăm thời gian thực (Real-time Waiting Lobby),
/// xử lý bốc thăm tương tác và phát sóng kết quả trực tiếp cho SXD/CĐT (Mục 20, 21, 22).
/// </summary>
[Authorize]
public class LotteryHub : Hub<ILotteryHubClient>
{
    // Mapping projectId -> Map(connectionId -> byte) để đếm số kết nối online thời gian thực ở sảnh
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> ProjectLobbies = new();
    
    // Mapping connectionId -> list of projectIds mà client đã join
    private static readonly ConcurrentDictionary<string, ConcurrentBag<Guid>> ConnectionProjects = new();

    private readonly ILotteryService _lotteryService;

    public LotteryHub(ILotteryService lotteryService)
    {
        _lotteryService = lotteryService;
    }

    /// <summary>[Mục 20] Client (App/Web) tham gia sảnh chờ bốc thăm của dự án.</summary>
    public async Task JoinProjectLobby(Guid projectId)
    {
        var groupName = GetGroupName(projectId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var lobby = ProjectLobbies.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, byte>());
        lobby.TryAdd(Context.ConnectionId, 0);

        var userProjects = ConnectionProjects.GetOrAdd(Context.ConnectionId, _ => new ConcurrentBag<Guid>());
        userProjects.Add(projectId);

        int onlineCount = lobby.Count;
        await Clients.Group(groupName).ReceiveLobbyCount(onlineCount);
    }

    /// <summary>[Mục 20] Client rời sảnh chờ bốc thăm.</summary>
    public async Task LeaveProjectLobby(Guid projectId)
    {
        var groupName = GetGroupName(projectId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        if (ProjectLobbies.TryGetValue(projectId, out var lobby))
        {
            lobby.TryRemove(Context.ConnectionId, out _);
            int onlineCount = lobby.Count;
            await Clients.Group(groupName).ReceiveLobbyCount(onlineCount);
        }
    }

    /// <summary>[Mục 21] Người dân bấm nút bốc thăm trực tiếp trên App/Web thời gian thực.</summary>
    public async Task DrawUnit(Guid projectId)
    {
        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            throw new HubException("Không thể xác thực người dùng.");
        }

        try
        {
            // Thực hiện bốc thăm thời gian thực với SemaphoreSlim concurrency lock (Row lock 1 mili-giây)
            var result = await _lotteryService.DrawUnitRealtimeAsync(projectId, userId);

            // Bắn gói tin ReceiveDrawResult(data) tức thì đến sảnh bốc thăm và màn hình giám sát Web SXD (Mục 21 & 22)
            var groupName = GetGroupName(projectId);
            await Clients.Group(groupName).ReceiveDrawResult(result);
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
                    var groupName = GetGroupName(projectId);
                    await Clients.Group(groupName).ReceiveLobbyCount(lobby.Count);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public static string GetGroupName(Guid projectId) => $"project_lottery_{projectId}";
}
