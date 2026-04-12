using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Unit.UseCases;

public sealed class DeleteTransactionTests
{
    private static async Task<(DeleteTransaction UseCase, FakeTransactionRepository Repo)> SetupAsync()
    {
        var repo = new FakeTransactionRepository();
        await repo.AddAsync(new Transaction
        {
            Date = new DateOnly(2025, 1, 1),
            Direction = TransactionDirection.Expense,
            Amount = 50m,
            CategoryId = 1,
            PayerId = 1,
            CounterpartyId = 1
        });
        return (new DeleteTransaction(repo), repo);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingId_RemovesTransaction()
    {
        var (useCase, repo) = await SetupAsync();

        var result = await useCase.ExecuteAsync(1);

        Assert.True(result.IsSuccess);

        var deleted = await repo.GetByIdAsync(1);
        Assert.Null(deleted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_InvalidId_ThrowsArgumentException(int id)
    {
        var (useCase, _) = await SetupAsync();

        var result = await useCase.ExecuteAsync(id);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "Id must be positive.");
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentId_ThrowsKeyNotFoundException()
    {
        var (useCase, _) = await SetupAsync();

        var result = await useCase.ExecuteAsync(999);

        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, e => e.Message == "Transaction 999 not found.");
    }
}
