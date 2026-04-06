using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class CounterpartyRepository : ICounterpartyRepository
{
    private readonly SaldoDbContext _context;

    public CounterpartyRepository(SaldoDbContext context)
    {
        _context = context;
    }

    public async Task<Counterparty?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Counterparties
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Counterparty>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Counterparties
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Counterparty counterparty, CancellationToken ct = default)
    {
        _context.Counterparties.Add(counterparty);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Counterparty counterparty, CancellationToken ct = default)
    {
        _context.Counterparties.Update(counterparty);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _context.Counterparties
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync(ct);
    }
}
