using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;

namespace Saldo.Tests.Unit.Fakes;

internal sealed class FakePartyRepository : IPartyRepository
{
    private readonly List<Party> _store;
    private int _nextId;

    public FakePartyRepository(IEnumerable<Party>? seed = null)
    {
        _store = seed?.ToList() ?? [];
        _nextId = _store.Count == 0 ? 1 : _store.Max(p => p.Id) + 1;
    }

    public Task<Party?> GetByIdAsync(int id, CancellationToken ct = default)
        => Task.FromResult(_store.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Party>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Party>>(_store.OrderBy(p => p.Name).ToList());

    public Task AddAsync(Party party, CancellationToken ct = default)
    {
        if (party.Id <= 0)
        {
            party.Id = _nextId++;
        }

        _store.Add(party);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Party party, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(p => p.Id == party.Id);
        if (idx >= 0)
        {
            _store[idx] = party;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _store.RemoveAll(p => p.Id == id);
        return Task.CompletedTask;
    }
}
