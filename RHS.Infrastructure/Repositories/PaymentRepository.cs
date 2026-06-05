using Microsoft.EntityFrameworkCore;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Repositories;

/// <summary>
/// Repository thao tác CRUD với bảng Payments.
/// Theo đúng pattern của UserRepository, OtpRepository trong project.
/// </summary>
public class PaymentRepository : IPaymentRepository
{
    private readonly AppDbContext _context;

    public PaymentRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task CreateAsync(Payment payment)
    {
        await _context.Payments.AddAsync(payment);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Payment payment)
    {
        _context.Payments.Update(payment);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<Payment?> GetByOrderIdAsync(string orderId)
    {
        return await _context.Payments
            .Include(p => p.User)
            .Include(p => p.HousingProject)
            .FirstOrDefaultAsync(p => p.OrderId == orderId);
    }

    /// <inheritdoc/>
    public async Task<Payment?> GetByIdAsync(Guid id)
    {
        return await _context.Payments
            .Include(p => p.User)
            .Include(p => p.HousingProject)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Payments
            .Include(p => p.HousingProject)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }
}
