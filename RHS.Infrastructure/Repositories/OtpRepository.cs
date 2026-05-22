using Microsoft.EntityFrameworkCore;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Repositories;

public class OtpRepository : IOtpRepository
{
    private readonly AppDbContext _context;

    public OtpRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<OtpVerification?> GetValidOtpAsync(Guid userId, string code)
    {
        return await _context.OtpVerifications
            .FirstOrDefaultAsync(otp => 
                otp.UserId == userId && 
                otp.OtpCode == code && 
                !otp.Verified && 
                otp.ExpiredAt > DateTime.UtcNow);
    }

    public async Task<OtpVerification> CreateAsync(OtpVerification otpVerification)
    {
        _context.OtpVerifications.Add(otpVerification);
        await _context.SaveChangesAsync();
        return otpVerification;
    }

    public async Task UpdateAsync(OtpVerification otpVerification)
    {
        _context.OtpVerifications.Update(otpVerification);
        await _context.SaveChangesAsync();
    }

    public async Task InvalidateAllUserOtpsAsync(Guid userId)
    {
        var otps = await _context.OtpVerifications
            .Where(otp => otp.UserId == userId && !otp.Verified)
            .ToListAsync();

        foreach (var otp in otps)
        {
            otp.Verified = true;
        }

        await _context.SaveChangesAsync();
    }
}
