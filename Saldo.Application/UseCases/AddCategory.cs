using Saldo.Application.Interfaces;
using Saldo.Domain.Entities;

namespace Saldo.Application.UseCases;

public sealed class AddCategory
{
    private readonly ICategoryRepository _categories;

    public AddCategory(ICategoryRepository categories) => _categories = categories;

    public async Task<Category> ExecuteAsync(string name, CancellationToken ct = default)
    {
        var category = new Category { Name = NormalizeName(name) };
        await _categories.AddAsync(category, ct);
        return category;
    }

    private static string NormalizeName(string name) => string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Name cannot be empty.", nameof(name))
        : name.Trim();
}
