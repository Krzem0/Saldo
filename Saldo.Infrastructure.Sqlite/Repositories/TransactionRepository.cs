using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
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
        await using var databaseTransaction = await _context.Database.BeginTransactionAsync(ct);
        await EnsurePeriodAsync(transaction.Date, ct);
        ClearReferenceNavigations(transaction);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(ct);
        await databaseTransaction.CommitAsync(ct);
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        await using var databaseTransaction = await _context.Database.BeginTransactionAsync(ct);
        await EnsurePeriodAsync(transaction.Date, ct);
        var tags = transaction.Tags.ToList();
        // Delete-and-reinsert tags to avoid tracking conflicts
        await _context.TransactionTags
            .Where(tt => tt.TransactionId == transaction.Id)
            .ExecuteDeleteAsync(ct);

        foreach (var link in _context.ChangeTracker.Entries<TransactionTag>()
            .Where(entry => entry.Entity.TransactionId == transaction.Id).ToList())
            link.State = EntityState.Detached;

        // The caller may pass an entity still tracked after an earlier insert.
        // Detach it before clearing navigations so EF does not interpret that as
        // severing required relationships.
        var entry = _context.Entry(transaction);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }

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

    private Task EnsurePeriodAsync(DateOnly date, CancellationToken ct)
    {
        var period = CalendarMonth.FromDate(date);
        // The unique index arbitrates concurrent creation. Never replace an existing
        // period: its state and future closing snapshot must remain untouched.
        return _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO AccountingPeriods (StartDate, EndDateExclusive, IsClosed)
            VALUES ({period.StartDate}, {period.EndDateExclusive}, 0)
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
        await _context.Transactions
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync(ct);
    }
}
