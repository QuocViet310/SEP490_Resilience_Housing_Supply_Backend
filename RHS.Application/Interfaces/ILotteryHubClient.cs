using RHS.Application.DTOs.Lottery;

namespace RHS.Application.Interfaces;

/// <summary>
/// Strong-typed interface cho SignalR LotteryHub.
/// </summary>
public interface ILotteryHubClient
{
    /// <summary>Cập nhật sĩ số người dân đang có mặt ở sảnh chờ thời gian thực.</summary>
    Task ReceiveLobbyCount(int onlineCount);

    /// <summary>Gửi gói tin kết quả bốc thăm vừa diễn ra cho màn hình giám sát SXD/CĐT.</summary>
    Task ReceiveDrawResult(LiveDrawResultDto data);

    /// <summary>Cập nhật trạng thái phiên bốc thăm (Bắt đầu, Tạm dừng, Kết thúc).</summary>
    Task ReceiveLotteryStatus(string statusMessage);
}
