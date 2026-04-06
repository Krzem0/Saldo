using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;

namespace Saldo.Tests.Unit.Fakes;

internal sealed class FakeTransactionRepository : ITransactionRepository
{
    private readonly List<Transaction> _store = [];
    private int _nextId = 1;

    public Task<Transaction?> GetByIdAsync(int id, CancellationToken ct = default)
        => Task.FromResult(_store.FirstOrDefault(t => t.Id == id));

    public Task<IReadOnlyList<Transaction>> GetByMonthAsync(int year, int month, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Transaction>>(
            _store.Where(t => t.Date.Year == year && t.Date.Month == month).ToList());

    public Task AddAsync(Transaction transaction, CancellationToken ct = default)
    {
        transaction.Id = _nextId++;
        _store.Add(transaction);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Transaction transaction, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(t => t.Id == transaction.Id);
        if (idx >= 0) _store[idx] = transaction;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _store.RemoveAll(t => t.Id == id);
        return Task.CompletedTask;
    }
}
