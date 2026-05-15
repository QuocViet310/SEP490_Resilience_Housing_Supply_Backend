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

    public async Task<OtpCode?> GetValidOtpAsync(Guid userId, string code, string purpose)
    {
        return await _context.OtpCodes
            .FirstOrDefaultAsync(otp => 
                otp.UserId == userId && 
                otp.Code == code && 
                otp.Purpose == purpose &&
                !otp.IsUsed && 
                otp.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<OtpCode> CreateAsync(OtpCode otpCode)
    {
        _context.OtpCodes.Add(otpCode);
        await _context.SaveChangesAsync();
        return otpCode;
    }

    public async Task UpdateAsync(OtpCode otpCode)
    {
        _context.OtpCodes.Update(otpCode);
        await _context.SaveChangesAsync();
    }

    public async Task InvalidateAllUserOtpsAsync(Guid userId, string purpose)
    {
        var otps = await _context.OtpCodes
            .Where(otp => otp.UserId == userId && otp.Purpose == purpose && !otp.IsUsed)
            .ToListAsync();

        foreach (var otp in otps)
        {
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}
