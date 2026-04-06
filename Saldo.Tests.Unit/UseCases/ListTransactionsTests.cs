using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Unit.UseCases;

public sealed class ListTransactionsTests
{
    [Fact]
    public async Task ExecuteAsync_NoTransactions_ReturnsEmptyList()
    {
        var useCase = new ListTransactions(new FakeTransactionRepository());

        var result = await useCase.ExecuteAsync(new ListTransactionsQuery(2025, 1));

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExecuteAsync_TransactionsInMonth_ReturnsOnlyThatMonth()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(new Transaction { Date = new DateOnly(2025, 1, 10), Amount = 100m, Direction = TransactionDirection.Expense, CategoryId = 1, PayerId = 1, CounterpartyId = 1 });
        await repo.AddAsync(new Transaction { Date = new DateOnly(2025, 1, 20), Amount = 200m, Direction = TransactionDirection.Income, CategoryId = 1, PayerId = 1, CounterpartyId = 1 });
        await repo.AddAsync(new Transaction { Date = new DateOnly(2025, 2, 5), Amount = 50m, Direction = TransactionDirection.Expense, CategoryId = 1, PayerId = 1, CounterpartyId = 1 });

        var useCase = new ListTransactions(repo);
        var result = await useCase.ExecuteAsync(new ListTransactionsQuery(2025, 1));

        Assert.Equal(2, result.Count);
        Assert.All(result, dto => Assert.Equal(1, dto.Date.Month));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDtoWithCorrectFields()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(new Transaction
        {
            Date = new DateOnly(2025, 3, 15),
            Amount = 99.50m,
            Direction = TransactionDirection.Expense,
            CategoryId = 2,
            PayerId = 3,
            CounterpartyId = 4,
            Description = "Lunch",
            Location = "Cafe"
        });

        var useCase = new ListTransactions(repo);
        var result = await useCase.ExecuteAsync(new ListTransactionsQuery(2025, 3));

        var dto = Assert.Single(result);
        Assert.Equal(new DateOnly(2025, 3, 15), dto.Date);
        Assert.Equal(99.50m, dto.Amount);
        Assert.Equal(TransactionDirection.Expense, dto.Direction);
        Assert.Equal(2, dto.CategoryId);
        Assert.Equal(3, dto.PayerId);
        Assert.Equal(4, dto.CounterpartyId);
        Assert.Equal("Lunch", dto.Description);
        Assert.Equal("Cafe", dto.Location);
    }
}
