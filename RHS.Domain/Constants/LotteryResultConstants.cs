namespace RHS.Domain.Constants;

public static class LotteryResultConstants
{
    public const string Pending = "PENDING";
    public const string Won = "WON";
    public const string Lost = "LOST";
    public const string PriorityWon = "PRIORITY_WON";

    public static readonly IReadOnlyList<string> AllValues = new[]
    {
        Pending, Won, Lost, PriorityWon
    };
}
