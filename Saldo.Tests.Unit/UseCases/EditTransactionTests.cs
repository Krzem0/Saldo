using Saldo.Application.DTOs;
using Saldo.Application.Errors;
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
        var locations = new FakeLocationRepository();
        var parties = new FakePartyRepository([
            new() { Id = 1, Name = "Me" },
            new() { Id = 2, Name = "Employer" }
        ]);
        await repo.AddAsync(new Transaction
        {
            Date = new DateOnly(2025, 1, 10),
            Type = TransactionType.Expense,
            Amount = 100m,
            CategoryId = 1,
            PayerId = 1,
            CounterpartyId = 1,
            Description = "Original"
        });
        return (new EditTransaction(repo, parties, locations), repo, 1);
    }

    private static EditTransactionCommand ValidCommand(int id = 1) => new(
        Id: id,
        Date: new DateOnly(2025, 6, 20),
        Type: TransactionType.Income,
        Amount: 250m,
        CategoryId: 2,
        PayerId: 2,
        PayerName: "Employer",
        CounterpartyId: 2,
        CounterpartyName: "Employer",
        Description: "Updated",
        Location: "Office",
        TagIds: []
    );

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsUpdatedDto()
    {
        var (useCase, _, _) = await SetupAsync();

        var result = await useCase.ExecuteAsync(ValidCommand());

        Assert.True(result.IsSuccess);
        var dto = result.Value;

        Assert.Equal(1, dto.Id);
        Assert.Equal(new DateOnly(2025, 6, 20), dto.Date);
        Assert.Equal(TransactionType.Income, dto.Type);
        Assert.Equal(250m, dto.Amount);
        Assert.Equal(2, dto.CategoryId);
        Assert.Equal("Updated", dto.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidId_ThrowsArgumentException(int id)
    {
        var (useCase, _, _) = await SetupAsync();

        var result = await useCase.ExecuteAsync(ValidCommand(id));

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.IdMustBePositive);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentId_ThrowsKeyNotFoundException()
    {
        var (useCase, _, _) = await SetupAsync();

        var result = await useCase.ExecuteAsync(ValidCommand(id: 999));

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_NonPositiveAmount_ThrowsArgumentException(decimal amount)
    {
        var (useCase, _, _) = await SetupAsync();

        var result = await useCase.ExecuteAsync(ValidCommand() with { Amount = amount });

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == ErrorCodes.Transaction.AmountMustBePositive);
    }
}
