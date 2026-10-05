using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Unit.UseCases;

public sealed class TransactionRequirementsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n ")]
    public Task AddAndEdit_RejectMissingDescription(string? description) =>
        AssertRejectedByBoth(description, new DateOnly(2019, 5, 1), TransactionType.Expense,
            ErrorCodes.Transaction.DescriptionRequired, "Description");

    [Fact]
    public Task AddAndEdit_RejectOverlongDescription() =>
        AssertRejectedByBoth(new string('x', 501), new DateOnly(2019, 5, 1), TransactionType.Expense,
            ErrorCodes.Transaction.DescriptionTooLong, "Description");

    [Fact]
    public Task AddAndEdit_RejectMissingDate() =>
        AssertRejectedByBoth("Groceries", default, TransactionType.Expense,
            ErrorCodes.Transaction.DateRequired, "Date");

    [Fact]
    public Task AddAndEdit_RejectUnknownType() =>
        AssertRejectedByBoth("Groceries", new DateOnly(2019, 5, 1), (TransactionType)99,
            ErrorCodes.Transaction.TypeInvalid, "Type");

    private static async Task AssertRejectedByBoth(string? description, DateOnly date,
        TransactionType type, string errorCode, string propertyName)
    {
        var repository = new FakeTransactionRepository();
        await repository.AddAsync(new Transaction { Date = new DateOnly(2019, 5, 1),
            CategoryId = 1, Amount = 10m, Description = "Original" });
        var parties = new FakePartyRepository([]);
        var locations = new FakeLocationRepository([]);
        var added = await new AddTransaction(repository, parties, locations).ExecuteAsync(
            new AddTransactionCommand(date, type, 20m, 1, null, null, null, null, description, null, []));
        var edited = await new EditTransaction(repository, parties, locations).ExecuteAsync(
            new EditTransactionCommand(1, date, type, 20m, 1, null, null, null, null, description, null, []));
        foreach (var result in new[] { added, edited })
        {
            Assert.True(result.IsFailed);
            Assert.Contains(result.Errors, error => error.Message == errorCode
                && Equals(error.Metadata["PropertyName"], propertyName));
        }
        var unchanged = Assert.Single(await repository.GetByMonthAsync(2019, 5));
        Assert.Equal("Original", unchanged.Description);
        Assert.Equal(10m, unchanged.Amount);
    }
}
