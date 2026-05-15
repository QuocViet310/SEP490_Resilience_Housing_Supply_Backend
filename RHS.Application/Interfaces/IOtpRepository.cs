using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

public interface IOtpRepository
{
    Task<OtpCode?> GetValidOtpAsync(Guid userId, string code, string purpose);
    Task<OtpCode> CreateAsync(OtpCode otpCode);
    Task UpdateAsync(OtpCode otpCode);
    Task InvalidateAllUserOtpsAsync(Guid userId, string purpose);
}
