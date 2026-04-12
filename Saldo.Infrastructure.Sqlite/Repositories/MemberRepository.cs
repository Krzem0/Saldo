using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class PartyRepository : IPartyRepository
{
    private readonly SaldoDbContext _context;

    public PartyRepository(SaldoDbContext context)
    {
        _context = context;
    }

    public async Task<Party?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Party>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Parties
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Party party, CancellationToken ct = default)
    {
        _context.Parties.Add(party);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Party party, CancellationToken ct = default)
    {
        _context.Parties.Update(party);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _context.Parties
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync(ct);
    }
}
