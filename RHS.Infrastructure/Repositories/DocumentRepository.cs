using Microsoft.EntityFrameworkCore;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _context;

    public DocumentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationDocument> CreateAsync(ApplicationDocument document)
    {
        _context.ApplicationDocuments.Add(document);
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task<ApplicationDocument?> GetByIdAsync(Guid documentId)
    {
        return await _context.ApplicationDocuments
            .AsNoTracking()
            .Include(d => d.HousingApplication)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);
    }

    public async Task<IReadOnlyList<ApplicationDocument>> GetByApplicationIdAsync(Guid applicationId)
    {
        return await _context.ApplicationDocuments
            .AsNoTracking()
            .Where(d => d.ApplicationId == applicationId)
            .OrderBy(d => d.UploadedAt)
            .ToListAsync();
    }

    public async Task<bool> ExistsByApplicationAndTypeAsync(Guid applicationId, string documentType)
    {
        return await _context.ApplicationDocuments
            .AsNoTracking()
            .AnyAsync(d => d.ApplicationId == applicationId && d.DocumentType == documentType);
    }

    public async Task DeleteAsync(ApplicationDocument document)
    {
        _context.ApplicationDocuments.Remove(document);
        await _context.SaveChangesAsync();
    }
}
