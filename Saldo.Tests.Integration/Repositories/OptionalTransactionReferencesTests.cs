using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class OptionalTransactionReferencesTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task InitialMigration_AddThenClearReferences_PersistsAndLoadsInMonthlyList(bool withPayer, bool withCounterparty)
    {
        using var db = new TestDatabase(useMigrations: true);
        Assert.False(db.Context.Database.HasPendingModelChanges());
        var category = new Category { Name = "Food" };
        var party = new Party { Name = "Me" };
        db.Context.AddRange(category, party);
        await db.Context.SaveChangesAsync();
        var transactions = new TransactionRepository(db.Context);
        var parties = new PartyRepository(db.Context, NullLogger<PartyRepository>.Instance);
        var locations = new LocationRepository(db.Context, NullLogger<LocationRepository>.Instance);
        var command = new AddTransactionCommand(new DateOnly(2019, 5, 1), TransactionType.Expense,
            12m, category.Id, withPayer ? party.Id : null, null,
            withCounterparty ? party.Id : null, null, " Groceries ", null, []);

        var added = await new AddTransaction(transactions, parties, locations).ExecuteAsync(command);
        Assert.True(added.IsSuccess);
        Assert.Equal(withPayer ? party.Id : (int?)null, added.Value.PayerId);
        Assert.Equal(withPayer ? "Me" : null, added.Value.PayerName);
        Assert.Equal(withCounterparty ? party.Id : (int?)null, added.Value.CounterpartyId);
        Assert.Equal(withCounterparty ? "Me" : null, added.Value.CounterpartyName);
        Assert.Equal("Groceries", added.Value.Description);
        db.Context.ChangeTracker.Clear();
        Assert.Single(await transactions.GetByMonthAsync(2019, 5));

        var edited = await new EditTransaction(transactions, parties, locations).ExecuteAsync(
            new EditTransactionCommand(added.Value.Id, command.Date, command.Type, command.Amount,
                category.Id, null, null, null, null, "Updated", null, []));
        Assert.True(edited.IsSuccess);
        db.Context.ChangeTracker.Clear();
        var loaded = Assert.Single(await transactions.GetByMonthAsync(2019, 5));
        Assert.Null(loaded.PayerId);
        Assert.Null(loaded.Payer);
        Assert.Null(loaded.CounterpartyId);
        Assert.Null(loaded.Counterparty);
        Assert.Equal("Updated", loaded.Description);
        Assert.Single(await parties.GetAllAsync());
        // Clearing both links permits deleting the formerly referenced dictionary entry.
        db.Context.Parties.Remove((await db.Context.Parties.SingleAsync()));
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        Assert.NotNull(await transactions.GetByIdAsync(added.Value.Id));
    }

    [Fact]
    public async Task InitialMigration_RejectsNullDescription()
    {
        using var db = new TestDatabase(useMigrations: true);
        var category = new Category { Name = "Food" };
        db.Context.Add(category);
        await db.Context.SaveChangesAsync();
        db.Context.Add(new Transaction { CategoryId = category.Id, Date = new DateOnly(2019, 5, 1),
            Amount = 1m, Description = null! });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.Context.SaveChangesAsync());
    }
}
