using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class LocationRepository : ILocationRepository
{
    private readonly SaldoDbContext _context;

    public LocationRepository(SaldoDbContext context)
    {
        _context = context;
    }

    public async Task<Location?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Locations
            .AsNoTracking()
            .OrderBy(l => l.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Location location, CancellationToken ct = default)
    {
        _context.Locations.Add(location);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Location location, CancellationToken ct = default)
    {
        _context.Locations.Update(location);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _context.Locations
            .Where(l => l.Id == id)
            .ExecuteDeleteAsync(ct);
    }
}
