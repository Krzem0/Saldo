using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;

namespace Saldo.Tests.Unit.Fakes;

internal sealed class FakeLocationRepository : ILocationRepository
{
    private readonly List<Location> _store;
    private int _nextId;

    public FakeLocationRepository(IEnumerable<Location>? seed = null)
    {
        _store = seed?.ToList() ?? [];
        _nextId = _store.Count == 0 ? 1 : _store.Max(location => location.Id) + 1;
    }

    public Task<Location?> GetByIdAsync(int id, CancellationToken ct = default)
        => Task.FromResult(_store.FirstOrDefault(location => location.Id == id));

    public Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Location>>(_store.OrderBy(location => location.Name).ToList());

    public Task AddAsync(Location location, CancellationToken ct = default)
    {
        if (location.Id <= 0)
        {
            location.Id = _nextId++;
        }

        _store.Add(location);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Location location, CancellationToken ct = default)
    {
        var idx = _store.FindIndex(existing => existing.Id == location.Id);
        if (idx >= 0)
        {
            _store[idx] = location;
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _store.RemoveAll(location => location.Id == id);
        return Task.CompletedTask;
    }
}
