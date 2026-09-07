using Microsoft.EntityFrameworkCore;
using RHS.Application.DTOs.Payment;
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
            .Include(p => p.HousingApplication)
            .FirstOrDefaultAsync(p => p.OrderId == orderId);
    }

    /// <inheritdoc/>
    public async Task<Payment?> GetByIdAsync(Guid id)
    {
        return await _context.Payments
            .Include(p => p.User)
            .Include(p => p.HousingProject)
            .Include(p => p.HousingApplication)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Payments
            .Include(p => p.User)
            .Include(p => p.HousingProject)
            .Include(p => p.HousingApplication)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<Payment> Items, int TotalCount)> GetAdminPaymentsPagedAsync(AdminTransactionQueryDto queryDto)
    {
        var query = _context.Payments
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.HousingProject)
            .Include(p => p.HousingApplication)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryDto.Status))
        {
            var status = queryDto.Status.Trim();
            query = query.Where(p => p.Status == status);
        }

        if (queryDto.ProjectId.HasValue)
        {
            query = query.Where(p => p.HousingProjectId == queryDto.ProjectId.Value);
        }

        if (queryDto.UserId.HasValue)
        {
            query = query.Where(p => p.UserId == queryDto.UserId.Value);
        }

        if (queryDto.FromDate.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= queryDto.FromDate.Value);
        }

        if (queryDto.ToDate.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= queryDto.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(queryDto.SearchKeyword))
        {
            var kw = queryDto.SearchKeyword.Trim().ToLower();
            query = query.Where(p =>
                p.OrderId.ToLower().Contains(kw) ||
                (p.VnpTransactionNo != null && p.VnpTransactionNo.ToLower().Contains(kw)) ||
                (p.User.FullName != null && p.User.FullName.ToLower().Contains(kw)) ||
                (p.User.Email != null && p.User.Email.ToLower().Contains(kw)) ||
                (p.User.PhoneNumber != null && p.User.PhoneNumber.ToLower().Contains(kw)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((queryDto.Page - 1) * queryDto.PageSize)
            .Take(queryDto.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public async Task<(IEnumerable<Payment> Items, int TotalCount)> GetUserPaymentsPagedAsync(Guid userId, UserTransactionQueryDto queryDto)
    {
        var query = _context.Payments
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.HousingProject)
            .Include(p => p.HousingApplication)
            .Where(p => p.UserId == userId);

        if (!string.IsNullOrWhiteSpace(queryDto.Status))
        {
            var status = queryDto.Status.Trim();
            query = query.Where(p => p.Status == status);
        }

        if (queryDto.ProjectId.HasValue)
        {
            query = query.Where(p => p.HousingProjectId == queryDto.ProjectId.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((queryDto.Page - 1) * queryDto.PageSize)
            .Take(queryDto.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
