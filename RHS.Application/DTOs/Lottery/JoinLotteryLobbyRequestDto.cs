namespace RHS.Application.DTOs.Lottery;

public class JoinLotteryLobbyRequestDto
{
    /// <summary>Mã OTP 6 số nhận từ thông báo / lịch đã duyệt.</summary>
    public string JoinCode { get; set; } = string.Empty;
}

public class VerifyLotteryJoinCodeResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SessionStatus { get; set; }
}
