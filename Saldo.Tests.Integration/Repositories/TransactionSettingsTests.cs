using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.UseCases;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Repositories;
using Saldo.Tests.Integration.Helpers;

namespace Saldo.Tests.Integration.Repositories;

public sealed class TransactionSettingsTests
{
    [Fact]
    public async Task InitialMigration_RenamedSelfPartyRemainsDefault()
    {
        using var db = new TestDatabase(useMigrations: true);
        Assert.False(db.Context.Database.HasPendingModelChanges());
        var settings = new TransactionSettingsRepository(db.Context);
        var self = await db.Context.Parties.SingleAsync(p => p.Name == "Ja");
        Assert.Equal(self.Id, (await settings.GetAsync()).DefaultPayerId);
        self.Name = "Marcin";
        db.Context.Parties.Add(new Party { Name = "Adam" });
        await db.Context.SaveChangesAsync();
        db.Context.ChangeTracker.Clear();
        Assert.Equal(self.Id, (await new GetNewTransactionDefaults(settings).ExecuteAsync()).PayerId);
    }

    [Fact]
    public async Task SaveChangeAndClear_PersistAcrossContexts()
    {
        using var db = new TestDatabase(useMigrations: true);
        var settings = new TransactionSettingsRepository(db.Context);
        var parties = new PartyRepository(db.Context, NullLogger<PartyRepository>.Instance);
        var other = new Party { Name = "Other" };
        await parties.AddAsync(other);
        var save = new SetDefaultPayer(settings, parties);
        await save.ExecuteAsync(other.Id);
        db.Context.ChangeTracker.Clear();
        await using var reader = new Saldo.Infrastructure.Sqlite.Persistence.SaldoDbContext(
            new DbContextOptionsBuilder<Saldo.Infrastructure.Sqlite.Persistence.SaldoDbContext>()
                .UseSqlite(db.Context.Database.GetConnectionString()!).Options);
        Assert.Equal(other.Id, (await new TransactionSettingsRepository(reader).GetAsync()).DefaultPayerId);
        await save.ExecuteAsync(null);
        Assert.Null((await new GetNewTransactionDefaults(new TransactionSettingsRepository(reader)).ExecuteAsync()).PayerId);
        Assert.Equal(1, await db.Context.TransactionSettings.CountAsync());
    }

    [Fact]
    public async Task DeleteUnusedDefaultParty_ClearsSetting()
    {
        using var db = new TestDatabase(useMigrations: true);
        var settings = new TransactionSettingsRepository(db.Context);
        var id = (await settings.GetAsync()).DefaultPayerId!.Value;
        await new PartyRepository(db.Context, NullLogger<PartyRepository>.Instance).DeleteAsync(id);
        Assert.Null((await new GetNewTransactionDefaults(settings).ExecuteAsync()).PayerId);
    }

    [Fact]
    public async Task InvalidDefaultParty_IsRejectedWithoutChangingSettings()
    {
        using var db = new TestDatabase(useMigrations: true);
        var settings = new TransactionSettingsRepository(db.Context);
        var save = new SetDefaultPayer(settings, new PartyRepository(db.Context, NullLogger<PartyRepository>.Instance));
        await Assert.ThrowsAsync<ArgumentException>(() => save.ExecuteAsync(999));
        Assert.Equal(1, (await settings.GetAsync()).DefaultPayerId);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() =>
            db.Context.Database.ExecuteSqlRawAsync("INSERT INTO TransactionSettings (Id) VALUES (2)"));
    }
}
