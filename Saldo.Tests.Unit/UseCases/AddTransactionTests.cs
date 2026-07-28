using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Unit.UseCases;

public sealed class AddTransactionTests
{
    private static AddTransactionCommand ValidCommand() => new(
        Date: new DateOnly(2025, 1, 15), Type: TransactionType.Expense, Amount: 100m,
        CategoryId: 1, PayerId: 1, PayerName: "Me", CounterpartyId: 2, CounterpartyName: "Shop",
        Description: "Groceries", Location: "Shop", TagIds: []);

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsDtoWithCorrectData()
    {
        var useCase = new AddTransaction(new FakeTransactionRepository(), new FakePartyRepository([
            new() { Id = 1, Name = "Me" }, new() { Id = 2, Name = "Shop" }
        ]), new FakeLocationRepository([new Location { Id = 1, Name = "Shop" }]));

        var result = await useCase.ExecuteAsync(ValidCommand());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Id);
        Assert.Equal(new DateOnly(2025, 1, 15), result.Value.Date);
        Assert.Equal(TransactionType.Expense, result.Value.Type);
        Assert.Equal(100m, result.Value.Amount);
        Assert.Equal(1, result.Value.CategoryId);
        Assert.Equal(1, result.Value.PayerId);
        Assert.Equal(2, result.Value.CounterpartyId);
        Assert.Equal("Groceries", result.Value.Description);
        Assert.Equal("Shop", result.Value.Location);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_NonPositiveAmount_ReturnsValidationError(decimal amount)
    {
        var result = await CreateUseCase().ExecuteAsync(ValidCommand() with { Amount = amount });
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.AmountMustBePositive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidCategoryId_ReturnsValidationError(int categoryId)
    {
        var result = await CreateUseCase().ExecuteAsync(ValidCommand() with { CategoryId = categoryId });
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.CategoryRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidPayerId_ReturnsValidationError(int payerId)
    {
        var result = await CreateUseCase().ExecuteAsync(ValidCommand() with { PayerId = payerId, PayerName = null });
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.PayerRequired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidCounterpartyId_ReturnsValidationError(int counterpartyId)
    {
        var result = await CreateUseCase().ExecuteAsync(ValidCommand() with { CounterpartyId = counterpartyId, CounterpartyName = null });
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.CounterpartyRequired);
    }

    [Fact]
    public async Task ExecuteAsync_NewCounterpartyName_DoesNotCreateParty()
    {
        var parties = new FakePartyRepository([new() { Id = 1, Name = "Me" }]);
        var result = await new AddTransaction(new FakeTransactionRepository(), parties, new FakeLocationRepository())
            .ExecuteAsync(ValidCommand() with { CounterpartyId = null, CounterpartyName = "NewShop" });

        Assert.True(result.IsFailed);
        Assert.DoesNotContain(await parties.GetAllAsync(), party => party.Name == "NewShop");
    }

    private static AddTransaction CreateUseCase() => new(
        new FakeTransactionRepository(),
        new FakePartyRepository([new() { Id = 1, Name = "Me" }, new() { Id = 2, Name = "Shop" }]),
        new FakeLocationRepository([new Location { Id = 1, Name = "Shop" }]));
}
