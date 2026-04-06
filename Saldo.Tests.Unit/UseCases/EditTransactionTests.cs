using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Unit.UseCases;

public sealed class EditTransactionTests
{
    private static async Task<(EditTransaction UseCase, FakeTransactionRepository Repo, int Id)> SetupAsync()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(new Transaction
        {
            Date = new DateOnly(2025, 1, 10),
            Direction = TransactionDirection.Expense,
            Amount = 100m,
            CategoryId = 1,
            PayerId = 1,
            CounterpartyId = 1,
            Description = "Original"
        });
        return (new EditTransaction(repo), repo, 1);
    }

    private static EditTransactionCommand ValidCommand(int id = 1) => new(
        Id: id,
        Date: new DateOnly(2025, 6, 20),
        Direction: TransactionDirection.Income,
        Amount: 250m,
        CategoryId: 2,
        PayerId: 2,
        CounterpartyId: 2,
        Description: "Updated",
        Location: "Office",
        TagIds: []
    );

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsUpdatedDto()
    {
        var (useCase, _, _) = await SetupAsync();

        var result = await useCase.ExecuteAsync(ValidCommand());

        Assert.Equal(1, result.Id);
        Assert.Equal(new DateOnly(2025, 6, 20), result.Date);
        Assert.Equal(TransactionDirection.Income, result.Direction);
        Assert.Equal(250m, result.Amount);
        Assert.Equal(2, result.CategoryId);
        Assert.Equal("Updated", result.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidId_ThrowsArgumentException(int id)
    {
        var (useCase, _, _) = await SetupAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(ValidCommand(id)));
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentId_ThrowsKeyNotFoundException()
    {
        var (useCase, _, _) = await SetupAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            useCase.ExecuteAsync(ValidCommand(id: 999)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_NonPositiveAmount_ThrowsArgumentException(decimal amount)
    {
        var (useCase, _, _) = await SetupAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(ValidCommand() with { Amount = amount }));
    }
}
