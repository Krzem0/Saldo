using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Unit.UseCases;

public sealed class GetSummaryTests
{
    private static Transaction MakeTransaction(TransactionDirection direction, decimal amount, int month = 1) => new()
    {
        Date = new DateOnly(2025, month, 1),
        Direction = direction,
        Amount = amount,
        CategoryId = 1,
        PayerId = 1,
        CounterpartyId = 1
    };

    [Fact]
    public async Task ExecuteAsync_NoTransactions_ReturnsAllZeros()
    {
        var useCase = new GetSummary(new FakeTransactionRepository());

        var result = await useCase.ExecuteAsync(new ListTransactionsQuery(2025, 1));

        Assert.Equal(0m, result.TotalIncome);
        Assert.Equal(0m, result.TotalExpense);
        Assert.Equal(0m, result.Balance);
    }

    [Fact]
    public async Task ExecuteAsync_OnlyIncome_CorrectSums()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(MakeTransaction(TransactionDirection.Income, 500m));
        await repo.AddAsync(MakeTransaction(TransactionDirection.Income, 300m));

        var result = await new GetSummary(repo).ExecuteAsync(new ListTransactionsQuery(2025, 1));

        Assert.Equal(800m, result.TotalIncome);
        Assert.Equal(0m, result.TotalExpense);
        Assert.Equal(800m, result.Balance);
    }

    [Fact]
    public async Task ExecuteAsync_MixedTransactions_BalanceIsIncomeMinusExpense()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(MakeTransaction(TransactionDirection.Income, 1000m));
        await repo.AddAsync(MakeTransaction(TransactionDirection.Expense, 350m));
        await repo.AddAsync(MakeTransaction(TransactionDirection.Expense, 150m));

        var result = await new GetSummary(repo).ExecuteAsync(new ListTransactionsQuery(2025, 1));

        Assert.Equal(1000m, result.TotalIncome);
        Assert.Equal(500m, result.TotalExpense);
        Assert.Equal(500m, result.Balance);
    }

    [Fact]
    public async Task ExecuteAsync_IgnoresOtherMonths()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(MakeTransaction(TransactionDirection.Income, 1000m, month: 1));
        await repo.AddAsync(MakeTransaction(TransactionDirection.Income, 9999m, month: 2));

        var result = await new GetSummary(repo).ExecuteAsync(new ListTransactionsQuery(2025, 1));

        Assert.Equal(1000m, result.TotalIncome);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCorrectYearAndMonth()
    {
        var useCase = new GetSummary(new FakeTransactionRepository());

        var result = await useCase.ExecuteAsync(new ListTransactionsQuery(2024, 11));

        Assert.Equal(2024, result.Year);
        Assert.Equal(11, result.Month);
    }
}
