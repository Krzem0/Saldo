using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Unit.UseCases;

public sealed class AddTransactionTests
{
    private static AddTransactionCommand ValidCommand() => new(
        Date: new DateOnly(2025, 1, 15),
        Direction: TransactionDirection.Expense,
        Amount: 100m,
        CategoryId: 1,
        PayerId: 1,
        CounterpartyId: 1,
        Description: "Groceries",
        Location: "Shop",
        TagIds: []
    );

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsDtoWithCorrectData()
    {
        var useCase = new AddTransaction(new FakeTransactionRepository());

        var result = await useCase.ExecuteAsync(ValidCommand());

        Assert.Equal(1, result.Id);
        Assert.Equal(new DateOnly(2025, 1, 15), result.Date);
        Assert.Equal(TransactionDirection.Expense, result.Direction);
        Assert.Equal(100m, result.Amount);
        Assert.Equal(1, result.CategoryId);
        Assert.Equal(1, result.PayerId);
        Assert.Equal(1, result.CounterpartyId);
        Assert.Equal("Groceries", result.Description);
        Assert.Equal("Shop", result.Location);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_NonPositiveAmount_ThrowsArgumentException(decimal amount)
    {
        var useCase = new AddTransaction(new FakeTransactionRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(ValidCommand() with { Amount = amount }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidCategoryId_ThrowsArgumentException(int categoryId)
    {
        var useCase = new AddTransaction(new FakeTransactionRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(ValidCommand() with { CategoryId = categoryId }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidPayerId_ThrowsArgumentException(int payerId)
    {
        var useCase = new AddTransaction(new FakeTransactionRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(ValidCommand() with { PayerId = payerId }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidCounterpartyId_ThrowsArgumentException(int counterpartyId)
    {
        var useCase = new AddTransaction(new FakeTransactionRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(ValidCommand() with { CounterpartyId = counterpartyId }));
    }
}
