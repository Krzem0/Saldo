using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class CategoryColorTests
{
    [Theory]
    [InlineData(" #a1b2c3 ", "#A1B2C3")]
    [InlineData(null, null)]
    public async Task AddCategory_FreshMigratedDatabase_PersistsOptionalColor(string? input, string? expected)
    {
        using var db = new TestDatabase(useMigrations: true);
        var repository = new CategoryRepository(db.Context, NullLogger<CategoryRepository>.Instance);
        var category = await new AddCategory(repository).ExecuteAsync("Food", input);
        db.Context.ChangeTracker.Clear();

        var loaded = await repository.GetByIdAsync(category.Id);
        var listed = Assert.Single(await repository.GetAllAsync());

        Assert.NotNull(loaded);
        Assert.Equal(expected, loaded.ColorCode);
        Assert.Equal(expected, listed.ColorCode);
    }

    [Theory]
    [InlineData(null, " #abcdef ", "#ABCDEF")]
    [InlineData("#112233", "#ABCDEF", "#ABCDEF")]
    [InlineData("#112233", null, null)]
    public async Task EditCategory_ExistingTransaction_UsesUpdatedColorInListDto(
        string? initialColor, string? input, string? expected)
    {
        using var db = new TestDatabase(useMigrations: true);
        var categories = new CategoryRepository(db.Context, NullLogger<CategoryRepository>.Instance);
        var category = await new AddCategory(categories).ExecuteAsync("Food", initialColor);
        var party = new Party { Name = "Me" };
        db.Context.Parties.Add(party);
        await db.Context.SaveChangesAsync();

        var transactions = new TransactionRepository(db.Context);
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 10, 4),
            Type = TransactionType.Expense,
            Amount = 10m,
            CategoryId = category.Id,
            PayerId = party.Id,
            CounterpartyId = party.Id
        };
        await transactions.AddAsync(transaction);
        db.Context.ChangeTracker.Clear();

        await new EditCategory(categories).ExecuteAsync(category.Id, "Food", input);
        db.Context.ChangeTracker.Clear();

        var loadedCategory = await categories.GetByIdAsync(category.Id);
        var loadedTransaction = await transactions.GetByIdAsync(transaction.Id);
        var dto = Assert.Single(await new ListTransactions(transactions)
            .ExecuteAsync(new ListTransactionsQuery(2026, 10)));

        Assert.NotNull(loadedCategory);
        Assert.NotNull(loadedTransaction);
        Assert.Equal(expected, loadedCategory.ColorCode);
        Assert.Equal(expected, loadedTransaction.Category.ColorCode);
        Assert.Equal(category.Id, dto.CategoryId);
        Assert.Equal("Food", dto.CategoryName);
        Assert.Equal(expected, dto.CategoryColorCode);
    }
}
