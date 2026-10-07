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
        Assert.Null((await settings.GetAsync()).DefaultLocationId);
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
    public async Task DefaultLocation_SaveRenameDeleteAndClear_PersistAndPreservePayer()
    {
        using var db = new TestDatabase(useMigrations: true);
        var settings = new TransactionSettingsRepository(db.Context);
        var locations = new LocationRepository(db.Context, NullLogger<LocationRepository>.Instance);
        var location = new Location { Name = "Home" };
        await locations.AddAsync(location);
        var save = new SetDefaultLocation(settings, locations);
        await save.ExecuteAsync(location.Id);
        location.Name = "Renamed";
        await locations.UpdateAsync(location);
        db.Context.ChangeTracker.Clear();
        await using var reader = new Saldo.Infrastructure.Sqlite.Persistence.SaldoDbContext(
            new DbContextOptionsBuilder<Saldo.Infrastructure.Sqlite.Persistence.SaldoDbContext>()
                .UseSqlite(db.Context.Database.GetConnectionString()!).Options);
        var defaults = await new GetNewTransactionDefaults(new TransactionSettingsRepository(reader)).ExecuteAsync();
        Assert.Equal(location.Id, defaults.LocationId);
        Assert.Equal(1, defaults.PayerId);
        await Assert.ThrowsAsync<ArgumentException>(() => save.ExecuteAsync(999));
        Assert.Equal(location.Id, (await settings.GetAsync()).DefaultLocationId);
        await save.ExecuteAsync(null);
        Assert.Null((await new TransactionSettingsRepository(reader).GetAsync()).DefaultLocationId);
        await save.ExecuteAsync(location.Id);
        await locations.DeleteAsync(location.Id);
        Assert.Null((await new TransactionSettingsRepository(reader).GetAsync()).DefaultLocationId);
        Assert.Equal(1, (await settings.GetAsync()).DefaultPayerId);
    }

    [Fact]
    public async Task SetBothDefaults_InvalidLocationDoesNotSavePayer()
    {
        using var db = new TestDatabase(useMigrations: true);
        var settings = new TransactionSettingsRepository(db.Context);
        var save = new SetTransactionDefaults(settings,
            new PartyRepository(db.Context, NullLogger<PartyRepository>.Instance),
            new LocationRepository(db.Context, NullLogger<LocationRepository>.Instance));
        await Assert.ThrowsAsync<ArgumentException>(() => save.ExecuteAsync(null, 999));
        Assert.Equal(1, (await settings.GetAsync()).DefaultPayerId);
        Assert.Null((await settings.GetAsync()).DefaultLocationId);
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
