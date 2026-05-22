using RHS.Domain.Entities;

namespace RHS.Application.Interfaces;

public interface IOtpRepository
{
    Task<OtpVerification?> GetValidOtpAsync(Guid userId, string code);
    Task<OtpVerification> CreateAsync(OtpVerification otpVerification);
    Task UpdateAsync(OtpVerification otpVerification);
    Task InvalidateAllUserOtpsAsync(Guid userId);
}
