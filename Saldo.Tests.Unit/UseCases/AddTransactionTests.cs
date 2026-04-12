using Saldo.Application.DTOs;
using Saldo.Application.Errors;
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

        Assert.True(result.IsSuccess);
        var dto = result.Value;

        Assert.Equal(1, dto.Id);
        Assert.Equal(new DateOnly(2025, 1, 15), dto.Date);
        Assert.Equal(TransactionDirection.Expense, dto.Direction);
        Assert.Equal(100m, dto.Amount);
        Assert.Equal(1, dto.CategoryId);
        Assert.Equal(1, dto.PayerId);
        Assert.Equal(1, dto.CounterpartyId);
        Assert.Equal("Groceries", dto.Description);
        Assert.Equal("Shop", dto.Location);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_NonPositiveAmount_ThrowsArgumentException(decimal amount)
    {
        var useCase = new AddTransaction(new FakeTransactionRepository());

        var result = await useCase.ExecuteAsync(ValidCommand() with { Amount = amount });

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.AmountMustBePositive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidCategoryId_ThrowsArgumentException(int categoryId)
    {
        var useCase = new AddTransaction(new FakeTransactionRepository());

        var result = await useCase.ExecuteAsync(ValidCommand() with { CategoryId = categoryId });

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.CategoryRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidPayerId_ThrowsArgumentException(int payerId)
    {
        var useCase = new AddTransaction(new FakeTransactionRepository());

        var result = await useCase.ExecuteAsync(ValidCommand() with { PayerId = payerId });

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.PayerRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidCounterpartyId_ThrowsArgumentException(int counterpartyId)
    {
        var useCase = new AddTransaction(new FakeTransactionRepository());

        var result = await useCase.ExecuteAsync(ValidCommand() with { CounterpartyId = counterpartyId });

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.CounterpartyRequired);
    }
}
