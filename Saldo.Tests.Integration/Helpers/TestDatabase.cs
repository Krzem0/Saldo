using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Saldo.Infrastructure.Sqlite.Persistence;

namespace Saldo.Tests.Integration.Helpers;

internal sealed class TestDatabase : IDisposable
{
    private readonly string _path;

    public SaldoDbContext Context { get; }

    public TestDatabase(bool useMigrations = false)
    {
        _path = Path.Combine(Path.GetTempPath(), $"saldo-test-{Guid.NewGuid():N}.db");

        var options = new DbContextOptionsBuilder<SaldoDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;

        Context = new SaldoDbContext(options);
        if (useMigrations)
        {
            Context.Database.Migrate();
        }
        else
        {
            Context.Database.EnsureCreated();
        }
    }

    public void Dispose()
    {
        Context.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
