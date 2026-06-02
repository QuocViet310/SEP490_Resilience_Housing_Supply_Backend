using Microsoft.EntityFrameworkCore;
using RHS.Application.Interfaces;
using RHS.Domain.Entities;
using RHS.Infrastructure.Data;

namespace RHS.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<User?> GetByGoogleIdAsync(string googleId)
    {
        return await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.GoogleId == googleId);
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task UpdateAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }

    public async Task<List<User>> GetByRoleAsync(string roleName)
    {
        return await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Role.RoleName == roleName)
            .ToListAsync();
    }

    public async Task<List<User>> GetStaffListAsync(int pageNumber, int pageSize, string? role = null, string? status = null, string? searchTerm = null)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .AsQueryable();

        // Filter by role
        if (!string.IsNullOrEmpty(role))
        {
            query = query.Where(u => u.Role.RoleName == role);
        }

        // Filter by status
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(u => u.Status == status);
        }

        // Search by email or full name
        if (!string.IsNullOrEmpty(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(u => u.Email.ToLower().Contains(searchTerm) || u.FullName.ToLower().Contains(searchTerm));
        }

        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetStaffCountAsync(string? role = null, string? status = null, string? searchTerm = null)
    {
        var query = _context.Users.AsQueryable();

        // Filter by role
        if (!string.IsNullOrEmpty(role))
        {
            query = query.Where(u => u.Role.RoleName == role);
        }

        // Filter by status
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(u => u.Status == status);
        }

        // Search by email or full name
        if (!string.IsNullOrEmpty(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(u => u.Email.ToLower().Contains(searchTerm) || u.FullName.ToLower().Contains(searchTerm));
        }

        return await query.CountAsync();
    }
}
