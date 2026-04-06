using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class TagRepository : ITagRepository
{
    private readonly SaldoDbContext _context;

    public TagRepository(SaldoDbContext context)
    {
        _context = context;
    }

    public async Task<Tag?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Tag tag, CancellationToken ct = default)
    {
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Tag tag, CancellationToken ct = default)
    {
        _context.Tags.Update(tag);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _context.Tags
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync(ct);
    }
}
