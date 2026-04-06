using Microsoft.EntityFrameworkCore;
using Saldo.Domain.Entities;

namespace Saldo.Infrastructure.Sqlite.Persistence;

public sealed class SaldoDbContext : DbContext
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Counterparty> Counterparties => Set<Counterparty>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();

    public SaldoDbContext(DbContextOptions<SaldoDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Member
        model.Entity<Member>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // Category
        model.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // Counterparty
        model.Entity<Counterparty>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // Tag
        model.Entity<Tag>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // Transaction
        model.Entity<Transaction>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);

            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Location).HasMaxLength(100);

            // Indexes pod raporty miesięczne i filtrowanie
            e.HasIndex(x => x.Date);
            e.HasIndex(x => new { x.Date, x.Direction });
            e.HasIndex(x => x.CategoryId);
            e.HasIndex(x => x.PayerId);
            e.HasIndex(x => x.CounterpartyId);

            // Relationships
            e.HasOne(x => x.Category)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Payer)
                .WithMany(x => x.TransactionsPaid)
                .HasForeignKey(x => x.PayerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Counterparty)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.CounterpartyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Many-to-many: Transaction <-> Tag (join entity)
        model.Entity<TransactionTag>(e =>
        {
            e.HasKey(x => new { x.TransactionId, x.TagId });

            e.HasOne(x => x.Transaction)
                .WithMany(x => x.Tags)
                .HasForeignKey(x => x.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Tag)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.TagId);
        });
    }
}
