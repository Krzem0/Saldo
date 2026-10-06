using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class TransactionSettingsRepository(SaldoDbContext context) : ITransactionSettingsRepository
{
    public async Task<TransactionSettings> GetAsync(CancellationToken ct = default) =>
        await context.TransactionSettings.AsNoTracking().SingleOrDefaultAsync(ct) ?? new TransactionSettings();
    public async Task SaveAsync(TransactionSettings settings, CancellationToken ct = default)
    {
        var current = await context.TransactionSettings.SingleOrDefaultAsync(ct);
        if (current is null)
        {
            current = new TransactionSettings();
            context.TransactionSettings.Add(current);
        }
        current.DefaultPayerId = settings.DefaultPayerId;
        await context.SaveChangesAsync(ct);
    }
}
