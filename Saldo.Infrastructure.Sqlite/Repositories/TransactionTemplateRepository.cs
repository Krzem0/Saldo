using Microsoft.EntityFrameworkCore;
using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class TransactionTemplateRepository(SaldoDbContext context) : ITransactionTemplateRepository
{
    public async Task<TransactionTemplate?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await context.TransactionTemplates.AsNoTracking().Include(x => x.Tags).Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<TransactionTemplate>> GetAllAsync(CancellationToken ct = default) =>
        await context.TransactionTemplates.AsNoTracking().Include(x => x.Tags).Include(x => x.Category).OrderBy(x => x.Name).ToListAsync(ct);

    public async Task AddAsync(TransactionTemplate template, CancellationToken ct = default)
    {
        ClearNavigations(template);
        context.TransactionTemplates.Add(template);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TransactionTemplate template, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var stored = await context.TransactionTemplates.Include(x => x.Tags).FirstOrDefaultAsync(x => x.Id == template.Id, ct)
            ?? throw new KeyNotFoundException($"Transaction template {template.Id} not found.");
        var tagIds = template.Tags.Select(x => x.TagId).Distinct().ToHashSet();
        context.Entry(stored).CurrentValues.SetValues(template);
        foreach (var link in stored.Tags.Where(x => !tagIds.Contains(x.TagId)).ToList())
        {
            stored.Tags.Remove(link);
            context.TransactionTemplateTags.Remove(link);
        }
        var existingIds = stored.Tags.Select(x => x.TagId).ToHashSet();
        foreach (var tagId in tagIds.Except(existingIds))
            stored.Tags.Add(new TransactionTemplateTag { TransactionTemplateId = stored.Id, TagId = tagId });
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        context.EnsureCanWrite();
        await context.TransactionTemplates.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        // ExecuteDelete bypasses tracking; detach stale entries for subsequent operations in this scope.
        foreach (var entry in context.ChangeTracker.Entries<TransactionTemplateTag>()
            .Where(x => x.Entity.TransactionTemplateId == id).ToList()) entry.State = EntityState.Detached;
        foreach (var entry in context.ChangeTracker.Entries<TransactionTemplate>()
            .Where(x => x.Entity.Id == id).ToList()) entry.State = EntityState.Detached;
    }

    private static void ClearNavigations(TransactionTemplate template)
    {
        template.Category = null!;
        template.Payer = null;
        template.Counterparty = null;
        template.Location = null;
        foreach (var link in template.Tags) link.Tag = null!;
    }
}
