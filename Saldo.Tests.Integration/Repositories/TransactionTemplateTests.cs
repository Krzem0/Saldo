using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class TransactionTemplateTests
{
    private static TransactionTemplateInput Input(TestDatabase db, TransactionTemplateRepository templates) => new(
        templates, new CategoryRepository(db.Context, NullLogger<CategoryRepository>.Instance),
        new PartyRepository(db.Context, NullLogger<PartyRepository>.Instance),
        new LocationRepository(db.Context, NullLogger<LocationRepository>.Instance), new TagRepository(db.Context));

    private static async Task<Category> Category(TestDatabase db)
    {
        var category = new Category { Name = "Food" };
        db.Context.Add(category);
        await db.Context.SaveChangesAsync();
        return category;
    }

    [Fact]
    public async Task InitialMigration_CoreCrud_PersistsOptionalAmountReferencesAndTags()
    {
        using var db = new TestDatabase(useMigrations: true);
        Assert.False(db.Context.Database.HasPendingModelChanges());
        var category = await Category(db);
        var party = new Party { Name = "Społem" };
        var location = new Location { Name = "Olsztyn" };
        var first = new Tag { Name = "Work" };
        var second = new Tag { Name = "Family" };
        db.Context.AddRange(party, location, first, second);
        await db.Context.SaveChangesAsync();
        var repository = new TransactionTemplateRepository(db.Context);
        var input = Input(db, repository);
        var command = new SaveTransactionTemplateCommand(" Work groceries ", TransactionType.Expense,
            " Shopping ", category.Id, 1, party.Id, location.Id, null, [first.Id, second.Id, first.Id]);
        var added = await new AddTransactionTemplate(repository, input).ExecuteAsync(command);
        Assert.True(added.IsSuccess);
        Assert.Equal("Work groceries", added.Value.Name);
        Assert.Equal("Shopping", added.Value.Description);
        Assert.Null(added.Value.Amount);
        Assert.Equal(2, added.Value.TagIds.Count);
        db.Context.ChangeTracker.Clear();
        var read = await new GetTransactionTemplate(repository).ExecuteAsync(added.Value.Id);
        Assert.Equal(command.CategoryId, read.Value.CategoryId);
        Assert.Equal(command.PayerId, read.Value.PayerId);
        Assert.Equal(command.CounterpartyId, read.Value.CounterpartyId);
        Assert.Equal(command.LocationId, read.Value.LocationId);
        Assert.Equal(added.Value.TagIds, read.Value.TagIds);

        var edit = new EditTransactionTemplate(repository, input);
        var edited = await edit.ExecuteAsync(added.Value.Id, command with
        {
            Name = " Fixed expense ", TransactionType = TransactionType.Income, Amount = 123.45m,
            PayerId = null, CounterpartyId = null, LocationId = null, TagIds = [second.Id]
        });
        Assert.True(edited.IsSuccess);
        db.Context.ChangeTracker.Clear();
        var listed = Assert.Single(await new ListTransactionTemplates(repository).ExecuteAsync());
        Assert.Equal("Fixed expense", listed.Name);
        Assert.Equal(TransactionType.Income, listed.TransactionType);
        Assert.Equal(123.45m, listed.Amount);
        Assert.Null(listed.PayerId);
        Assert.Null(listed.CounterpartyId);
        Assert.Null(listed.LocationId);
        Assert.Equal(second.Id, Assert.Single(listed.TagIds));
        Assert.True((await edit.ExecuteAsync(listed.Id, command with { Amount = null, TagIds = [] })).IsSuccess);
        db.Context.ChangeTracker.Clear();
        Assert.Null((await repository.GetByIdAsync(listed.Id))!.Amount);
        Assert.Empty(await db.Context.TransactionTemplateTags.ToListAsync());
        Assert.True((await new DeleteTransactionTemplate(repository).ExecuteAsync(listed.Id)).IsSuccess);
        Assert.Empty(await repository.GetAllAsync());
        Assert.True((await new GetTransactionTemplate(repository).ExecuteAsync(listed.Id)).IsFailed);
    }

    [Fact]
    public async Task SameContext_EditAfterAdd_ReplaceTagsAndDelete_CascadesOnlyTemplateLinks()
    {
        using var db = new TestDatabase(useMigrations: true);
        var category = await Category(db);
        var tag = new Tag { Name = "Work" };
        db.Context.Add(tag);
        await db.Context.SaveChangesAsync();
        var repo = new TransactionTemplateRepository(db.Context);
        var input = Input(db, repo);
        var command = new SaveTransactionTemplateCommand("A", TransactionType.Expense, "Shopping", category.Id, TagIds: [tag.Id]);
        var added = await new AddTransactionTemplate(repo, input).ExecuteAsync(command);
        Assert.True((await new EditTransactionTemplate(repo, input).ExecuteAsync(added.Value.Id, command with { Amount = 10m })).IsSuccess);
        Assert.True((await new DeleteTransactionTemplate(repo).ExecuteAsync(added.Value.Id)).IsSuccess);
        Assert.Empty(await db.Context.TransactionTemplateTags.ToListAsync());
        Assert.NotNull(await db.Context.Tags.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tag.Id));
        Assert.NotNull(await db.Context.Categories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == category.Id));
    }

    [Fact]
    public async Task InvalidInputAndDuplicateNames_DoNotWriteOrModifyTemplates()
    {
        using var db = new TestDatabase(useMigrations: true);
        var category = await Category(db);
        var repo = new TransactionTemplateRepository(db.Context);
        var input = Input(db, repo);
        var add = new AddTransactionTemplate(repo, input);
        var edit = new EditTransactionTemplate(repo, input);
        var valid = new SaveTransactionTemplateCommand("Original", TransactionType.Expense, "Shopping", category.Id);
        var original = (await add.ExecuteAsync(valid)).Value;
        var invalid = new[]
        {
            valid with { Name = " " }, valid with { Name = new string('a', 101) },
            valid with { Description = " ", CategoryId = null }, valid with { Description = new string('a', 501) },
            valid with { TransactionType = (TransactionType)99 }, valid with { Amount = 0 },
            valid with { Amount = -1 }, valid with { CategoryId = 99999 },
            valid with { PayerId = 99999 }, valid with { CounterpartyId = 99999 },
            valid with { LocationId = 99999 }, valid with { TagIds = [99999] }, valid with { TagIds = [0] }
        };
        foreach (var command in invalid)
        {
            var failed = await add.ExecuteAsync(command);
            Assert.True(failed.IsFailed);
            Assert.All(failed.Errors, error => Assert.True(error.Metadata.ContainsKey("PropertyName")));
            Assert.True((await edit.ExecuteAsync(original.Id, command)).IsFailed);
        }
        var duplicate = await add.ExecuteAsync(valid with { Name = " original " });
        Assert.Contains(duplicate.Errors, e => e.Message == "TransactionTemplate.NameDuplicate");
        var other = (await add.ExecuteAsync(valid with { Name = "Other", Description = "Other shopping" })).Value;
        Assert.True((await edit.ExecuteAsync(other.Id, valid)).IsFailed);
        Assert.True((await edit.ExecuteAsync(original.Id, valid)).IsSuccess);
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        Assert.Equal("Original", (await repo.GetByIdAsync(original.Id))!.Name);
        Assert.Null((await repo.GetByIdAsync(original.Id))!.Amount);
        Assert.Equal(2, (await repo.GetAllAsync()).Count);
        Assert.True((await edit.ExecuteAsync(99999, valid)).IsFailed);
        Assert.True((await new DeleteTransactionTemplate(repo).ExecuteAsync(99999)).IsFailed);
        Assert.True((await new GetTransactionTemplate(repo).ExecuteAsync(0)).IsFailed);
    }

    [Fact]
    public async Task UsedDictionaryEntries_AreProtectedUntilTemplateDeleted()
    {
        using var db = new TestDatabase(useMigrations: true);
        var category = await Category(db);
        var payer = new Party { Name = "Payer" };
        var counterparty = new Party { Name = "Shop" };
        var location = new Location { Name = "Town" };
        var tag = new Tag { Name = "Work" };
        db.Context.AddRange(payer, counterparty, location, tag);
        await db.Context.SaveChangesAsync();
        var repo = new TransactionTemplateRepository(db.Context);
        var added = await new AddTransactionTemplate(repo, Input(db, repo)).ExecuteAsync(new(
            "Groceries", TransactionType.Expense, "Shopping", category.Id, payer.Id, counterparty.Id, location.Id, TagIds: [tag.Id]));
        var categories = new CategoryRepository(db.Context, NullLogger<CategoryRepository>.Instance);
        var parties = new PartyRepository(db.Context, NullLogger<PartyRepository>.Instance);
        var locations = new LocationRepository(db.Context, NullLogger<LocationRepository>.Instance);
        var tags = new TagRepository(db.Context);
        await Assert.ThrowsAsync<ReferenceEntityInUseException>(() => categories.DeleteAsync(category.Id));
        await Assert.ThrowsAsync<ReferenceEntityInUseException>(() => parties.DeleteAsync(payer.Id));
        await Assert.ThrowsAsync<ReferenceEntityInUseException>(() => parties.DeleteAsync(counterparty.Id));
        await Assert.ThrowsAsync<ReferenceEntityInUseException>(() => locations.DeleteAsync(location.Id));
        await Assert.ThrowsAsync<ReferenceEntityInUseException>(() => tags.DeleteAsync(tag.Id));
        await new DeleteTransactionTemplate(repo).ExecuteAsync(added.Value.Id);
        await categories.DeleteAsync(category.Id);
        await parties.DeleteAsync(payer.Id);
        await parties.DeleteAsync(counterparty.Id);
        await locations.DeleteAsync(location.Id);
        await tags.DeleteAsync(tag.Id);
    }

    [Fact]
    public async Task FailedRepositoryEdit_RollsBackScalarValuesAndTagReplacement()
    {
        using var db = new TestDatabase(useMigrations: true);
        var category = await Category(db);
        var tag = new Tag { Name = "Work" };
        db.Context.Add(tag);
        await db.Context.SaveChangesAsync();
        var repo = new TransactionTemplateRepository(db.Context);
        var added = (await new AddTransactionTemplate(repo, Input(db, repo)).ExecuteAsync(new(
            "Original", TransactionType.Expense, "Shopping", category.Id, TagIds: [tag.Id]))).Value;
        db.Context.ChangeTracker.Clear();
        var stored = (await repo.GetByIdAsync(added.Id))!;
        stored.Amount = 99m;
        stored.Tags = [new TransactionTemplateTag { TagId = 99999 }];
        await Assert.ThrowsAsync<DbUpdateException>(() => repo.UpdateAsync(stored));
        db.Context.ChangeTracker.Clear();
        var unchanged = (await repo.GetByIdAsync(added.Id))!;
        Assert.Null(unchanged.Amount);
        Assert.Equal(tag.Id, Assert.Single(unchanged.Tags).TagId);
    }
}
