using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class TransactionRepositoryTests : IDisposable
{
    private readonly TestDatabase _db;
    private readonly TransactionRepository _sut;

    public TransactionRepositoryTests()
    {
        _db = new TestDatabase();
        _sut = new TransactionRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<(int CategoryId, int PayerId, int CounterpartyId)> SeedReferencesAsync()
    {
        var category = new Category { Name = "Food" };
        var payer = new Party { Name = "Alice" };
        var counterparty = new Party { Name = "Shop" };

        _db.Context.Categories.Add(category);
        _db.Context.Parties.AddRange(payer, counterparty);
        await _db.Context.SaveChangesAsync();

        return (category.Id, payer.Id, counterparty.Id);
    }

    private static Transaction MakeTransaction(int categoryId, int payerId, int counterpartyId,
        DateOnly? date = null, decimal amount = 100m, TransactionDirection direction = TransactionDirection.Expense) =>
        new()
        {
            Date = date ?? new DateOnly(2025, 6, 1),
            Direction = direction,
            Amount = amount,
            CategoryId = categoryId,
            PayerId = payerId,
            CounterpartyId = counterpartyId,
            Description = "Test",
            Location = "Somewhere"
        };

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_AssignsIdAndPersists()
    {
        var (cat, pay, cnt) = await SeedReferencesAsync();

        var transaction = MakeTransaction(cat, pay, cnt);
        await _sut.AddAsync(transaction);

        Assert.True(transaction.Id > 0);
        var loaded = await _sut.GetByIdAsync(transaction.Id);
        Assert.NotNull(loaded);
        Assert.Equal(100m, loaded.Amount);
    }

    [Fact]
    public async Task GetByIdAsync_LoadsNavigationProperties()
    {
        var (cat, pay, cnt) = await SeedReferencesAsync();
        var transaction = MakeTransaction(cat, pay, cnt);
        await _sut.AddAsync(transaction);

        var loaded = await _sut.GetByIdAsync(transaction.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Food", loaded.Category.Name);
        Assert.Equal("Alice", loaded.Payer.Name);
        Assert.Equal("Shop", loaded.Counterparty.Name);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ReturnsNull()
    {
        var loaded = await _sut.GetByIdAsync(999);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetByMonthAsync_ReturnsOnlyMatchingMonth()
    {
        var (cat, pay, cnt) = await SeedReferencesAsync();
        await _sut.AddAsync(MakeTransaction(cat, pay, cnt, date: new DateOnly(2025, 6, 10)));
        await _sut.AddAsync(MakeTransaction(cat, pay, cnt, date: new DateOnly(2025, 6, 28)));
        await _sut.AddAsync(MakeTransaction(cat, pay, cnt, date: new DateOnly(2025, 7, 1)));

        var result = await _sut.GetByMonthAsync(2025, 6);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal(6, t.Date.Month));
    }

    [Fact]
    public async Task GetByMonthAsync_OrdersByDate()
    {
        var (cat, pay, cnt) = await SeedReferencesAsync();
        await _sut.AddAsync(MakeTransaction(cat, pay, cnt, date: new DateOnly(2025, 6, 20)));
        await _sut.AddAsync(MakeTransaction(cat, pay, cnt, date: new DateOnly(2025, 6, 5)));

        var result = await _sut.GetByMonthAsync(2025, 6);

        Assert.Equal(new DateOnly(2025, 6, 5), result[0].Date);
        Assert.Equal(new DateOnly(2025, 6, 20), result[1].Date);
    }

    [Fact]
    public async Task UpdateAsync_ChangesScalarFields()
    {
        var (cat, pay, cnt) = await SeedReferencesAsync();
        var transaction = MakeTransaction(cat, pay, cnt, amount: 50m);
        await _sut.AddAsync(transaction);

        transaction.Amount = 200m;
        transaction.Description = "Updated";
        await _sut.UpdateAsync(transaction);

        var loaded = await _sut.GetByIdAsync(transaction.Id);
        Assert.NotNull(loaded);
        Assert.Equal(200m, loaded.Amount);
        Assert.Equal("Updated", loaded.Description);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTransaction()
    {
        var (cat, pay, cnt) = await SeedReferencesAsync();
        var transaction = MakeTransaction(cat, pay, cnt);
        await _sut.AddAsync(transaction);

        await _sut.DeleteAsync(transaction.Id);

        var loaded = await _sut.GetByIdAsync(transaction.Id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task AddAsync_WithTags_PersistsTagLinks()
    {
        var (cat, pay, cnt) = await SeedReferencesAsync();

        var tag = new Tag { Name = "groceries" };
        _db.Context.Tags.Add(tag);
        await _db.Context.SaveChangesAsync();

        var transaction = MakeTransaction(cat, pay, cnt);
        transaction.Tags.Add(new TransactionTag { TagId = tag.Id });
        await _sut.AddAsync(transaction);

        var loaded = await _sut.GetByIdAsync(transaction.Id);
        Assert.NotNull(loaded);
        Assert.Single(loaded.Tags);
        Assert.Equal("groceries", loaded.Tags.First().Tag.Name);
    }
}
