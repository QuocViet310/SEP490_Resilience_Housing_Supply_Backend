namespace RHS.Domain.Constants;

/// <summary>Trạng thái phiên bốc thăm live (FSM).</summary>
public static class LotterySessionStatusConstants
{
    public const string Scheduled = "Scheduled";
    public const string WaitingLobby = "WaitingLobby";
    public const string Live = "Live";
    public const string Finished = "Finished";
    public const string Published = "Published";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Scheduled, WaitingLobby, Live, Finished, Published
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status);

    public static bool CanJoinLobby(string? status) =>
        status is WaitingLobby or Live or Scheduled;

    public static bool CanDraw(string? status) => status == Live;
}
