using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Saldo.Infrastructure.Sqlite.Persistence;

public sealed class SaldoDbContextFactory
    : IDesignTimeDbContextFactory<SaldoDbContext>
{
    public SaldoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SaldoDbContext>()
            .UseSqlite("Data Source=saldo-dev.db")
            .Options;

        return new SaldoDbContext(options);
    }
}
