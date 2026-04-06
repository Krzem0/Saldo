using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;
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
            .Include(t => t.Payer)
            .Include(t => t.Counterparty)
            .Include(t => t.Tags)
                .ThenInclude(tt => tt.Tag)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Transaction>> GetByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        return await _context.Transactions
            .Include(t => t.Category)
            .Include(t => t.Payer)
            .Include(t => t.Counterparty)
            .Include(t => t.Tags)
                .ThenInclude(tt => tt.Tag)
            .AsNoTracking()
            .Where(t => t.Date >= start && t.Date <= end)
            .OrderBy(t => t.Date)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        // Delete-and-reinsert tags to avoid tracking conflicts
        await _context.TransactionTags
            .Where(tt => tt.TransactionId == transaction.Id)
            .ExecuteDeleteAsync(ct);

        // Attach transaction and mark scalar fields as modified
        _context.Transactions.Attach(transaction).State = EntityState.Modified;

        // Add new tags explicitly with FK set
        foreach (var tag in transaction.Tags)
        {
            _context.TransactionTags.Add(new TransactionTag
            {
                TransactionId = transaction.Id,
                TagId = tag.TagId
            });
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await _context.Transactions
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync(ct);
    }
}
