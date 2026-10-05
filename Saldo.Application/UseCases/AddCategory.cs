using Saldo.Application.Interfaces;
using Saldo.Application.Errors;
using Saldo.Domain.Entities;

namespace Saldo.Application.UseCases;

public sealed class AddCategory
{
    private readonly ICategoryRepository _categories;

    public AddCategory(ICategoryRepository categories) => _categories = categories;

    public async Task<Category> ExecuteAsync(string name, string? colorCode = null, CancellationToken ct = default, string? iconKey = null)
    {
        var normalizedName = CategoryInputNormalizer.NormalizeName(name);
        var normalizedIconKey = CategoryIconKeyNormalizer.Normalize(iconKey);
        var normalizedColorCode = CategoryInputNormalizer.NormalizeColorCode(colorCode);
        if ((await _categories.GetAllAsync(ct)).Any(category => string.Equals(category.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DuplicateReferenceException("category", normalizedName);
        }

        var category = new Category
        {
            Name = normalizedName,
            IconKey = normalizedIconKey,
            ColorCode = normalizedColorCode
        };
        await _categories.AddAsync(category, ct);
        return category;
    }
}
