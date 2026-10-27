using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using Saldo.Application.Errors;
using Saldo.Domain.Entities;
using Saldo.Domain.ValueObjects;
using Saldo.Infrastructure.Sqlite.Persistence;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly SaldoDbContext _context;

    public TransactionRepository(SaldoDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Transactions
            .Include(t => t.Category)
            .Include(t => t.Location)
            .Include(t => t.Payer)
            .Include(t => t.Counterparty)
            .Include(t => t.Tags)
                .ThenInclude(tt => tt.Tag)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Transaction>> GetByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var period = CalendarMonth.FromYearMonth(year, month);
        var start = period.StartDate;
        var end = period.EndDateExclusive;

        return await _context.Transactions
            .Include(t => t.Category)
            .Include(t => t.Location)
            .Include(t => t.Payer)
            .Include(t => t.Counterparty)
            .Include(t => t.Tags)
                .ThenInclude(tt => tt.Tag)
            .AsNoTracking()
            .Where(t => t.Date >= start && t.Date < end)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        _context.EnsureCanWrite();
        await using var databaseTransaction = await _context.Database.BeginTransactionAsync(ct);
        await EnsureOpenAsync(transaction.Date, ct);
        await EnsurePeriodAsync(transaction.Date, ct);
        ClearReferenceNavigations(transaction);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(ct);
        await databaseTransaction.CommitAsync(ct);
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        _context.EnsureCanWrite();
        await using var databaseTransaction = await _context.Database.BeginTransactionAsync(ct);
        // Read the persisted source date, not a potentially edited/tracked entity.
        var sourceDate = await _context.Transactions.AsNoTracking()
            .Where(t => t.Id == transaction.Id).Select(t => (DateOnly?)t.Date).SingleOrDefaultAsync(ct);
        if (sourceDate is { } originalDate)
            await EnsureOpenAsync(originalDate, ct);
        await EnsureOpenAsync(transaction.Date, ct);
        await EnsurePeriodAsync(transaction.Date, ct);
        var tags = transaction.Tags.ToList();
        // Delete-and-reinsert tags to avoid tracking conflicts
        await _context.TransactionTags
            .Where(tt => tt.TransactionId == transaction.Id)
            .ExecuteDeleteAsync(ct);

        foreach (var link in _context.ChangeTracker.Entries<TransactionTag>()
            .Where(entry => entry.Entity.TransactionId == transaction.Id).ToList())
            link.State = EntityState.Detached;

        // A no-tracking read may return a different instance from one still
        // tracked after an earlier insert in this scope. Detach either instance
        // before attaching the edited data and clearing required navigations.
        foreach (var entry in _context.ChangeTracker.Entries<Transaction>()
                     .Where(entry => entry.Entity.Id == transaction.Id).ToList())
            entry.State = EntityState.Detached;

        // Reference entities are selected by their foreign keys. They may come from
        // no-tracking queries, so attaching their navigation objects could make EF
        // try to insert them again (or track two Party objects with the same key).
        ClearReferenceNavigations(transaction);
        // Attach only scalar transaction data; tag links are inserted separately below.
        transaction.Tags = [];

        // Attach transaction and mark scalar fields as modified
        _context.Transactions.Attach(transaction).State = EntityState.Modified;

        // Add new tags explicitly with FK set
        foreach (var tag in tags)
        {
            _context.TransactionTags.Add(new TransactionTag
            {
                TransactionId = transaction.Id,
                TagId = tag.TagId
            });
        }

        await _context.SaveChangesAsync(ct);
        await databaseTransaction.CommitAsync(ct);
    }

    private async Task EnsureOpenAsync(DateOnly date, CancellationToken ct)
    {
        if (await _context.AccountingPeriods.AnyAsync(
                period => period.IsClosed && period.StartDate <= date && date < period.EndDateExclusive, ct))
            throw new AccountingPeriodClosedException(date);
    }

    private Task EnsurePeriodAsync(DateOnly date, CancellationToken ct)
    {
        var period = CalendarMonth.FromDate(date);
        // Filter existing months before inserting so AUTOINCREMENT is not consumed
        // by every transaction save. The write transaction serializes creation;
        // the unique index remains a final guard against duplicate months.
        return _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO AccountingPeriods (StartDate, EndDateExclusive, IsClosed)
            SELECT {period.StartDate}, {period.EndDateExclusive}, 0
            WHERE NOT EXISTS (
                SELECT 1 FROM AccountingPeriods WHERE StartDate = {period.StartDate})
            ON CONFLICT (StartDate) DO NOTHING;
            """, ct);
    }

    private static void ClearReferenceNavigations(Transaction transaction)
    {
        transaction.Category = null!;
        transaction.Payer = null;
        transaction.Counterparty = null;
        transaction.Location = null;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _context.EnsureCanWrite();
        await using var databaseTransaction = await _context.Database.BeginTransactionAsync(ct);
        var date = await _context.Transactions.AsNoTracking()
            .Where(t => t.Id == id).Select(t => (DateOnly?)t.Date).SingleOrDefaultAsync(ct);
        if (date is { } originalDate)
            await EnsureOpenAsync(originalDate, ct);
        await _context.Transactions
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync(ct);
        await databaseTransaction.CommitAsync(ct);
    }
}
