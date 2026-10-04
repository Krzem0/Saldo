using Saldo.Application.Interfaces;

namespace Saldo.Application.UseCases;

public sealed class EditCategory
{
    private readonly ICategoryRepository _categories;

    public EditCategory(ICategoryRepository categories) => _categories = categories;

    public async Task ExecuteAsync(int id, string name, string? colorCode = null, CancellationToken ct = default)
    {
        var normalizedName = CategoryInputNormalizer.NormalizeName(name);
        var normalizedColorCode = CategoryInputNormalizer.NormalizeColorCode(colorCode);
        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Category {id} not found.");

        category.Name = normalizedName;
        category.ColorCode = normalizedColorCode;
        await _categories.UpdateAsync(category, ct);
    }
}
