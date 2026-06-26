using Microsoft.EntityFrameworkCore;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Repositories;

/// <summary>
/// Repository thao tác CRUD với bảng PrincipleAgreements.
/// Theo đúng pattern của PaymentRepository trong project.
/// </summary>
public class PrincipleAgreementRepository : IPrincipleAgreementRepository
{
    private readonly AppDbContext _context;

    public PrincipleAgreementRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task CreateAsync(PrincipleAgreement agreement)
    {
        await _context.PrincipleAgreements.AddAsync(agreement);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<PrincipleAgreement?> GetByApplicationIdAsync(Guid applicationId)
    {
        return await _context.PrincipleAgreements
            .Include(pa => pa.HousingApplication)
            .FirstOrDefaultAsync(pa => pa.ApplicationId == applicationId);
    }
}
