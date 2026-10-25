using FluentResults;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Unit.UseCases;

public sealed class AccountingPeriodProtectionTests
{
    private static readonly DateOnly January = new(2026, 1, 10);
    private static readonly DateOnly April = new(2026, 4, 10);

    [Theory]
    [InlineData("add")]
    [InlineData("edit")]
    [InlineData("delete")]
    public async Task ClosedSourceMonth_ReturnsStableErrorWithoutChangingTransactions(string operation)
    {
        var transactions = new FakeTransactionRepository();
        await transactions.AddAsync(NewTransaction());
        var periods = new FakeAccountingPeriodRepository();
        Assert.True((await new CloseAccountingPeriod(periods).ExecuteAsync(2026, 1)).IsSuccess);

        var result = await ExecuteAsync(operation, transactions, periods, April);

        Assert.True(result.IsFailed);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.AccountingPeriod.Closed, error.Message);
        Assert.Equal("Date", error.Metadata["PropertyName"]);
        var original = Assert.Single(await transactions.GetByMonthAsync(2026, 1));
        Assert.Equal("Original", original.Description);
        Assert.Empty(await transactions.GetByMonthAsync(2026, 4));
    }

    [Fact]
    public async Task EditIntoClosedDestination_DoesNotMutateSource()
    {
        var transactions = new FakeTransactionRepository();
        await transactions.AddAsync(NewTransaction());
        var periods = new FakeAccountingPeriodRepository();
        await new CloseAccountingPeriod(periods).ExecuteAsync(2026, 4);

        var result = await ExecuteAsync("edit", transactions, periods, April);

        Assert.True(result.IsFailed);
        Assert.Equal(January, (await transactions.GetByIdAsync(1))!.Date);
        Assert.Equal("Original", (await transactions.GetByIdAsync(1))!.Description);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("edit")]
    [InlineData("delete")]
    public async Task PeriodClosedDuringWrite_ReturnsResultInsteadOfThrowing(string operation)
    {
        // The precheck sees an absent/open period; persistence reports a later close.
        var result = await ExecuteAsync(operation, new RejectingWritesRepository(),
            new FakeAccountingPeriodRepository(), January);

        Assert.True(result.IsFailed);
        Assert.Equal(ErrorCodes.AccountingPeriod.Closed, Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("edit")]
    [InlineData("delete")]
    public async Task ExplicitReopen_AllowsWritesAgain(string operation)
    {
        var transactions = new FakeTransactionRepository();
        await transactions.AddAsync(NewTransaction());
        var periods = new FakeAccountingPeriodRepository();
        await new CloseAccountingPeriod(periods).ExecuteAsync(2026, 1);
        Assert.True((await new ReopenAccountingPeriod(periods).ExecuteAsync(2026, 1)).IsSuccess);

        Assert.True((await ExecuteAsync(operation, transactions, periods, January)).IsSuccess);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(9999, 12)]
    public async Task CloseAndReopen_InvalidMonth_ReturnValidationResult(int year, int month)
    {
        var periods = new FakeAccountingPeriodRepository();

        var close = await new CloseAccountingPeriod(periods).ExecuteAsync(year, month);
        var reopen = await new ReopenAccountingPeriod(periods).ExecuteAsync(year, month);

        Assert.Equal(ErrorCodes.AccountingPeriod.InvalidMonth, Assert.Single(close.Errors).Message);
        Assert.Equal(ErrorCodes.AccountingPeriod.InvalidMonth, Assert.Single(reopen.Errors).Message);
    }

    private static async Task<Result> ExecuteAsync(string operation, ITransactionRepository transactions,
        IAccountingPeriodRepository periods, DateOnly destination)
    {
        var parties = new FakePartyRepository([]);
        var locations = new FakeLocationRepository();
        return operation switch
        {
            "add" => (await new AddTransaction(transactions, parties, locations, periods).ExecuteAsync(
                new AddTransactionCommand(January, TransactionType.Expense, 10m, 1,
                    null, null, null, null, "Added", null, []))).ToResult(),
            "edit" => (await new EditTransaction(transactions, parties, locations, periods).ExecuteAsync(
                new EditTransactionCommand(1, destination, TransactionType.Expense, 20m, 1,
                    null, null, null, null, "Changed", null, []))).ToResult(),
            "delete" => await new DeleteTransaction(transactions, periods).ExecuteAsync(1),
            _ => throw new ArgumentException(operation)
        };
    }

    private static Transaction NewTransaction() => new()
    {
        Id = 1, Date = January, Amount = 10m, Type = TransactionType.Expense,
        CategoryId = 1, Description = "Original"
    };

    private sealed class RejectingWritesRepository : ITransactionRepository
    {
        public Task<Transaction?> GetByIdAsync(int id, CancellationToken ct = default) =>
            Task.FromResult<Transaction?>(NewTransaction());
        public Task<IReadOnlyList<Transaction>> GetByMonthAsync(int year, int month, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task AddAsync(Transaction transaction, CancellationToken ct = default) =>
            throw new AccountingPeriodClosedException(January);
        public Task UpdateAsync(Transaction transaction, CancellationToken ct = default) =>
            throw new AccountingPeriodClosedException(January);
        public Task DeleteAsync(int id, CancellationToken ct = default) =>
            throw new AccountingPeriodClosedException(January);
    }
}
