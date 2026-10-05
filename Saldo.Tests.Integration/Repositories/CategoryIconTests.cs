using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class CategoryIconTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("#3366CC")]
    public async Task InitialMigration_AddEditClear_UpdatesExistingTransactions(string? color)
    {
        using var db = new TestDatabase(useMigrations: true);
        Assert.False(db.Context.Database.HasPendingModelChanges());
        var categories = new CategoryRepository(db.Context, NullLogger<CategoryRepository>.Instance);
        var category = await new AddCategory(categories).ExecuteAsync("Home", color, iconKey: " mdi:Home ");
        var transactions = new TransactionRepository(db.Context);
        await transactions.AddAsync(new Transaction { Date = new DateOnly(2019, 5, 1), Amount = 10,
            CategoryId = category.Id, Description = "Rent" });
        db.Context.ChangeTracker.Clear();
        Assert.Equal("mdi:Home", (await categories.GetByIdAsync(category.Id))!.IconKey);
        async Task<TransactionDto> Load() => Assert.Single(await new ListTransactions(transactions)
            .ExecuteAsync(new ListTransactionsQuery(2019, 5)));
        Assert.Equal("mdi:Home", (await Load()).CategoryIconKey);
        await new EditCategory(categories).ExecuteAsync(category.Id, "Home", color, iconKey: "mdi:Bank");
        db.Context.ChangeTracker.Clear();
        Assert.Equal("mdi:Bank", (await Load()).CategoryIconKey);
        await new EditCategory(categories).ExecuteAsync(category.Id, "Home", color, iconKey: null);
        db.Context.ChangeTracker.Clear();
        var cleared = await Load();
        Assert.Null(cleared.CategoryIconKey);
        Assert.Equal(color, cleared.CategoryColorCode);
    }
}
