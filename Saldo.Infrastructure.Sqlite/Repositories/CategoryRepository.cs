using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Saldo.Application.Interfaces;
using Saldo.Application.Errors;
using Saldo.Domain.Entities;
using Saldo.Infrastructure.Sqlite.Persistence;
using Microsoft.Extensions.Logging;

namespace Saldo.Infrastructure.Sqlite.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly SaldoDbContext _context;
    private readonly ILogger<CategoryRepository> _logger;

    public CategoryRepository(SaldoDbContext context, ILogger<CategoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Category?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public async Task AddAsync(Category category, CancellationToken ct = default)
    {
        _context.Categories.Add(category);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Category category, CancellationToken ct = default)
    {
        _context.Categories.Update(category);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        try
        {
            await _context.Categories
                .Where(c => c.Id == id)
                .ExecuteDeleteAsync(ct);
        }
        catch (SqliteException ex) when (IsForeignKeyViolation(ex))
        {
            _logger.LogWarning(ex, "Category {CategoryId} cannot be deleted because it is in use.", id);
            throw new ReferenceEntityInUseException(ex);
        }
    }

    private static bool IsForeignKeyViolation(SqliteException exception) =>
        exception.SqliteErrorCode == 19 && exception.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase);
}
