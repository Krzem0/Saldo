using Microsoft.EntityFrameworkCore;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class TagWorkflowTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" #aabbcc ", "#AABBCC")]
    public async Task TagColor_InitialMigration_AddEditAndClearPersist(string? input, string? expected)
    {
        using var db = new TestDatabase(useMigrations: true);
        Assert.False(db.Context.Database.HasPendingModelChanges());
        var tags = new TagRepository(db.Context);
        var tag = await new AddTag(tags).ExecuteAsync(" Dla Iwony ", input);
        db.Context.ChangeTracker.Clear();
        Assert.Equal(expected, (await tags.GetByIdAsync(tag.Id))!.ColorCode);

        await new EditTag(tags).ExecuteAsync(tag.Id, " Dla bliskich ", " #12ab34 ");
        db.Context.ChangeTracker.Clear();
        var edited = (await tags.GetByIdAsync(tag.Id))!;
        Assert.Equal("Dla bliskich", edited.Name);
        Assert.Equal("#12AB34", edited.ColorCode);

        await new EditTag(tags).ExecuteAsync(tag.Id, edited.Name, null);
        db.Context.ChangeTracker.Clear();
        Assert.Null((await tags.GetByIdAsync(tag.Id))!.ColorCode);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#ABC")]
    [InlineData("#GG0000")]
    [InlineData("112233")]
    [InlineData("#FF112233")]
    public async Task InvalidTagColor_AddAndEditDoNotModifyStoredData(string colorCode)
    {
        using var db = new TestDatabase(useMigrations: true);
        var tags = new TagRepository(db.Context);
        var tag = await new AddTag(tags).ExecuteAsync("Original", "#123456");
        await Assert.ThrowsAsync<ArgumentException>(() => new AddTag(tags).ExecuteAsync("New", colorCode));
        await Assert.ThrowsAsync<ArgumentException>(() => new EditTag(tags).ExecuteAsync(tag.Id, "Changed", colorCode));
        // Even a later SaveChanges in the same scope must not persist partially edited values.
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        var unchanged = Assert.Single(await tags.GetAllAsync());
        Assert.Equal("Original", unchanged.Name);
        Assert.Equal("#123456", unchanged.ColorCode);
    }

    [Fact]
    public async Task TransactionUpdate_FailedTagWrite_RollsBackAmountAndOriginalTags()
    {
        using var db = new TestDatabase();
        var category = new Category { Name = "Food" };
        var party = new Party { Name = "Me" };
        var tag = new Tag { Name = "Dla Iwony" };
        db.Context.AddRange(category, party, tag);
        await db.Context.SaveChangesAsync();
        var repository = new TransactionRepository(db.Context);
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 10, 4), Amount = 10m, Type = TransactionType.Expense,
            CategoryId = category.Id, PayerId = party.Id, CounterpartyId = party.Id,
            Tags = [new TransactionTag { TagId = tag.Id }]
        };
        await repository.AddAsync(transaction);
        db.Context.ChangeTracker.Clear();
        var edit = (await repository.GetByIdAsync(transaction.Id))!;
        edit.Amount = 99m;
        edit.Tags = [new TransactionTag { TagId = 999 }];

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.UpdateAsync(edit));

        db.Context.ChangeTracker.Clear();
        var unchanged = (await repository.GetByIdAsync(transaction.Id))!;
        Assert.Equal(10m, unchanged.Amount);
        Assert.Equal(tag.Id, Assert.Single(unchanged.Tags).TagId);
    }

    [Fact]
    public async Task AddEditDelete_Tag_EnforcesUniqueNamesAndPersistsChanges()
    {
        using var db = new TestDatabase();
        var tags = new TagRepository(db.Context);
        var tag = await new AddTag(tags).ExecuteAsync(" Dla Iwony ");
        Assert.Equal("Dla Iwony", tag.Name);
        await Assert.ThrowsAsync<DuplicateReferenceException>(() => new AddTag(tags).ExecuteAsync("dla iwony"));
        await new EditTag(tags).ExecuteAsync(tag.Id, " Dla Iwony ");
        var other = await new AddTag(tags).ExecuteAsync("Wakacje");
        await Assert.ThrowsAsync<DuplicateReferenceException>(() => new EditTag(tags).ExecuteAsync(tag.Id, "wakacje"));
        await new EditTag(tags).ExecuteAsync(tag.Id, " Dla bliskich ");
        Assert.Equal("Dla bliskich", (await tags.GetByIdAsync(tag.Id))!.Name);
        await tags.DeleteAsync(other.Id);
        Assert.Single(await tags.GetAllAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("This tag name is deliberately longer than fifty characters")]
    public async Task AddEdit_InvalidName_DoesNotChangeStoredTags(string name)
    {
        using var db = new TestDatabase();
        var tags = new TagRepository(db.Context);
        var tag = await new AddTag(tags).ExecuteAsync("Dla Iwony");
        await Assert.ThrowsAsync<InvalidTagNameException>(() => new AddTag(tags).ExecuteAsync(name));
        await Assert.ThrowsAsync<InvalidTagNameException>(() => new EditTag(tags).ExecuteAsync(tag.Id, name));
        Assert.Equal("Dla Iwony", (await tags.GetByIdAsync(tag.Id))!.Name);
        Assert.Single(await tags.GetAllAsync());
    }

    [Fact]
    public async Task Transaction_AddEditRenameAndRemoveTags_PreservesLinksAndMapsIds()
    {
        using var db = new TestDatabase();
        var tags = new TagRepository(db.Context);
        var iwona = await new AddTag(tags).ExecuteAsync("Dla Iwony");
        var holiday = await new AddTag(tags).ExecuteAsync("Wakacje");
        var category = new Category { Name = "Używki" };
        var party = new Party { Name = "Ja" };
        db.Context.AddRange(category, party);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        var transactions = new TransactionRepository(db.Context);
        var parties = new PartyRepository(db.Context, Microsoft.Extensions.Logging.Abstractions.NullLogger<PartyRepository>.Instance);
        var locations = new LocationRepository(db.Context, Microsoft.Extensions.Logging.Abstractions.NullLogger<LocationRepository>.Instance);
        var added = await new AddTransaction(transactions, parties, locations).ExecuteAsync(new AddTransactionCommand(
            new DateOnly(2026, 10, 4), TransactionType.Expense, 20m, category.Id,
            party.Id, party.Name, party.Id, party.Name, "Papierosy", null, [iwona.Id, holiday.Id]));
        Assert.True(added.IsSuccess);
        Assert.Equal(new[] { iwona.Id, holiday.Id }, added.Value.TagIds);
        Assert.Equal(new[] { "Dla Iwony", "Wakacje" }, added.Value.Tags);
        await Assert.ThrowsAsync<ReferenceEntityInUseException>(() => tags.DeleteAsync(iwona.Id));

        await new EditTag(tags).ExecuteAsync(iwona.Id, "Dla bliskich");
        var listed = Assert.Single(await new ListTransactions(transactions).ExecuteAsync(new ListTransactionsQuery(2026, 10)));
        Assert.Contains("Dla bliskich", listed.Tags);
        Assert.Contains(iwona.Id, listed.TagIds);

        var edit = new EditTransaction(transactions, parties, locations);
        EditTransactionCommand Command(IReadOnlyList<int> ids) => new(added.Value.Id,
            added.Value.Date, added.Value.Type, 25m, category.Id, party.Id, party.Name,
            party.Id, party.Name, "Updated", null, ids);
        db.Context.ChangeTracker.Clear();
        var changed = await edit.ExecuteAsync(Command([iwona.Id]));
        Assert.True(changed.IsSuccess);
        Assert.Equal(new[] { iwona.Id }, changed.Value.TagIds);
        Assert.Equal("Dla bliskich", Assert.Single(changed.Value.Tags));
        db.Context.ChangeTracker.Clear();
        var cleared = await edit.ExecuteAsync(Command([]));
        Assert.True(cleared.IsSuccess);
        Assert.Empty(cleared.Value.TagIds);
        Assert.Empty(await db.Context.TransactionTags.ToListAsync());
        await tags.DeleteAsync(iwona.Id);
        Assert.Null(await tags.GetByIdAsync(iwona.Id));
    }
}
