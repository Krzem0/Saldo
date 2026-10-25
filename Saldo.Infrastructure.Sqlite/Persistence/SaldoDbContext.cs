using Microsoft.EntityFrameworkCore;
using Saldo.Domain.Entities;
using Saldo.Application.Interfaces;

namespace Saldo.Infrastructure.Sqlite.Persistence;

public sealed class SaldoDbContext : DbContext
{
    private readonly IDatabaseWriteAccess? _writeAccess;
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<TransactionSettings> TransactionSettings => Set<TransactionSettings>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();
    public DbSet<TransactionTemplate> TransactionTemplates => Set<TransactionTemplate>();
    public DbSet<TransactionTemplateTag> TransactionTemplateTags => Set<TransactionTemplateTag>();

    public SaldoDbContext(DbContextOptions<SaldoDbContext> options, IDatabaseWriteAccess? writeAccess = null)
        : base(options)
    {
        _writeAccess = writeAccess;
    }

    public void EnsureCanWrite() => _writeAccess?.EnsureCanWrite();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureCanWrite();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureCanWrite();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(SaldoDbContext).Assembly);
    }
}
