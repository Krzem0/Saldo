using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Saldo.Application.Interfaces;
using Saldo.Application.Errors;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;
using Microsoft.Extensions.Logging;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class PartyRepository : IPartyRepository
{
    private readonly SaldoDbContext _context;
    private readonly ILogger<PartyRepository> _logger;

    public PartyRepository(SaldoDbContext context, ILogger<PartyRepository> logger)
    {
        _context = context;
        _logger = logger;
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
        try
        {
            await _context.Parties
                .Where(p => p.Id == id)
                .ExecuteDeleteAsync(ct);
        }
        catch (SqliteException ex) when (IsForeignKeyViolation(ex))
        {
            _logger.LogWarning(ex, "Party {PartyId} cannot be deleted because it is in use.", id);
            throw new ReferenceEntityInUseException(ex);
        }
    }

    private static bool IsForeignKeyViolation(SqliteException exception) =>
        exception.SqliteErrorCode == 19 && exception.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
}
