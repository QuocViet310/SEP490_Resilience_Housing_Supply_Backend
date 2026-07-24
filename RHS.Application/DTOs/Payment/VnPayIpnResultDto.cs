namespace RHS.Application.DTOs.Payment;

/// <summary>Phản hồi chuẩn VNPay IPN (server-to-server).</summary>
public class VnPayIpnResultDto
{
    public string RspCode { get; set; } = "99";
    public string Message { get; set; } = "Unknown error";
}
