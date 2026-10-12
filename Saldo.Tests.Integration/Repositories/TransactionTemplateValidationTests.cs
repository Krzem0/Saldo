using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.DTOs;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Infrastructure.Sqlite.Persistence;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class TransactionTemplateValidationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ManualSql_UpgradesOldRequiredCategorySchemaWithoutLosingTemplatesOrTags(bool foreignKeys)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var setting = connection.CreateCommand())
        {
            setting.CommandText = foreignKeys ? "PRAGMA foreign_keys=ON;" : "PRAGMA foreign_keys=OFF;";
            await setting.ExecuteNonQueryAsync();
        }
        await using var context = new SaldoDbContext(new DbContextOptionsBuilder<SaldoDbContext>().UseSqlite(connection).Options);
        var script = context.GetService<IMigrator>().GenerateScript(toMigration: "20260406194301_InitialSchema");
        script = System.Text.RegularExpressions.Regex.Replace(script, "CREATE TABLE \"TransactionTemplates\"[\\s\\S]*?;", match =>
            System.Text.RegularExpressions.Regex.Replace(match.Value, "(\"CategoryId\" INTEGER)(?: NULL)?(,)", "$1 NOT NULL$2")
                .Replace("\"Description\" TEXT NULL", "\"Description\" TEXT NOT NULL"));
        Assert.Contains("\"CategoryId\" INTEGER NOT NULL", script);
        await context.Database.ExecuteSqlRawAsync(script);
        var category = new Category { Name = "Food" };
        var tag = new Tag { Name = "Work" };
        context.AddRange(category, tag);
        await context.SaveChangesAsync();
        var template = new TransactionTemplate { Name = "Existing", Description = "Shopping", CategoryId = category.Id,
            Tags = [new TransactionTemplateTag { TagId = tag.Id }] };
        context.Add(template);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await using var editorTransaction = await context.Database.BeginTransactionAsync();
        // SAVEPOINT also works in SQLite editors that already opened a transaction.
        await context.Database.ExecuteSqlRawAsync("""
            SAVEPOINT saldo_template_schema;
            CREATE TEMP TABLE saldo_template_tags_backup AS
                SELECT TransactionTemplateId, TagId FROM TransactionTemplateTags;
            CREATE TEMP TABLE saldo_template_sequence_backup AS
                SELECT seq FROM sqlite_sequence WHERE name = 'TransactionTemplates';
            CREATE TABLE TransactionTemplates_new (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                TransactionType INTEGER NOT NULL,
                Description TEXT NULL,
                Amount TEXT NULL,
                CategoryId INTEGER NULL,
                PayerId INTEGER NULL,
                CounterpartyId INTEGER NULL,
                LocationId INTEGER NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories (Id) ON DELETE RESTRICT,
                FOREIGN KEY (PayerId) REFERENCES Parties (Id) ON DELETE RESTRICT,
                FOREIGN KEY (CounterpartyId) REFERENCES Parties (Id) ON DELETE RESTRICT,
                FOREIGN KEY (LocationId) REFERENCES Locations (Id) ON DELETE RESTRICT
            );
            INSERT INTO TransactionTemplates_new
                (Id, Name, TransactionType, Description, Amount, CategoryId, PayerId, CounterpartyId, LocationId)
            SELECT Id, Name, TransactionType, NULLIF(TRIM(Description), ''), Amount, CategoryId, PayerId, CounterpartyId, LocationId
            FROM TransactionTemplates;
            DROP TABLE TransactionTemplates;
            ALTER TABLE TransactionTemplates_new RENAME TO TransactionTemplates;
            DELETE FROM TransactionTemplateTags;
            INSERT INTO TransactionTemplateTags (TransactionTemplateId, TagId)
                SELECT TransactionTemplateId, TagId FROM saldo_template_tags_backup;
            CREATE UNIQUE INDEX IX_TransactionTemplates_Name ON TransactionTemplates (Name);
            CREATE INDEX IX_TransactionTemplates_CategoryId ON TransactionTemplates (CategoryId);
            CREATE INDEX IX_TransactionTemplates_PayerId ON TransactionTemplates (PayerId);
            CREATE INDEX IX_TransactionTemplates_CounterpartyId ON TransactionTemplates (CounterpartyId);
            CREATE INDEX IX_TransactionTemplates_LocationId ON TransactionTemplates (LocationId);
            UPDATE sqlite_sequence SET seq = MAX(seq, COALESCE((SELECT seq FROM saldo_template_sequence_backup), 0))
                WHERE name = 'TransactionTemplates';
            DROP TABLE saldo_template_tags_backup;
            DROP TABLE saldo_template_sequence_backup;
            RELEASE SAVEPOINT saldo_template_schema;
            """);
        await editorTransaction.CommitAsync();
        var stored = await context.TransactionTemplates.AsNoTracking().Include(t => t.Tags).SingleAsync();
        Assert.Equal(template.Id, stored.Id);
        Assert.Equal(category.Id, stored.CategoryId);
        Assert.Equal(tag.Id, Assert.Single(stored.Tags).TagId);
        context.Add(new TransactionTemplate { Name = "Amount only", Amount = 10m });
        await context.SaveChangesAsync();
        Assert.Equal(2, await context.TransactionTemplates.CountAsync());
        await using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA foreign_key_check;";
        Assert.Null(await check.ExecuteScalarAsync());
    }

    private static TransactionTemplateInput Input(TestDatabase db, TransactionTemplateRepository repo) => new(repo,
        new CategoryRepository(db.Context, NullLogger<CategoryRepository>.Instance),
        new PartyRepository(db.Context, NullLogger<PartyRepository>.Instance),
        new LocationRepository(db.Context, NullLogger<LocationRepository>.Instance), new TagRepository(db.Context));

    [Fact]
    public async Task EachAdditionalFieldCanBeUsedAlone_AndEmptyAddOrEditDoesNotWrite()
    {
        using var db = new TestDatabase(useMigrations: true);
        var category = new Category { Name = "Food" };
        var party = new Party { Name = "Marcin" };
        var location = new Location { Name = "Olsztyn" };
        var tag = new Tag { Name = "Work" };
        db.Context.AddRange(category, party, location, tag);
        await db.Context.SaveChangesAsync();
        var repo = new TransactionTemplateRepository(db.Context);
        var input = Input(db, repo);
        var add = new AddTransactionTemplate(repo, input);
        var edit = new EditTransactionTemplate(repo, input);
        var empty = new SaveTransactionTemplateCommand("Empty", TransactionType.Expense, "  ");
        Assert.Contains((await add.ExecuteAsync(empty)).Errors, e => e.Message == "TransactionTemplate.ContentRequired");
        var commands = new[]
        {
            empty with { Name = "Description", Description = "Shopping" },
            empty with { Name = "Amount", Amount = 10m },
            empty with { Name = "Category", CategoryId = category.Id },
            empty with { Name = "Payer", PayerId = party.Id },
            empty with { Name = "Counterparty", CounterpartyId = party.Id },
            empty with { Name = "Location", LocationId = location.Id },
            empty with { Name = "Tags", TagIds = [tag.Id] }
        };
        foreach (var command in commands)
        {
            var added = await add.ExecuteAsync(command);
            Assert.True(added.IsSuccess);
            Assert.True((await edit.ExecuteAsync(added.Value.Id, command)).IsSuccess);
            Assert.Contains((await edit.ExecuteAsync(added.Value.Id, empty)).Errors, e => e.Message == "TransactionTemplate.ContentRequired");
        }
        db.Context.ChangeTracker.Clear();
        Assert.Equal(7, (await repo.GetAllAsync()).Count);
        var tagOnly = (await repo.GetAllAsync()).Single(t => t.Name == "Tags");
        Assert.Null(tagOnly.CategoryId);
        Assert.Null(tagOnly.Description);
        Assert.Equal(tag.Id, Assert.Single(tagOnly.Tags).TagId);
        Assert.False(db.Context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task IdenticalContentIsBlockedOnAddAndEdit_IgnoringNameCaseOuterWhitespaceAndTagOrder()
    {
        using var db = new TestDatabase(useMigrations: true);
        var first = new Tag { Name = "Work" };
        var second = new Tag { Name = "Family" };
        db.Context.AddRange(first, second);
        await db.Context.SaveChangesAsync();
        var repo = new TransactionTemplateRepository(db.Context);
        var input = Input(db, repo);
        var add = new AddTransactionTemplate(repo, input);
        var edit = new EditTransactionTemplate(repo, input);
        var command = new SaveTransactionTemplateCommand("A", TransactionType.Expense, " Shopping ", Amount: 12.50m, TagIds: [first.Id, second.Id]);
        var original = (await add.ExecuteAsync(command)).Value;
        var duplicate = command with { Name = "B", Description = "shopping", Amount = 12.5m, TagIds = [second.Id, first.Id, first.Id] };
        Assert.Contains((await add.ExecuteAsync(duplicate)).Errors, e => e.Message == "TransactionTemplate.ContentDuplicate");
        var other = (await add.ExecuteAsync(duplicate with { Amount = 13m })).Value;
        Assert.Contains((await edit.ExecuteAsync(other.Id, duplicate)).Errors, e => e.Message == "TransactionTemplate.ContentDuplicate");
        Assert.True((await edit.ExecuteAsync(original.Id, duplicate with { Name = "Renamed" })).IsSuccess);
        var differentType = await add.ExecuteAsync(command with { Name = "Income", TransactionType = TransactionType.Income });
        Assert.True(differentType.IsSuccess);
        var differentTags = await add.ExecuteAsync(command with { Name = "One tag", TagIds = [first.Id] });
        Assert.True(differentTags.IsSuccess);
        db.Context.ChangeTracker.Clear();
        Assert.Equal(13m, (await repo.GetByIdAsync(other.Id))!.Amount);
        Assert.Equal(4, (await repo.GetAllAsync()).Count);
    }

    [Fact]
    public async Task NullAndWhitespaceDescriptionsWithNoTagsAreEquivalent()
    {
        using var db = new TestDatabase(useMigrations: true);
        var repo = new TransactionTemplateRepository(db.Context);
        var add = new AddTransactionTemplate(repo, Input(db, repo));
        var command = new SaveTransactionTemplateCommand("A", TransactionType.Expense, Amount: 10m);
        Assert.True((await add.ExecuteAsync(command)).IsSuccess);
        Assert.Contains((await add.ExecuteAsync(command with { Name = "B", Description = " \t ", TagIds = [] })).Errors,
            e => e.Message == "TransactionTemplate.ContentDuplicate");
        Assert.Single(await repo.GetAllAsync());
    }
}
