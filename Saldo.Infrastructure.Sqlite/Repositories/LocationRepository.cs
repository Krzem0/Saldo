using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Saldo.Application.Interfaces;
using Saldo.Application.Errors;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;
using Microsoft.Extensions.Logging;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class LocationRepository : ILocationRepository
{
    private readonly SaldoDbContext _context;
    private readonly ILogger<LocationRepository> _logger;

    public LocationRepository(SaldoDbContext context, ILogger<LocationRepository> logger)
    {
        _context = context;
        _logger = logger;
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
        try
        {
            await _context.Locations
                .Where(l => l.Id == id)
                .ExecuteDeleteAsync(ct);
        }
        catch (SqliteException ex) when (IsForeignKeyViolation(ex))
        {
            _logger.LogWarning(ex, "Location {LocationId} cannot be deleted because it is in use.", id);
            throw new ReferenceEntityInUseException(ex);
        }
    }

    private static bool IsForeignKeyViolation(SqliteException exception) =>
        exception.SqliteErrorCode == 19 && exception.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
}
