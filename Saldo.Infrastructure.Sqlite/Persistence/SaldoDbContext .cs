using Microsoft.EntityFrameworkCore;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence;

public sealed class SaldoDbContext : DbContext
{
  public DbSet<Party> Parties => Set<Party>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();

    public SaldoDbContext(DbContextOptions<SaldoDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(SaldoDbContext).Assembly);
    }
}
